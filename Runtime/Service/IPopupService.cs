using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The popup service seam. Shows modal dialogs you await for a typed result; popups stack. Substitute an
    /// implementation by assigning <see cref="PopupService.Default"/>; the default is <see cref="PopupManager"/>.
    /// </summary>
    public interface IPopupService : IDisposable
    {
        /// <summary>The number of popups currently open (including stacked ones).</summary>
        int OpenCount { get; }

        /// <summary>
        /// Loads, shows, and awaits a popup identified directly by <paramref name="reference"/>; returns the value
        /// the popup closes with, cast to <typeparamref name="TResult"/>. Honors <paramref name="ct"/> (cancellation
        /// tears the popup down and propagates).
        /// </summary>
        UniTask<TResult> ShowAsync<TResult>(PopupRef reference, object arg = null, PopupOptions options = null, CancellationToken ct = default);

        /// <summary>
        /// As <see cref="ShowAsync{TResult}(PopupRef, object, PopupOptions, CancellationToken)"/>, but resolves
        /// <paramref name="id"/> through the configured <see cref="PopupRegistry"/>. Throws if no registry is set.
        /// </summary>
        UniTask<TResult> ShowAsync<TResult>(object id, object arg = null, PopupOptions options = null, CancellationToken ct = default);

        /// <summary>
        /// Routes a back request (Android back / Esc) to the top popup: dismisses it when its options permit, and
        /// returns <c>true</c> if a popup was open (so the caller stops further back handling). Returns <c>false</c>
        /// when nothing is open — let the navigator handle Back then.
        /// </summary>
        bool TryHandleBack();

        /// <summary>Dismisses every open popup (top-most first). Each awaiting <c>ShowAsync</c> resumes and tears down.</summary>
        void CloseAll();
    }
}
