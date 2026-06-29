using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Default <see cref="IPopupLoader"/> for <see cref="PopupSource.Resources"/>: loads a prefab from a
    /// <c>Resources</c> folder by path via <see cref="Resources.LoadAsync{T}(string)"/> and caches it by path.
    /// Release is a no-op (the prefab stays cached for the next show).
    /// </summary>
    public sealed class ResourcesPopupLoader : IPopupLoader
    {
        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        /// <inheritdoc/>
        public async UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
        {
            if (reference.Source != PopupSource.Resources)
                throw new InvalidOperationException(
                    $"{nameof(ResourcesPopupLoader)} only handles PopupSource.Resources, got {reference.Source}.");
            if (string.IsNullOrEmpty(reference.Location))
                throw new ArgumentException("PopupRef.Location (a Resources path) is required.", nameof(reference));

            if (_cache.TryGetValue(reference.Location, out var cached) && cached != null)
                return cached;

            var op = Resources.LoadAsync<GameObject>(reference.Location);
            await op.ToUniTask(cancellationToken: ct);

            var prefab = op.asset as GameObject;
            if (prefab == null)
                throw new InvalidOperationException(
                    $"Resources.LoadAsync<GameObject>(\"{reference.Location}\") returned null. " +
                    "Verify the prefab exists inside a Resources folder and the path is correct.");

            _cache[reference.Location] = prefab;
            return prefab;
        }

        /// <inheritdoc/>
        public void ReleasePrefab(PopupRef reference, GameObject prefab)
        {
            // Cached for reuse; nothing to release per show.
        }
    }
}
