using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The animation seam for popups. Swap one implementation to restyle every popup open/close in the app.
    /// </summary>
    /// <remarks>
    /// Contract: the manager activates the popup root before calling <see cref="PlayEnterAsync"/> and destroys
    /// the instance after <see cref="PlayExitAsync"/> returns. Implementations only animate visual properties
    /// (alpha, scale) and must honor <c>ct</c> — on cancellation the exception propagates and the manager tears
    /// the popup down. The built-ins require no third-party animation package.
    /// </remarks>
    public interface IPopupTransition
    {
        /// <summary>Animates the popup in. Called after the root is activated and <c>OnOpened</c> has run.</summary>
        UniTask PlayEnterAsync(IPopup popup, CancellationToken ct);

        /// <summary>Animates the popup out. Called after the result resolves and <c>OnClosing</c> has run.</summary>
        UniTask PlayExitAsync(IPopup popup, CancellationToken ct);
    }
}
