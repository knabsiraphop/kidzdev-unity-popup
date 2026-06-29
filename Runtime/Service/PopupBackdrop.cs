using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// A full-screen scrim raised behind a popup: dims the content, blocks raycasts to everything below it,
    /// and (optionally) reports taps so the manager can dismiss the popup. One backdrop per open popup, so
    /// the newest popup's backdrop dims the popups beneath it too.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PopupBackdrop : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Invoked when the backdrop is tapped (only wired when dismiss-on-backdrop-click is enabled).</summary>
        public Action OnClicked;

        private Image _image;

        /// <summary>Creates a stretched backdrop under <paramref name="layer"/> tinted with <paramref name="color"/>.</summary>
        public static PopupBackdrop Create(Transform layer, Color color)
        {
            var go = new GameObject("PopupBackdrop", typeof(RectTransform), typeof(Image), typeof(PopupBackdrop));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            var backdrop = go.GetComponent<PopupBackdrop>();
            backdrop._image = go.GetComponent<Image>();
            backdrop._image.color = color;
            backdrop._image.raycastTarget = true;
            return backdrop;
        }

        /// <inheritdoc/>
        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke();
    }
}
