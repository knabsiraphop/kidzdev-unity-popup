using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Identifies <i>which</i> popup to show and <i>where it loads from</i>. The pairing of
    /// <see cref="Source"/> + <see cref="Location"/>/<see cref="Prefab"/> is what lets a single app mix
    /// loaders per popup, chosen by situation (see <see cref="CompositePopupLoader"/>).
    /// </summary>
    /// <remarks>
    /// Construct via the factory helpers rather than the raw constructor:
    /// <code>
    /// PopupRef.Resources("Popups/Confirm")   // shipped, instant
    /// PopupRef.Direct(myPrefab)              // inspector-assigned, no IO
    /// PopupRef.Addressables("event_popup")   // downloadable / patchable
    /// </code>
    /// </remarks>
    public readonly struct PopupRef
    {
        /// <summary>How the prefab is loaded.</summary>
        public readonly PopupSource Source;

        /// <summary>Resources path or Addressables key (unused for <see cref="PopupSource.Direct"/>).</summary>
        public readonly string Location;

        /// <summary>Inspector-assigned prefab for <see cref="PopupSource.Direct"/> (otherwise <c>null</c>).</summary>
        public readonly GameObject Prefab;

        /// <summary>Prefer the static factory helpers; this exists for advanced/custom sources.</summary>
        public PopupRef(PopupSource source, string location, GameObject prefab)
        {
            Source = source;
            Location = location;
            Prefab = prefab;
        }

        /// <summary>A popup loaded from a <c>Resources</c> folder by <paramref name="path"/>.</summary>
        public static PopupRef Resources(string path) => new PopupRef(PopupSource.Resources, path, null);

        /// <summary>A popup loaded via Unity Addressables by <paramref name="key"/>.</summary>
        public static PopupRef Addressables(string key) => new PopupRef(PopupSource.Addressables, key, null);

        /// <summary>A popup that uses the inspector-assigned <paramref name="prefab"/> directly (no IO).</summary>
        public static PopupRef Direct(GameObject prefab) => new PopupRef(PopupSource.Direct, null, prefab);

        /// <summary><c>true</c> when this ref has enough information for its source to load a prefab.</summary>
        public bool IsValid => Source == PopupSource.Direct
            ? Prefab != null
            : !string.IsNullOrEmpty(Location);

        /// <inheritdoc/>
        public override string ToString() => Source == PopupSource.Direct
            ? $"PopupRef(Direct, {(Prefab != null ? Prefab.name : "null")})"
            : $"PopupRef({Source}, \"{Location}\")";
    }
}
