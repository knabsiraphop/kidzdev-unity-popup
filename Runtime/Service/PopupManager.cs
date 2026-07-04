using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Default <see cref="IPopupService"/>. Owns a high-sort-order overlay canvas and shows each popup on it with
    /// its own backdrop. Popups <b>stack</b> (reentrant): a warning can be shown over a confirm, and each
    /// <c>ShowAsync</c> is an independent await that tears its own instance down when it resolves.
    /// </summary>
    /// <remarks>
    /// <para><b>ShowAsync flow:</b> load prefab → instantiate a fresh instance under the layer → raise a backdrop →
    /// <c>OnOpened</c> → enter transition → await the result under a linked CTS (caller token + manager lifetime) →
    /// <c>OnClosing</c> → exit transition → destroy instance, release prefab, pop backdrop → return the result.</para>
    /// <para>Main-thread only. <see cref="Dispose"/> cancels all in-flight shows and tears down the layer.</para>
    /// </remarks>
    public sealed class PopupManager : IPopupService
    {
        private sealed class Entry
        {
            public IPopup Popup;
            public GameObject Instance;
            public PopupBackdrop Backdrop;
            public PopupOptions Options;
        }

        private readonly IPopupLoader _loader;
        private readonly IPopupTransition _transition;
        private readonly PopupRegistry _registry;
        private readonly PopupOptions _defaultOptions;
        private readonly int _sortOrder;
        private readonly Func<Transform> _layerFactory;
        private readonly Func<float, CancellationToken, UniTask> _autoDismissTimer;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private Transform _layer;
        private bool _disposed;

        /// <param name="loader">Prefab loader; defaults to <see cref="CompositePopupLoader.CreateDefault"/> (Resources + Direct).</param>
        /// <param name="transition">Default transition; defaults to <see cref="InstantPopupTransition"/>.</param>
        /// <param name="registry">Optional id → ref map for the <c>ShowAsync(object id, ...)</c> overload.</param>
        /// <param name="defaultOptions">Default per-show options; defaults to <see cref="PopupOptions.Default"/>.</param>
        /// <param name="sortOrder">Sorting order of the default overlay canvas (ignored when <paramref name="layerFactory"/> is set).</param>
        /// <param name="layerFactory">
        /// Optional factory for the layer popups are parented under, called lazily on the first show. Use it to
        /// supply a canvas configured for your project (e.g. a <c>CanvasScaler</c> with a reference resolution) —
        /// the default layer scales at constant pixel size. The manager <b>owns</b> the returned object and
        /// destroys it on <see cref="Dispose"/>, so return a fresh root, not a shared scene canvas.
        /// </param>
        /// <param name="autoDismissTimer">
        /// Optional timer backing <see cref="PopupOptions.AutoDismissAfter"/>; defaults to
        /// <c>UniTask.Delay</c> with <c>ignoreTimeScale: true</c> so a timed popup still closes while the game
        /// is paused. Inject a custom timer for scaled, server-driven, or test-controlled timing.
        /// </param>
        public PopupManager(
            IPopupLoader loader = null,
            IPopupTransition transition = null,
            PopupRegistry registry = null,
            PopupOptions defaultOptions = null,
            int sortOrder = 1000,
            Func<Transform> layerFactory = null,
            Func<float, CancellationToken, UniTask> autoDismissTimer = null)
        {
            _loader = loader ?? CompositePopupLoader.CreateDefault();
            _transition = transition ?? new InstantPopupTransition();
            _registry = registry;
            _defaultOptions = defaultOptions ?? PopupOptions.Default;
            _sortOrder = sortOrder;
            _layerFactory = layerFactory;
            _autoDismissTimer = autoDismissTimer ?? DefaultAutoDismissTimer;
        }

        private static UniTask DefaultAutoDismissTimer(float seconds, CancellationToken ct) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct);

        /// <inheritdoc/>
        public int OpenCount => _entries.Count;

        /// <inheritdoc/>
        public UniTask<TResult> ShowAsync<TResult>(object id, object arg = null, PopupOptions options = null, CancellationToken ct = default)
        {
            if (_registry == null)
                throw new InvalidOperationException(
                    "No PopupRegistry configured. Construct PopupManager with a registry, or call ShowAsync with a PopupRef directly.");
            return ShowAsync<TResult>(_registry.Get(id), arg, options, ct);
        }

        /// <inheritdoc/>
        public async UniTask<TResult> ShowAsync<TResult>(PopupRef reference, object arg = null, PopupOptions options = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            options ??= _defaultOptions;
            var layer = EnsureLayer();

            // One linked token (caller + manager lifetime) threads the whole pipeline, so Dispose cancels even an
            // in-flight load. Created before the load so a load failure still disposes it (via using); ReleasePrefab
            // is deliberately left to the post-load finally so we only release what we actually loaded.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);

            var prefab = await _loader.LoadPrefabAsync(reference, linked.Token);
            if (prefab == null)
                throw new InvalidOperationException($"IPopupLoader returned a null prefab for {reference}.");

            // instance/backdrop are tracked as locals (not via entry) so the finally tears them down even if
            // Instantiate throws before the entry is registered — otherwise a raised backdrop would be orphaned.
            Entry entry = null;
            GameObject instance = null;
            PopupBackdrop backdrop = null;
            CancellationTokenSource autoDismissCts = null;
            object result = null;
            try
            {
                backdrop = PopupBackdrop.Create(layer, options.BackdropColor);
                instance = UnityEngine.Object.Instantiate(prefab, layer);
                instance.transform.SetAsLastSibling(); // popup sits above its backdrop

                var popup = instance.GetComponentInChildren<IPopup>(true);
                if (popup == null)
                    throw new InvalidOperationException(
                        $"Popup prefab for {reference} has no component implementing IPopup on its hierarchy.");

                entry = new Entry { Popup = popup, Instance = instance, Backdrop = backdrop, Options = options };
                _entries.Add(entry);

                if (options.DismissOnBackdropClick)
                    backdrop.OnClicked = () => popup.TryDismiss();

                popup.Root.SetActive(true);
                popup.OnOpened(arg);

                var transition = options.Transition ?? _transition;
                await transition.PlayEnterAsync(popup, linked.Token);

                if (options.AutoDismissAfter > 0f)
                {
                    autoDismissCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    RunAutoDismissAsync(popup, options.AutoDismissAfter, autoDismissCts.Token).Forget();
                }

                result = await popup.Result.AttachExternalCancellation(linked.Token);
                popup.OnClosing();
                await transition.PlayExitAsync(popup, linked.Token);
            }
            finally
            {
                autoDismissCts?.Cancel();
                autoDismissCts?.Dispose();
                if (entry != null) _entries.Remove(entry);
                DestroyObject(instance);
                if (backdrop != null) DestroyObject(backdrop.gameObject);
                _loader.ReleasePrefab(reference, prefab);
            }

            return CastResult<TResult>(reference, result);
        }

        // Fires PopupOptions.AutoDismissAfter. Routes through TryDismiss so a vetoing popup stays open and the
        // close result matches whatever Back/backdrop would produce. Cancellation (popup closed first, or the
        // manager was disposed) is expected and silently swallowed.
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
        // which popup misbehaved; name the ref and both types so the mismatch is diagnosable from the exception.
        private static TResult CastResult<TResult>(in PopupRef reference, object result)
        {
            if (result is TResult typed) return typed;
            if (result == null && default(TResult) == null) return default;
            throw new InvalidCastException(
                $"Popup {reference} completed with {(result == null ? "null" : result.GetType().Name)}, " +
                $"which is not the awaited result type {typeof(TResult).Name}.");
        }

        /// <inheritdoc/>
        public bool TryHandleBack()
        {
            if (_entries.Count == 0) return false;
            var top = _entries[_entries.Count - 1];
            if (top.Options.DismissOnBack)
                top.Popup.TryDismiss();
            return true; // a popup was open: consume the back press regardless
        }

        /// <inheritdoc/>
        public void CloseAll()
        {
            // Snapshot: each TryDismiss resumes an awaiting ShowAsync that mutates _entries.
            var snapshot = _entries.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
                snapshot[i].Popup.TryDismiss();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Cancelling the lifetime token resumes every in-flight ShowAsync via cancellation, whose finally
            // blocks remove their entries and destroy their instances.
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            // Anything left (popups parked on the result await with nothing observing the throw) is cleaned here.
            foreach (var entry in _entries)
            {
                if (entry.Instance != null) DestroyObject(entry.Instance);
                if (entry.Backdrop != null) DestroyObject(entry.Backdrop.gameObject);
            }
            _entries.Clear();

            if (_layer != null)
            {
                DestroyObject(_layer.gameObject);
                _layer = null;
            }
        }

        // Popups live at runtime, but the EditMode test suite drives the manager outside play mode where
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
                        "The layerFactory returned null; it must return the transform popups are parented under.");
                return _layer;
            }

            var go = new GameObject("[PopupLayer]");
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
            if (_disposed) throw new ObjectDisposedException(nameof(PopupManager));
        }
    }
}
