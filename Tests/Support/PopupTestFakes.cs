using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup.Tests
{
    // These fakes live in a NON-editor assembly (KidzDev.Unity.Popup.TestSupport) on purpose: FakePopup is a
    // MonoBehaviour, and Unity refuses to AddComponent a MonoBehaviour that is compiled into an editor-only
    // assembly. Both the EditMode and PlayMode test assemblies reference this one, so they share the fakes.

    /// <summary>
    /// An <see cref="IPopup"/> backed by a real GameObject, used as a fake "prefab" the fake loader returns.
    /// Every instantiated copy registers itself in <see cref="Live"/> on <c>OnOpened</c> so a test can grab the
    /// live instance and drive its result.
    /// </summary>
    public sealed class FakePopup : MonoBehaviour, IPopup
    {
        public static readonly List<FakePopup> Live = new List<FakePopup>();

        private UniTaskCompletionSource<object> _completion;
        private UniTaskCompletionSource<object> Completion => _completion ??= new UniTaskCompletionSource<object>();

        public int OpenedCount;
        public int ClosingCount;
        public object LastArg;
        public bool VetoDismiss;

        public GameObject Root => gameObject;
        public UniTask<object> Result => Completion.Task;

        public void OnOpened(object arg)
        {
            OpenedCount++;
            LastArg = arg;
            Live.Add(this);
        }

        public void OnClosing()
        {
            ClosingCount++;
            Live.Remove(this);
        }

        public bool TryDismiss()
        {
            if (VetoDismiss) return false;
            Completion.TrySetResult(PopupResult.Cancelled);
            return true;
        }

        /// <summary>Closes this popup with <paramref name="result"/> (what a button would do).</summary>
        public void CloseWith(object result) => Completion.TrySetResult(result);

        /// <summary>Builds a fake "prefab" GameObject carrying a <see cref="FakePopup"/>.</summary>
        public static GameObject CreatePrefab(string name = "FakePopup")
        {
            var go = new GameObject(name);
            go.AddComponent<FakePopup>();
            go.SetActive(false);
            return go;
        }

        public static void ResetLive() => Live.Clear();
    }

    /// <summary>
    /// An <see cref="IPopupLoader"/> that always returns the same fake prefab and counts load/release calls.
    /// </summary>
    public sealed class FakeLoader : IPopupLoader
    {
        private readonly GameObject _prefab;
        public int LoadCount;
        public int ReleaseCount;

        /// <summary>When set, <see cref="LoadPrefabAsync"/> awaits this (honoring the token) before returning.</summary>
        public UniTaskCompletionSource LoadGate;

        public FakeLoader(GameObject prefab) => _prefab = prefab;

        public async UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
        {
            LoadCount++;
            if (LoadGate != null) await LoadGate.Task.AttachExternalCancellation(ct);
            return _prefab;
        }

        public void ReleasePrefab(PopupRef reference, GameObject prefab) => ReleaseCount++;
    }

    /// <summary>
    /// An <see cref="IPopupTransition"/> whose completion the test controls. Each call returns a pending task
    /// until <see cref="Open"/>; thereafter calls complete synchronously. No frame pumping, so it works in
    /// EditMode <c>[Test]</c> methods.
    /// </summary>
    public sealed class GatedPopupTransition : IPopupTransition
    {
        public int EnterCalls;
        public int ExitCalls;
        public bool Open = true;
        private readonly List<UniTaskCompletionSource> _pending = new List<UniTaskCompletionSource>();

        public UniTask PlayEnterAsync(IPopup popup, CancellationToken ct)
        {
            EnterCalls++;
            return Gate();
        }

        public UniTask PlayExitAsync(IPopup popup, CancellationToken ct)
        {
            ExitCalls++;
            return Gate();
        }

        private UniTask Gate()
        {
            if (Open) return UniTask.CompletedTask;
            var tcs = new UniTaskCompletionSource();
            _pending.Add(tcs);
            return tcs.Task;
        }

        public void Release()
        {
            Open = true;
            var copy = _pending.ToArray();
            _pending.Clear();
            foreach (var t in copy) t.TrySetResult();
        }
    }

    /// <summary>An <see cref="IPopupLoader"/> that records the last ref it was asked to load — for routing tests.</summary>
    public sealed class RecordingLoader : IPopupLoader
    {
        private readonly GameObject _prefab;
        public PopupRef LastReference;
        public int LoadCount;

        public RecordingLoader(GameObject prefab = null) => _prefab = prefab;

        public UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
        {
            LoadCount++;
            LastReference = reference;
            return UniTask.FromResult(_prefab);
        }

        public void ReleasePrefab(PopupRef reference, GameObject prefab) { }
    }
}
