using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// A capped, queued group of concurrently-visible popups (e.g. "you got 5 rewards"), as opposed to
    /// <see cref="IPopupService"/>'s modal LIFO where only the top popup is ever interactive. Substitute an
    /// implementation via <see cref="PopupStack.Default"/>; the default is <see cref="PopupStackController"/>.
    /// </summary>
    public interface IPopupStack : IDisposable
    {
        /// <summary>The number of cards currently shown (loaded, instantiated, positioned per the layout).</summary>
        int VisibleCount { get; }

        /// <summary>The number of cards waiting for a slot to free before they're even loaded.</summary>
        int QueuedCount { get; }

        /// <summary>
        /// Loads, shows (or queues), and awaits a card identified by <paramref name="reference"/>; returns the
        /// value it closes with, cast to <typeparamref name="TResult"/>. If the stack is already at capacity the
        /// card is not loaded or instantiated until a slot frees, so its <c>OnOpened</c> never runs early.
        /// </summary>
        /// <param name="autoDismissAfter">
        /// Seconds after which this card auto-closes (as if dismissed), once it's actually shown — not while
        /// still queued, and not until its enter transition finishes. <c>0</c> or less disables it. Routes
        /// through <see cref="IPopup.TryDismiss"/>, so a card that vetoes dismissal is not auto-closed.
        /// </param>
        UniTask<TResult> Enqueue<TResult>(PopupRef reference, object arg = null, float autoDismissAfter = 0f, CancellationToken ct = default);

        /// <summary>Dismisses every visible card and cancels every card still queued (never instantiated).</summary>
        void ClearAll();
    }
}
