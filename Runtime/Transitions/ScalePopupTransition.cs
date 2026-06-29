using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Pop-in transition: lerps the popup root's local scale (and a <see cref="CanvasGroup"/> alpha, added on
    /// demand) from a smaller start to full on enter, and reverses on exit. No third-party animation dependency.
    /// </summary>
    public sealed class ScalePopupTransition : IPopupTransition
    {
        private readonly float _duration;
        private readonly float _fromScale;

        /// <param name="duration">Animation duration in seconds. Values &lt;= 0 make the transition instant.</param>
        /// <param name="fromScale">The scale the popup grows from on enter (and shrinks to on exit).</param>
        public ScalePopupTransition(float duration = 0.15f, float fromScale = 0.8f)
        {
            _duration = duration;
            _fromScale = fromScale;
        }

        /// <inheritdoc/>
        public UniTask PlayEnterAsync(IPopup popup, CancellationToken ct)
            => AnimateAsync(popup, _fromScale, 1f, 0f, 1f, ct);

        /// <inheritdoc/>
        public UniTask PlayExitAsync(IPopup popup, CancellationToken ct)
            => AnimateAsync(popup, 1f, _fromScale, 1f, 0f, ct);

        private async UniTask AnimateAsync(IPopup popup, float scaleFrom, float scaleTo, float alphaFrom, float alphaTo, CancellationToken ct)
        {
            if (popup?.Root == null) return;
            var tr = popup.Root.transform;
            var cg = popup.Root.GetComponent<CanvasGroup>();
            if (cg == null) cg = popup.Root.AddComponent<CanvasGroup>();

            if (_duration <= 0f)
            {
                tr.localScale = Vector3.one * scaleTo;
                cg.alpha = alphaTo;
                return;
            }

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / _duration);
                tr.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, k);
                cg.alpha = Mathf.Lerp(alphaFrom, alphaTo, k);
                await UniTask.NextFrame(ct);
            }
            tr.localScale = Vector3.one * scaleTo;
            cg.alpha = alphaTo;
        }
    }
}
