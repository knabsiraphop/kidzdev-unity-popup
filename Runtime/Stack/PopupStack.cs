using System;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Static facade over <see cref="IPopupStack"/>, mirroring <see cref="PopupService"/>'s pattern. There is no
    /// ready-made default here: unlike a transition (where "no animation" is a reasonable default), the choice
    /// between a <see cref="DeckStackLayout"/> and a <see cref="GroupStackLayout"/> is a real design decision,
    /// so <see cref="Default"/> must be assigned before use.
    /// </summary>
    /// <example>
    /// <code>
    /// PopupStack.Default = new PopupStackController(new DeckStackLayout(maxVisible: 3));
    /// var result = await PopupStack.Default.Enqueue&lt;PopupResult&gt;(PopupRef.Resources("Popups/Reward"));
    /// </code>
    /// </example>
    public static class PopupStack
    {
        private static IPopupStack _default;

        /// <summary>The ambient reward stack. No default value — assign a configured <see cref="PopupStackController"/> before use.</summary>
        public static IPopupStack Default
        {
            get => _default ?? throw new InvalidOperationException(
                "PopupStack.Default has not been assigned. Construct a PopupStackController with an " +
                "IStackLayout (DeckStackLayout or GroupStackLayout) and assign it here first.");
            set => _default = value;
        }
    }
}
