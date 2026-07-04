using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup.Samples
{
    /// <summary>
    /// A reward card for the <see cref="PopupStack"/> demo: a label and a "Collect" button. It has no idea
    /// it's in a stack — arrangement, capacity, and interactivity gating are entirely the
    /// <see cref="PopupStackController"/> + <see cref="IStackLayout"/>'s concern, not the card's.
    /// </summary>
    /// <remarks>
    /// Accepts either a plain <see cref="string"/> label (untimed) or a <see cref="Content"/> (label + a
    /// countdown to render). Either way the card only <i>displays</i> the countdown — the actual auto-close
    /// deadline is enforced by <c>PopupStack.Enqueue</c>'s <c>autoDismissAfter</c>, the same render/enforce
    /// split as the popup package's <c>AnnouncementPopup</c>.
    /// </remarks>
    public sealed class RewardCardPopup : Popup
    {
        [Header("Optional label (uGUI Text)")]
        [SerializeField] private Text _label;
        [Header("Optional countdown (uGUI Text) — only used when opened with a Content")]
        [SerializeField] private Text _countdownLabel;

        private CancellationTokenSource _countdownCts;

        /// <inheritdoc/>
        protected override object DismissResult => PopupResult.Dismissed;

        /// <inheritdoc/>
        public override void OnOpened(object arg)
        {
            switch (arg)
            {
                case string text:
                    if (_label != null) _label.text = text;
                    break;
                case Content content:
                    if (_label != null) _label.text = content.Label;
                    RunCountdown(content.Seconds).Forget();
                    break;
            }
        }

        /// <inheritdoc/>
        public override void OnClosing()
        {
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = null;
        }

        /// <summary>Closes with <see cref="PopupResult.Confirmed"/>. Wire to the "Collect" button.</summary>
        public void Collect() => Close(PopupResult.Confirmed);

        // Purely cosmetic; cancelled from OnClosing whenever the card closes (Collect or the stack's
        // auto-dismiss timer). Uses unscaled time so it stays in sync with the stack's ignoreTimeScale timer.
        private async UniTaskVoid RunCountdown(float seconds)
        {
            _countdownCts = new CancellationTokenSource();
            var ct = _countdownCts.Token;
            if (_countdownLabel == null) return;

            var remaining = seconds;
            while (remaining > 0f)
            {
                _countdownLabel.text = $"{Mathf.CeilToInt(remaining)}s";
                await UniTask.Delay(System.TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: ct)
                    .SuppressCancellationThrow();
                if (ct.IsCancellationRequested) return;
                remaining -= 1f;
            }
            _countdownLabel.text = string.Empty;
        }

        /// <summary>A reward's label plus the auto-dismiss duration (seconds) shown by the countdown label.</summary>
        public readonly struct Content
        {
            public readonly string Label;
            public readonly float Seconds;
            public Content(string label, float seconds) { Label = label; Seconds = seconds; }
        }
    }
}
