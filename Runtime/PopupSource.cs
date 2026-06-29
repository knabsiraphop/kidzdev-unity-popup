namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Where a popup prefab is loaded from. A <see cref="PopupRef"/> carries one of these, and the
    /// <see cref="CompositePopupLoader"/> routes each show to the matching <see cref="IPopupLoader"/>.
    /// This is what lets one app mix "some Resources, some Addressables, some inspector-assigned" popups.
    /// </summary>
    public enum PopupSource
    {
        /// <summary>Load from a <c>Resources</c> folder by path. Zero setup, shipped in the build.</summary>
        Resources,

        /// <summary>Load via Unity Addressables by key. Downloadable / patchable; loader lives in <c>Samples~</c>.</summary>
        Addressables,

        /// <summary>Use an inspector-assigned prefab carried on the <see cref="PopupRef"/>. No IO at all.</summary>
        Direct,
    }
}
