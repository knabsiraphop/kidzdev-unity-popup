using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KidzDev.Unity.Popup.Samples
{
    /// <summary>
    /// An <see cref="IPopupLoader"/> backed by Unity Addressables. Lives in <c>Samples~</c> so the core package
    /// keeps Addressables an <b>optional</b> dependency — import this only if you load popups via Addressables.
    /// </summary>
    /// <remarks>
    /// Each Addressables key loads exactly one handle; concurrent first-loads for the same key share one
    /// in-flight request, and the handle is released on error. Wire it into the manager's loader:
    /// <code>
    /// var loader = new CompositePopupLoader()
    ///     .With(PopupSource.Resources, new ResourcesPopupLoader())
    ///     .With(PopupSource.Direct,    new DirectPopupLoader())
    ///     .With(PopupSource.Addressables, new AddressablesPopupLoader());
    /// PopupService.Default = new PopupManager(loader);
    /// </code>
    /// If your project already uses the KidzDev Addressables Toolkit, you can instead delegate to its
    /// <c>AssetLoader</c>/<c>AssetScope</c> for ref-counting and scoped lifetime.
    /// </remarks>
    public sealed class AddressablesPopupLoader : IPopupLoader
    {
        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _handles =
            new Dictionary<string, AsyncOperationHandle<GameObject>>();
        private readonly Dictionary<string, UniTaskCompletionSource<GameObject>> _inflight =
            new Dictionary<string, UniTaskCompletionSource<GameObject>>();

        /// <inheritdoc/>
        public async UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
        {
            if (reference.Source != PopupSource.Addressables)
                throw new InvalidOperationException(
                    $"{nameof(AddressablesPopupLoader)} only handles PopupSource.Addressables, got {reference.Source}.");

            var key = reference.Location;
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("PopupRef.Location (an Addressables key) is required.", nameof(reference));

            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            if (_inflight.TryGetValue(key, out var pending)) return await pending.Task.AttachExternalCancellation(ct);

            var tcs = new UniTaskCompletionSource<GameObject>();
            _inflight[key] = tcs;
            try
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(key);
                GameObject prefab;
                try
                {
                    // Await the load, then read the typed result off the handle. Reading handle.Result avoids
                    // binding to the non-generic AsyncOperationHandle.ToUniTask() overload (which yields void).
                    await handle.ToUniTask(cancellationToken: ct);
                    prefab = handle.Result;
                }
                catch
                {
                    Addressables.Release(handle);
                    throw;
                }

                if (prefab == null)
                {
                    Addressables.Release(handle);
                    throw new InvalidOperationException($"Addressables popup prefab not found for key '{key}'.");
                }

                _cache[key] = prefab;
                _handles[key] = handle;
                _inflight.Remove(key);
                tcs.TrySetResult(prefab);
                return prefab;
            }
            catch (OperationCanceledException)
            {
                _inflight.Remove(key);
                tcs.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                _inflight.Remove(key);
                tcs.TrySetException(ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public void ReleasePrefab(PopupRef reference, GameObject prefab)
        {
            // Handles are cached and shared across shows; release everything via ReleaseAll on teardown.
        }

        /// <summary>Releases every cached Addressables handle. Call when the popup system is torn down.</summary>
        public void ReleaseAll()
        {
            foreach (var key in new List<string>(_cache.Keys))
            {
                if (_handles.TryGetValue(key, out var handle))
                {
                    _handles.Remove(key);
                    Addressables.Release(handle);
                }
            }
            _cache.Clear();
        }
    }
}
