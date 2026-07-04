using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Every visible card is independently interactive; a <see cref="LayoutGroup"/> supplied by the caller does
    /// all positioning. Reflow is instant — reparenting/reordering is enough, Unity's layout system handles the
    /// actual placement on its own next layout pass.
    /// </summary>
    public sealed class GroupStackLayout : IStackLayout
    {
        private readonly RectTransform _groupTransform;

        /// <param name="group">The layout group cards are parented under; caller owns its lifetime and sizing.</param>
        /// <param name="maxVisible">How many cards show at once.</param>
        public GroupStackLayout(LayoutGroup group, int maxVisible = 3)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            _groupTransform = (RectTransform)group.transform;
            MaxVisible = maxVisible;
        }

        /// <inheritdoc/>
        public int MaxVisible { get; }

        /// <inheritdoc/>
        public bool IsInteractable(int index, int visibleCount) => true;

        /// <summary>
        /// Cards are relocated into <c>group</c>'s transform, which usually lives in the caller's own scene
        /// canvas, not the stack's private overlay layer — the shared backdrop must be raised there too (as
        /// the sibling immediately before the group) or it ends up in the wrong canvas and can block clicks
        /// meant for the cards.
        /// </summary>
        public Transform BackdropAnchor => _groupTransform;

        /// <inheritdoc/>
        public UniTask ArrangeAsync(IReadOnlyList<RectTransform> visibleCards, CancellationToken ct)
        {
            for (int i = 0; i < visibleCards.Count; i++)
            {
                var card = visibleCards[i];
                if (card.parent != _groupTransform) card.SetParent(_groupTransform, false);
                card.SetSiblingIndex(i);
            }
            return UniTask.CompletedTask;
        }
    }
}
