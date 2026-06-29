namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The common button outcomes for a popup. A popup may instead complete with any value
    /// (e.g. <see cref="ConfirmPopup"/> returns a <see cref="bool"/>); this enum is the convenient
    /// default for alerts and cancel-able dialogs that don't carry richer data.
    /// </summary>
    public enum PopupResult
    {
        /// <summary>The user accepted (OK / Yes / primary action).</summary>
        Confirmed,

        /// <summary>The user declined (No / Cancel), or the popup was dismissed by Back / backdrop.</summary>
        Cancelled,

        /// <summary>The popup closed without an explicit choice (e.g. an alert acknowledged).</summary>
        Dismissed,
    }
}
