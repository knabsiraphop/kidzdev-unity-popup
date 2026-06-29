using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// A ready-made acknowledgement dialog: a message and a single OK button. Completes with a
    /// <see cref="PopupResult"/> (<see cref="PopupResult.Confirmed"/> on OK, <see cref="PopupResult.Dismissed"/>
    /// on Back / backdrop). Wire the OK button to <see cref="Ok"/> in the inspector.
    /// </summary>
    public class AlertPopup : Popup
    {
        [Header("Optional labels (uGUI Text)")]
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _messageLabel;

        /// <inheritdoc/>
        public override void OnOpened(object arg)
        {
            switch (arg)
            {
                case string message when _messageLabel != null:
                    _messageLabel.text = message;
                    break;
                case ConfirmPopup.Content content:
                    if (_titleLabel != null && content.Title != null) _titleLabel.text = content.Title;
                    if (_messageLabel != null && content.Message != null) _messageLabel.text = content.Message;
                    break;
            }
        }

        /// <inheritdoc/>
        protected override object DismissResult => PopupResult.Dismissed;

        /// <summary>Closes with <see cref="PopupResult.Confirmed"/>. Wire to the OK button.</summary>
        public void Ok() => Close(PopupResult.Confirmed);
    }
}
