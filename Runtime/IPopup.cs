using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// A modal view the <see cref="PopupManager"/> shows, awaits, and destroys. Any prefab whose root has
    /// a component implementing this interface can be a popup; <see cref="Popup"/> is the convenient base.
    /// </summary>
    /// <remarks>
    /// Lifecycle for one show:
    /// <c>OnOpened(arg)</c> → enter transition → (manager awaits <see cref="Result"/>) → <c>OnClosing()</c>
    /// → exit transition → instance destroyed.
    /// The view is <b>visually passive</b>: it does not animate itself or destroy itself. It completes
    /// <see cref="Result"/> (typically from a button) and the manager does the rest.
    /// </remarks>
    public interface IPopup
    {
        /// <summary>
        /// The popup's root GameObject. The manager toggles its active state and the transition animates it.
        /// Must not be <c>null</c>.
        /// </summary>
        GameObject Root { get; }

        /// <summary>
        /// Called once after the instance is created and parented, before the enter transition.
        /// Bind incoming data from <paramref name="arg"/> here.
        /// </summary>
        /// <param name="arg">Optional payload passed to <c>ShowAsync</c>; may be <c>null</c>.</param>
        void OnOpened(object arg);

        /// <summary>Called once after <see cref="Result"/> completes, before the exit transition. Release transient state.</summary>
        void OnClosing();

        /// <summary>
        /// The completion the manager awaits. The value is delivered as <c>object</c> and cast to the caller's
        /// <c>TResult</c> at the service boundary. Completing this (e.g. from a button) closes the popup.
        /// </summary>
        UniTask<object> Result { get; }

        /// <summary>
        /// A dismissal request from Back / a backdrop tap. The popup decides its dismiss outcome and completes
        /// <see cref="Result"/>; return <c>false</c> to veto dismissal (e.g. a mandatory dialog). The manager
        /// only calls this when the active <see cref="PopupOptions"/> permit it.
        /// </summary>
        bool TryDismiss();
    }
}
