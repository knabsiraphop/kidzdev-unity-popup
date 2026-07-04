using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Per-show appearance/behavior knobs. The manager holds a default set; pass an instance to
    /// <c>ShowAsync</c> to override for a single popup.
    /// </summary>
    public sealed class PopupOptions
    {
        /// <summary>Backdrop tint (a dim scrim behind the popup). Alpha drives how much the scene is darkened.</summary>
        public Color BackdropColor = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>When <c>true</c>, tapping the backdrop dismisses the popup (subject to <see cref="IPopup.TryDismiss"/>).</summary>
        public bool DismissOnBackdropClick = false;

        /// <summary>When <c>true</c>, a back request (<see cref="IPopupService.TryHandleBack"/>) dismisses the top popup.</summary>
        public bool DismissOnBack = true;

        /// <summary>Optional per-show transition override; <c>null</c> uses the manager's default transition.</summary>
        public IPopupTransition Transition = null;

        /// <summary>
        /// Seconds after which the popup auto-closes (as if dismissed). <c>0</c> or less disables it.
        /// The countdown starts after the enter transition and is cancelled if the popup closes first.
        /// Auto-close routes through <see cref="IPopup.TryDismiss"/>, so a vetoing popup is not auto-closed
        /// and the close result is the popup's <c>DismissResult</c>.
        /// </summary>
        public float AutoDismissAfter = 0f;

        /// <summary>A fresh default-valued options instance.</summary>
        public static PopupOptions Default => new PopupOptions();
    }
}
