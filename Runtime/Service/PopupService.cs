namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Static facade over <see cref="IPopupService"/>, mirroring the package's facade + seam + default-impl pattern.
    /// <see cref="Default"/> lazily creates a <see cref="PopupManager"/>; assign it to inject a configured manager
    /// (custom loaders, registry, transition) or a test double.
    /// </summary>
    /// <example>
    /// <code>
    /// bool ok = await PopupService.Default.ShowAsync&lt;bool&gt;(PopupRef.Resources("Popups/Confirm"), "Delete?");
    /// </code>
    /// </example>
    public static class PopupService
    {
        private static IPopupService _default;

        /// <summary>The ambient popup service. Lazily a default <see cref="PopupManager"/>; settable for injection.</summary>
        public static IPopupService Default
        {
            get => _default ??= new PopupManager();
            set => _default = value;
        }
    }
}
