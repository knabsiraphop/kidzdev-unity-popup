using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Convenient base for concrete popups. Implements <see cref="IPopup"/>, exposes a
    /// <see cref="Close(object)"/> for buttons to call, and completes an internal completion the manager awaits.
    /// Subclass it for ready-made dialogs (<see cref="ConfirmPopup"/>, <see cref="AlertPopup"/>) or your own.
    /// </summary>
    public abstract class Popup : MonoBehaviour, IPopup
    {
        [Tooltip("Optional explicit root; defaults to this component's GameObject. Set when the IPopup lives on a child.")]
        [SerializeField] private GameObject _rootOverride;

        private UniTaskCompletionSource<object> _completion;
        private UniTaskCompletionSource<object> Completion => _completion ??= new UniTaskCompletionSource<object>();

        /// <inheritdoc/>
        public GameObject Root => _rootOverride != null ? _rootOverride : gameObject;

        /// <inheritdoc/>
        public UniTask<object> Result => Completion.Task;

        /// <summary>The result delivered when the popup is dismissed via Back / backdrop. Override to change it.</summary>
        protected virtual object DismissResult => PopupResult.Cancelled;

        /// <inheritdoc/>
        public virtual void OnOpened(object arg) { }

        /// <inheritdoc/>
        public virtual void OnClosing() { }

        /// <inheritdoc/>
        public virtual bool TryDismiss()
        {
            Completion.TrySetResult(DismissResult);
            return true;
        }

        /// <summary>Closes the popup, completing its result with <paramref name="result"/>. Safe to call once;
        /// later calls are ignored.</summary>
        protected void Close(object result)
        {
            Completion.TrySetResult(result);
        }
    }
}
