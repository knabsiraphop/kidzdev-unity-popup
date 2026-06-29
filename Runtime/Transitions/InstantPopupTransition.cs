using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The default transition: no animation. The manager handles activation and teardown, so this has
    /// nothing to do beyond return.
    /// </summary>
    public sealed class InstantPopupTransition : IPopupTransition
    {
        /// <inheritdoc/>
        public UniTask PlayEnterAsync(IPopup popup, CancellationToken ct) => UniTask.CompletedTask;

        /// <inheritdoc/>
        public UniTask PlayExitAsync(IPopup popup, CancellationToken ct) => UniTask.CompletedTask;
    }
}
