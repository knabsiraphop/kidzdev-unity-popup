using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Cards peek behind each other with a depth offset; only the frontmost (index 0 — the oldest/first-shown
    /// card) is interactive. Collecting it slides the rest forward. No third-party animation dependency — a
    /// bare per-frame lerp on unscaled time, the same style as the built-in <see cref="IPopupTransition"/>s.
    /// </summary>
    public sealed class DeckStackLayout : IStackLayout
    {
        private readonly Vector2 _offsetPerDepth;
        private readonly float _scalePerDepth;
        private readonly float _slideDuration;

        /// <param name="maxVisible">How many cards peek at once.</param>
        /// <param name="offsetPerDepth">
        /// Anchored-position offset per depth step (the card at depth 1 sits here, depth 2 at 2x, ...).
        /// Defaults to a small downward peek.
        /// </param>
        /// <param name="scalePerDepth">Uniform scale multiplier per depth step (depth 1 at this, depth 2 at this^2, ...).</param>
        /// <param name="slideDuration">Seconds to animate a reflow. Values &lt;= 0 snap instantly.</param>
        public DeckStackLayout(int maxVisible = 3, Vector2? offsetPerDepth = null, float scalePerDepth = 0.94f, float slideDuration = 0.15f)
        {
            MaxVisible = maxVisible;
            _offsetPerDepth = offsetPerDepth ?? new Vector2(0f, -24f);
            _scalePerDepth = scalePerDepth;
            _slideDuration = slideDuration;
        }

        /// <inheritdoc/>
        public int MaxVisible { get; }

        /// <inheritdoc/>
        public bool IsInteractable(int index, int visibleCount) => index == 0;

        /// <summary>Cards stay in the stack's own private overlay layer, so the default backdrop placement there is already correct.</summary>
        public Transform BackdropAnchor => null;

        /// <inheritdoc/>
        public async UniTask ArrangeAsync(IReadOnlyList<RectTransform> visibleCards, CancellationToken ct)
        {
            // Later siblings render on top; walking back-to-front leaves index 0 (front) as the last sibling.
            for (int i = visibleCards.Count - 1; i >= 0; i--)
                visibleCards[i].SetAsLastSibling();

            var targetPos = new Vector2[visibleCards.Count];
            var targetScale = new float[visibleCards.Count];
            var startPos = new Vector2[visibleCards.Count];
            var startScale = new float[visibleCards.Count];
            for (int i = 0; i < visibleCards.Count; i++)
            {
                targetPos[i] = _offsetPerDepth * i;
                targetScale[i] = Mathf.Pow(_scalePerDepth, i);
                startPos[i] = visibleCards[i].anchoredPosition;
                startScale[i] = visibleCards[i].localScale.x;
            }

            if (_slideDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < _slideDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(elapsed / _slideDuration);
                    for (int i = 0; i < visibleCards.Count; i++)
                    {
                        visibleCards[i].anchoredPosition = Vector2.Lerp(startPos[i], targetPos[i], k);
                        visibleCards[i].localScale = Vector3.one * Mathf.Lerp(startScale[i], targetScale[i], k);
                    }
                    await UniTask.NextFrame(ct);
                }
            }

            for (int i = 0; i < visibleCards.Count; i++)
            {
                visibleCards[i].anchoredPosition = targetPos[i];
                visibleCards[i].localScale = Vector3.one * targetScale[i];
            }
        }
    }
}
