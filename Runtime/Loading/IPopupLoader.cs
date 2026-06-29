using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Resolves a <see cref="PopupRef"/> to a prefab and releases it later. The manager instantiates a fresh
    /// instance from the returned prefab per show, so a loader returns (and caches) the <i>prefab</i>, not an instance.
    /// </summary>
    public interface IPopupLoader
    {
        /// <summary>
        /// Loads the prefab identified by <paramref name="reference"/>. Implementations may cache; the manager
        /// calls <see cref="ReleasePrefab"/> once per show when the popup closes.
        /// </summary>
        UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct);

        /// <summary>
        /// Signals the manager is done with one show of <paramref name="reference"/>. Reference-counting / caching
        /// loaders decrement here; a cache-forever loader makes this a no-op.
        /// </summary>
        void ReleasePrefab(PopupRef reference, GameObject prefab);
    }
}
