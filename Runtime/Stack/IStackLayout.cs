using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The visual/interactivity policy for a <see cref="PopupStackController"/>. Swap one implementation to
    /// change how a whole reward stack looks and which cards accept input, without touching card content or
    /// the controller's queueing/capacity logic.
    /// </summary>
    public interface IStackLayout
    {
        /// <summary>The maximum number of cards the layout keeps visible at once.</summary>
        int MaxVisible { get; }

        /// <summary>
        /// Positions/parents/orders the currently visible cards, front (index 0 — the oldest/first-shown card)
        /// to back. May animate; honors <paramref name="ct"/> (cancelled if the stack tears down mid-reflow).
        /// </summary>
        UniTask ArrangeAsync(IReadOnlyList<RectTransform> visibleCards, CancellationToken ct);

        /// <summary>Whether the card at <paramref name="index"/> (0 = frontmost) currently accepts input.</summary>
        bool IsInteractable(int index, int visibleCount);

        /// <summary>
        /// Where <see cref="PopupStackController"/>'s shared backdrop (if configured) should be parented,
        /// positioned as the sibling immediately before it. Return <c>null</c> (the default expectation for a
        /// layout that keeps cards in the stack's own private overlay layer, e.g. <see cref="DeckStackLayout"/>)
        /// to let the controller raise the backdrop in that layer instead.
        /// </summary>
        /// <remarks>
        /// A layout that relocates cards into an externally-owned canvas (e.g. <see cref="GroupStackLayout"/>)
        /// must return that destination — otherwise the backdrop ends up in the controller's own overlay canvas
        /// while the cards render in a different (typically lower-sorted) one, and Unity resolves every click
        /// to whichever canvas sorts higher: the full-screen backdrop wins, silently swallowing clicks meant
        /// for the cards underneath.
        /// </remarks>
        Transform BackdropAnchor { get; }
    }
}
