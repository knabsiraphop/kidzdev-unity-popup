using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// <see cref="IPopupLoader"/> for <see cref="PopupSource.Direct"/>: returns the inspector-assigned prefab
    /// carried on the <see cref="PopupRef"/>. No IO — the fastest source. Release is a no-op (the project owns
    /// the prefab's lifetime).
    /// </summary>
    public sealed class DirectPopupLoader : IPopupLoader
    {
        /// <inheritdoc/>
        public UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
        {
            if (reference.Prefab == null)
                throw new InvalidOperationException(
                    $"PopupSource.Direct requires PopupRef.Prefab to be assigned ({reference}).");
            return UniTask.FromResult(reference.Prefab);
        }

        /// <inheritdoc/>
        public void ReleasePrefab(PopupRef reference, GameObject prefab)
        {
            // The project owns the inspector-assigned prefab; nothing to release.
        }
    }
}
