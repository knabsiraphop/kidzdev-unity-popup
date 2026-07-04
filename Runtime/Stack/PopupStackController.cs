using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Default <see cref="IPopupStack"/>. Unlike <see cref="PopupManager"/> (a modal LIFO where only the top
    /// popup is ever interactive), this shows up to <see cref="IStackLayout.MaxVisible"/> cards at once,
    /// queueing the rest until a slot frees. Arrangement and interactivity are delegated entirely to the
    /// <see cref="IStackLayout"/> — the controller only owns admission (the queue/cap) and each card's
    /// load → show → await-result → teardown lifecycle.
    /// </summary>
    /// <remarks>
    /// <para><b>Enqueue flow:</b> take an admission ticket (queues if the stack is at capacity) → load prefab →
    /// instantiate under the layer → <c>OnOpened</c> → reflow (positions this card and re-settles the others) →
    /// enter transition → await the result under a linked CTS (caller token + stack lifetime) → <c>OnClosing</c>
    /// → exit transition → destroy, release prefab, reflow the remainder, promote the next queued card.</para>
    /// <para>Main-thread only. <see cref="Dispose"/> cancels every in-flight and queued card and tears down the layer.</para>
    /// </remarks>
    public sealed class PopupStackController : IPopupStack
    {
        private sealed class Active
        {
            public IPopup Popup;
            public GameObject Instance;
            public RectTransform Rect;
        }

        private readonly IStackLayout _layout;
        private readonly IPopupLoader _loader;
        private readonly IPopupTransition _transition;
        private readonly Color? _backdropColor;
        private readonly int _sortOrder;
        private readonly Func<Transform> _layerFactory;
        private readonly Func<float, CancellationToken, UniTask> _autoDismissTimer;

        private readonly List<Active> _active = new List<Active>();
        private readonly Queue<UniTaskCompletionSource> _pendingAdmissions = new Queue<UniTaskCompletionSource>();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private int _reservedSlots;
        private Transform _layer;
        private PopupBackdrop _backdrop;
        private bool _disposed;

        /// <param name="layout">The arrangement/interactivity/capacity policy. Required — there is no default.</param>
        /// <param name="loader">Prefab loader; defaults to <see cref="CompositePopupLoader.CreateDefault"/> (Resources + Direct).</param>
        /// <param name="transition">Enter/exit transition each card plays; defaults to <see cref="InstantPopupTransition"/>.</param>
        /// <param name="backdropColor">
        /// Shared backdrop tint for the whole stack, raised on the first card and torn down when the stack is
        /// empty; <c>null</c> (the default) means no backdrop. It does not dismiss on tap — reward cards are
        /// collected, not tapped away.
        /// </param>
        /// <param name="sortOrder">Sorting order of the default overlay canvas (ignored when <paramref name="layerFactory"/> is set).</param>
        /// <param name="layerFactory">
        /// Optional factory for the layer cards are parented under, called lazily on the first card. Same
        /// ownership contract as <see cref="PopupManager"/>'s parameter of the same name: the controller destroys
        /// the returned object on <see cref="Dispose"/>, so return a fresh root, not a shared scene canvas.
        /// </param>
        /// <param name="autoDismissTimer">
        /// Optional timer backing <see cref="Enqueue{TResult}"/>'s <c>autoDismissAfter</c>; defaults to
        /// <c>UniTask.Delay</c> with <c>ignoreTimeScale: true</c> so a timed card still closes while the game
        /// is paused. Inject a custom timer for scaled, server-driven, or test-controlled timing.
        /// </param>
        public PopupStackController(
            IStackLayout layout,
            IPopupLoader loader = null,
            IPopupTransition transition = null,
            Color? backdropColor = null,
            int sortOrder = 1000,
            Func<Transform> layerFactory = null,
            Func<float, CancellationToken, UniTask> autoDismissTimer = null)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _loader = loader ?? CompositePopupLoader.CreateDefault();
            _transition = transition ?? new InstantPopupTransition();
            _backdropColor = backdropColor;
            _sortOrder = sortOrder;
            _layerFactory = layerFactory;
            _autoDismissTimer = autoDismissTimer ?? DefaultAutoDismissTimer;
        }

        private static UniTask DefaultAutoDismissTimer(float seconds, CancellationToken ct) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct);

        /// <inheritdoc/>
        public int VisibleCount => _active.Count;

        /// <inheritdoc/>
        public int QueuedCount => _pendingAdmissions.Count;

        /// <inheritdoc/>
        public async UniTask<TResult> Enqueue<TResult>(PopupRef reference, object arg = null, float autoDismissAfter = 0f, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var layer = EnsureLayer();

            // One linked token (caller + stack lifetime) threads the whole pipeline, so Dispose cancels even a
            // card still waiting for admission.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);

            // Reserve a slot before loading anything, so a card queued past capacity never touches the loader
            // (and never runs OnOpened) until a slot actually frees.
            var admission = new UniTaskCompletionSource();
            _pendingAdmissions.Enqueue(admission);
            TryPromote();
            await admission.Task.AttachExternalCancellation(linked.Token);

            var prefab = await _loader.LoadPrefabAsync(reference, linked.Token);
            if (prefab == null)
                throw new InvalidOperationException($"IPopupLoader returned a null prefab for {reference}.");

            Active entry = null;
            GameObject instance = null;
            object result = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, layer);
                var popup = instance.GetComponentInChildren<IPopup>(true);
                if (popup == null)
                    throw new InvalidOperationException(
                        $"Popup prefab for {reference} has no component implementing IPopup on its hierarchy.");

                var rect = instance.transform as RectTransform;
                if (rect == null)
                    throw new InvalidOperationException(
                        $"Popup prefab for {reference} must have a RectTransform root to be used in a PopupStack.");

                entry = new Active { Popup = popup, Instance = instance, Rect = rect };
                _active.Add(entry);
                EnsureBackdrop(layer);

                popup.Root.SetActive(true);
                popup.OnOpened(arg);
                RequestReflow();

                await _transition.PlayEnterAsync(popup, linked.Token);

                if (autoDismissAfter > 0f)
                    RunAutoDismissAsync(popup, autoDismissAfter, linked.Token).Forget();

                result = await popup.Result.AttachExternalCancellation(linked.Token);
                popup.OnClosing();
                await _transition.PlayExitAsync(popup, linked.Token);
            }
            finally
            {
                if (entry != null) _active.Remove(entry);
                DestroyObject(instance);
                _loader.ReleasePrefab(reference, prefab);
                _reservedSlots--;
                RequestReflow();
                TryPromote();
                TearDownBackdropIfEmpty();
            }

            return CastResult<TResult>(reference, result);
        }

        // Fires an Enqueue call's autoDismissAfter. Routes through TryDismiss so a vetoing card stays open and
        // the close result matches whatever a manual dismissal would produce. Cancellation (the card closed
        // first, or the stack tore down) is expected and silently swallowed.
        private async UniTaskVoid RunAutoDismissAsync(IPopup popup, float seconds, CancellationToken ct)
        {
            try
            {
                await _autoDismissTimer(seconds, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            popup.TryDismiss();
        }

        // A raw (TResult) cast would surface a bare InvalidCastException (or NRE unboxing null) that doesn't say
        // which card misbehaved; name the ref and both types so the mismatch is diagnosable from the exception.
        private static TResult CastResult<TResult>(in PopupRef reference, object result)
        {
            if (result is TResult typed) return typed;
            if (result == null && default(TResult) == null) return default;
            throw new InvalidCastException(
                $"Popup {reference} completed with {(result == null ? "null" : result.GetType().Name)}, " +
                $"which is not the awaited result type {typeof(TResult).Name}.");
        }

        /// <inheritdoc/>
        public void ClearAll()
        {
            // Snapshot both sets first: dismissing an active entry resumes its Enqueue call, which mutates
            // _active and may synchronously promote (and thus dequeue) a pending admission.
            var pendingSnapshot = _pendingAdmissions.ToArray();
            _pendingAdmissions.Clear();
            foreach (var admission in pendingSnapshot)
                admission.TrySetCanceled();

            var activeSnapshot = _active.ToArray();
            foreach (var entry in activeSnapshot)
                entry.Popup.TryDismiss();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Cancelling the lifetime token resumes every in-flight Enqueue (queued or shown) via cancellation,
            // whose finally blocks remove their entries and destroy their instances.
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            var pendingSnapshot = _pendingAdmissions.ToArray();
            _pendingAdmissions.Clear();
            foreach (var admission in pendingSnapshot)
                admission.TrySetCanceled();

            // Anything left (cards parked on the result await with nothing observing the throw) is cleaned here.
            foreach (var entry in _active)
                DestroyObject(entry.Instance);
            _active.Clear();

            if (_backdrop != null) DestroyObject(_backdrop.gameObject);
            _backdrop = null;

            if (_layer != null)
            {
                DestroyObject(_layer.gameObject);
                _layer = null;
            }
        }

        private void TryPromote()
        {
            while (_reservedSlots < _layout.MaxVisible && _pendingAdmissions.Count > 0)
            {
                var admission = _pendingAdmissions.Dequeue();
                if (admission.Task.Status != UniTaskStatus.Pending) continue; // cancelled while still queued
                _reservedSlots++;
                admission.TrySetResult();
            }
        }

        // Reflow is single-flight and coalesced, and fire-and-forget rather than awaited: if a layout's
        // ArrangeAsync animates (e.g. DeckStackLayout's slide), a card added or removed mid-animation must not
        // spawn a second, overlapping pass — two concurrent passes started from different snapshots of _active
        // would each finish and separately write CanvasGroup.interactable from their own stale count, and
        // whichever happened to finish last would silently win. (Awaiting one shared UniTask from multiple
        // callers doesn't work either — UniTask only supports one registered continuation at a time, even with
        // .Preserve(), so a second concurrent awaiter throws "Already continuation registered".) Instead every
        // caller just marks the pass "dirty"; the single running loop re-reads _active and repeats until a full
        // pass completes with nothing new queued. No caller needs to block on this: the enter/exit transition
        // and admission bookkeeping that follow don't depend on the reflow having visually settled yet.
        private bool _reflowRunning;
        private bool _reflowDirty;

        private void RequestReflow()
        {
            _reflowDirty = true;
            if (_reflowRunning) return;
            _reflowRunning = true;
            RunReflowLoopAsync().Forget();
        }

        private async UniTask RunReflowLoopAsync()
        {
            try
            {
                while (_reflowDirty)
                {
                    _reflowDirty = false;
                    try
                    {
                        var rects = new List<RectTransform>(_active.Count);
                        foreach (var entry in _active) rects.Add(entry.Rect);
                        await _layout.ArrangeAsync(rects, _lifetimeCts.Token);

                        for (int i = 0; i < _active.Count; i++)
                        {
                            var cg = GetOrAddCanvasGroup(_active[i].Instance);
                            bool interactable = _layout.IsInteractable(i, _active.Count);
                            cg.interactable = interactable; // gates Selectables that predate this card's Awake
                            cg.blocksRaycasts = interactable; // gates raycasts, e.g. a Deck card behind the front one
                            SetSelectablesInteractable(_active[i].Instance, interactable);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // The stack is tearing down; final visual state doesn't matter.
                        break;
                    }
                }
            }
            finally
            {
                _reflowRunning = false;
            }
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        // UnityEngine.UI.Selectable (Button, Toggle, ...) caches whether its ancestor CanvasGroups currently
        // allow interaction, and only re-evaluates that cache on specific lifecycle events (OnEnable,
        // OnTransformParentChanged) — not on a bare CanvasGroup.interactable assignment, nor on toggling the
        // Selectable's own `enabled` flag or the CanvasGroup's `enabled` flag (both verified to NOT refresh
        // it). Without this, a card reparented by a layout (e.g. GroupStackLayout) ends up with a Button whose
        // ancestor CanvasGroup correctly reads interactable=true but that still silently refuses clicks,
        // because Selectable's cached flag was never recalculated for the new parent chain. Setting
        // Selectable.interactable directly sidesteps the stale cache entirely — it's the component's own
        // property, checked directly by IsInteractable(), not derived from an ancestor scan.
        private static void SetSelectablesInteractable(GameObject go, bool interactable)
        {
            foreach (var selectable in go.GetComponentsInChildren<Selectable>(true))
                selectable.interactable = interactable;
        }

        private void EnsureBackdrop(Transform layer)
        {
            if (_backdrop != null || _backdropColor == null) return;

            // A layout that relocates cards elsewhere (GroupStackLayout) must have the backdrop raised in that
            // same destination, as the sibling immediately before it — otherwise the backdrop sits in this
            // controller's own overlay canvas while the cards render in a different one, and whichever canvas
            // sorts higher wins every raycast; a full-screen backdrop above the cards silently eats every click.
            var anchor = _layout.BackdropAnchor;
            if (anchor != null && anchor.parent != null)
            {
                _backdrop = PopupBackdrop.Create(anchor.parent, _backdropColor.Value);
                _backdrop.transform.SetSiblingIndex(anchor.GetSiblingIndex());
            }
            else
            {
                _backdrop = PopupBackdrop.Create(layer, _backdropColor.Value);
                _backdrop.transform.SetAsFirstSibling(); // stays behind every card
            }
        }

        private void TearDownBackdropIfEmpty()
        {
            if (_backdrop == null) return;
            if (_active.Count > 0 || _reservedSlots > 0) return;
            DestroyObject(_backdrop.gameObject);
            _backdrop = null;
        }

        // Popups live at runtime, but the EditMode test suite drives the controller outside play mode where
        // Object.Destroy is a deferred no-op that also logs an error. Pick the immediate variant when not playing.
        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        private Transform EnsureLayer()
        {
            if (_layer != null) return _layer;

            if (_layerFactory != null)
            {
                _layer = _layerFactory();
                if (_layer == null)
                    throw new InvalidOperationException(
                        "The layerFactory returned null; it must return the transform cards are parented under.");
                return _layer;
            }

            var go = new GameObject("[PopupStackLayer]");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortOrder;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            _layer = go.transform;
            return _layer;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PopupStackController));
        }
    }
}
