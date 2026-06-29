using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup.Samples
{
    /// <summary>
    /// Demo driver: three buttons exercise the popup service three ways — a confirm loaded from
    /// <b>Resources</b>, an alert loaded from <b>Resources</b>, and a <b>stacked</b> flow that raises a
    /// <c>Direct</c> popup (a serialized prefab) on top of a Resources confirm. The result of each flow is
    /// written to a status label so the behaviour is visible in the Game view.
    /// </summary>
    public sealed class PopupDemoController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _alertButton;
        [SerializeField] private Button _stackedButton;
        [SerializeField] private Text _statusLabel;

        [Header("Sources")]
        [Tooltip("Resources path (under any Resources/ folder) of the confirm popup prefab.")]
        [SerializeField] private string _confirmResourcePath = "Popups/DemoConfirmPopup";
        [Tooltip("Resources path of the alert popup prefab.")]
        [SerializeField] private string _alertResourcePath = "Popups/DemoAlertPopup";
        [Tooltip("Prefab shown via PopupRef.Direct in the stacked flow.")]
        [SerializeField] private GameObject _directPopupPrefab;

        private IPopupService _service;

        private void Awake()
        {
            // A self-owned service with a fade transition. The default loader already routes Resources + Direct.
            _service = new PopupManager(transition: new FadePopupTransition(0.15f));
        }

        private void OnEnable()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_alertButton != null) _alertButton.onClick.AddListener(OnAlertClicked);
            if (_stackedButton != null) _stackedButton.onClick.AddListener(OnStackedClicked);
        }

        private void OnDisable()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_alertButton != null) _alertButton.onClick.RemoveListener(OnAlertClicked);
            if (_stackedButton != null) _stackedButton.onClick.RemoveListener(OnStackedClicked);
        }

        private void OnDestroy() => _service?.Dispose();

        // Forwards to async handlers; Forget keeps the button callback synchronous.
        private void OnConfirmClicked() => ShowConfirm().Forget();
        private void OnAlertClicked() => ShowAlert().Forget();
        private void OnStackedClicked() => ShowStacked().Forget();

        private async UniTaskVoid ShowConfirm()
        {
            SetStatus("Confirm: waiting…");
            var ok = await _service.ShowAsync<bool>(
                PopupRef.Resources(_confirmResourcePath),
                new ConfirmPopup.Content("Delete save?", "This can't be undone."));
            SetStatus(ok ? "Confirm: YES" : "Confirm: NO");
        }

        private async UniTaskVoid ShowAlert()
        {
            SetStatus("Alert: waiting…");
            var result = await _service.ShowAsync<PopupResult>(
                PopupRef.Resources(_alertResourcePath),
                new ConfirmPopup.Content("Saved", "Your progress is safe."));
            SetStatus("Alert: " + result);
        }

        private async UniTaskVoid ShowStacked()
        {
            SetStatus("Stacked: confirm…");
            var confirm = _service.ShowAsync<bool>(
                PopupRef.Resources(_confirmResourcePath),
                new ConfirmPopup.Content("Buy item?", "Tap Yes to see a Direct popup stack on top."));

            // Raise a Direct popup on top of the confirm to show stacking + a second loader source.
            if (_directPopupPrefab != null)
            {
                await _service.ShowAsync<PopupResult>(
                    PopupRef.Direct(_directPopupPrefab),
                    new ConfirmPopup.Content("On top (Direct)", "Close me to get back to the confirm."));
            }

            var ok = await confirm;
            SetStatus(ok ? "Stacked: bought" : "Stacked: cancelled");
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }
    }
}
