using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// A ready-made Yes/No dialog that completes with a <see cref="bool"/> (<c>true</c> = confirmed).
    /// Wire the Yes button to <see cref="Confirm"/> and the No button to <see cref="Cancel"/> in the inspector.
    /// Dismissal (Back / backdrop) resolves to <c>false</c>.
    /// </summary>
    public class ConfirmPopup : Popup
    {
        [Header("Optional labels (uGUI Text)")]
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _messageLabel;

        /// <summary>
        /// Binds incoming text. Pass a <see cref="string"/> for the message, or a <see cref="Content"/> for
        /// title + message.
        /// </summary>
        public override void OnOpened(object arg)
        {
            switch (arg)
            {
                case string message:
                    SetText(null, message);
                    break;
                case Content content:
                    SetText(content.Title, content.Message);
                    break;
            }
        }

        /// <inheritdoc/>
        protected override object DismissResult => false;

        /// <summary>Closes with <c>true</c>. Wire to the confirm/Yes button.</summary>
        public void Confirm() => Close(true);

        /// <summary>Closes with <c>false</c>. Wire to the cancel/No button.</summary>
        public void Cancel() => Close(false);

        private void SetText(string title, string message)
        {
            if (_titleLabel != null && title != null) _titleLabel.text = title;
            if (_messageLabel != null && message != null) _messageLabel.text = message;
        }

        /// <summary>Optional richer argument: title + message.</summary>
        public readonly struct Content
        {
            public readonly string Title;
            public readonly string Message;
            public Content(string title, string message) { Title = title; Message = message; }
        }
    }
}
