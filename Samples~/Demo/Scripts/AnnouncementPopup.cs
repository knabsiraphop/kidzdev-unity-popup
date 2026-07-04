using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup.Samples
{
    /// <summary>
    /// A timed announcement popup. It renders its own "Closing in Ns…" countdown, but does not close itself —
    /// <see cref="PopupOptions.AutoDismissAfter"/> enforces the actual deadline (this is the render/enforce
    /// split: the popup only reflects the state, the manager owns the timeout). Tapping "Got it" closes early
    /// with <see cref="PopupResult.Confirmed"/>; a timed close (or Back/backdrop) resolves
    /// <see cref="PopupResult.Dismissed"/>.
    /// </summary>
    public sealed class AnnouncementPopup : Popup
    {
        [Header("Labels (uGUI Text)")]
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _messageLabel;
        [SerializeField] private Text _countdownLabel;

        private CancellationTokenSource _countdownCts;

        /// <inheritdoc/>
        protected override object DismissResult => PopupResult.Dismissed;

        /// <inheritdoc/>
        public override void OnOpened(object arg)
        {
            if (arg is Content content)
            {
                if (_titleLabel != null) _titleLabel.text = content.Title;
                if (_messageLabel != null) _messageLabel.text = content.Body;
                RunCountdown(content.Seconds).Forget();
            }
        }

        /// <inheritdoc/>
        public override void OnClosing()
        {
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = null;
        }

        /// <summary>Closes with <see cref="PopupResult.Confirmed"/>. Wire to the "Got it" button.</summary>
        public void Ok() => Close(PopupResult.Confirmed);

        // Purely cosmetic countdown label; cancelled from OnClosing whenever the popup closes (button, timer, or
        // Back/backdrop dismissal). Uses unscaled time so it stays in sync with the manager's ignoreTimeScale timer.
        private async UniTaskVoid RunCountdown(float seconds)
        {
            _countdownCts = new CancellationTokenSource();
            var ct = _countdownCts.Token;
            if (_countdownLabel == null) return;

            var remaining = seconds;
            while (remaining > 0f)
            {
                _countdownLabel.text = $"Closing in {Mathf.CeilToInt(remaining)}…";
                await UniTask.Delay(System.TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: ct)
                    .SuppressCancellationThrow();
                if (ct.IsCancellationRequested) return;
                remaining -= 1f;
            }
            _countdownLabel.text = "Closing…";
        }

        /// <summary>Title + body + the auto-dismiss duration (seconds) shown by the countdown label.</summary>
        public readonly struct Content
        {
            public readonly string Title;
            public readonly string Body;
            public readonly float Seconds;
            public Content(string title, string body, float seconds) { Title = title; Body = body; Seconds = seconds; }
        }
    }
}
