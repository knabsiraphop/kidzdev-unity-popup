using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Fades a popup in/out using a <see cref="CanvasGroup"/> on its root (added on demand), with no
    /// third-party animation dependency — a bare per-frame alpha lerp on unscaled time.
    /// </summary>
    public sealed class FadePopupTransition : IPopupTransition
    {
        private readonly float _duration;

        /// <param name="duration">Fade duration in seconds. Values &lt;= 0 make the transition instant.</param>
        public FadePopupTransition(float duration = 0.15f)
        {
            _duration = duration;
        }

        /// <inheritdoc/>
        public UniTask PlayEnterAsync(IPopup popup, CancellationToken ct) => FadeAsync(popup, 0f, 1f, ct);

        /// <inheritdoc/>
        public UniTask PlayExitAsync(IPopup popup, CancellationToken ct) => FadeAsync(popup, 1f, 0f, ct);

        private async UniTask FadeAsync(IPopup popup, float from, float to, CancellationToken ct)
        {
            var cg = GetCanvasGroup(popup);
            if (cg == null) return;

            if (_duration <= 0f)
            {
                cg.alpha = to;
                return;
            }

            cg.alpha = from;
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / _duration));
                await UniTask.NextFrame(ct);
            }
            cg.alpha = to;
        }

        private static CanvasGroup GetCanvasGroup(IPopup popup)
        {
            if (popup?.Root == null) return null;
            var cg = popup.Root.GetComponent<CanvasGroup>();
            if (cg == null) cg = popup.Root.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}
