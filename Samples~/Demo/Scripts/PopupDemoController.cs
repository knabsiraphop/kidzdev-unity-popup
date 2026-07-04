using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.Popup.Samples
{
    /// <summary>
    /// Demo driver: buttons exercise the popup service several ways — a confirm loaded from
    /// <b>Resources</b>, an alert loaded from <b>Resources</b>, a <b>stacked</b> flow that raises a
    /// <c>Direct</c> popup (a serialized prefab) on top of a Resources confirm, a timed <b>announcement</b>
    /// (<see cref="PopupOptions.AutoDismissAfter"/>, tap-or-wait), two announcements <b>stacked</b> with
    /// different durations, and a <b>reward stack</b> (<see cref="PopupStack"/>) shown three ways — a
    /// <see cref="DeckStackLayout"/> deck (3 peeking at once), a <see cref="GroupStackLayout"/> list, and a
    /// one-at-a-time <b>timed notification</b> queue (a <see cref="DeckStackLayout"/> with
    /// <c>maxVisible: 1</c> + <c>autoDismissAfter</c>: each reward shows, counts down, and — collected or
    /// not — the next one takes its place). The result of each flow is written to a status label so the
    /// behaviour is visible in the Game view.
    /// </summary>
    public sealed class PopupDemoController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _alertButton;
        [SerializeField] private Button _stackedButton;
        [SerializeField] private Button _announcementButton;
        [SerializeField] private Button _stackedAnnouncementButton;
        [SerializeField] private Button _rewardsDeckButton;
        [SerializeField] private Button _rewardsGroupButton;
        [SerializeField] private Button _rewardsDeckTimedButton;
        [SerializeField] private Text _statusLabel;

        [Header("Sources")]
        [Tooltip("Resources path (under any Resources/ folder) of the confirm popup prefab.")]
        [SerializeField] private string _confirmResourcePath = "Popups/DemoConfirmPopup";
        [Tooltip("Resources path of the alert popup prefab.")]
        [SerializeField] private string _alertResourcePath = "Popups/DemoAlertPopup";
        [Tooltip("Prefab shown via PopupRef.Direct in the stacked flow.")]
        [SerializeField] private GameObject _directPopupPrefab;
        [Tooltip("Resources path of the timed announcement popup prefab.")]
        [SerializeField] private string _announcementResourcePath = "Popups/DemoAnnouncementPopup";
        [Tooltip("Resources path of the reward card prefab.")]
        [SerializeField] private string _rewardResourcePath = "Popups/DemoRewardCard";
        [Tooltip("VerticalLayoutGroup the Group-layout reward stack parents its cards under.")]
        [SerializeField] private VerticalLayoutGroup _rewardGroupContainer;

        private IPopupService _service;
        private IPopupStack _deckRewardStack;
        private IPopupStack _groupRewardStack;
        private IPopupStack _timedRewardStack;

        private void Awake()
        {
            // A self-owned service with a fade transition. The default loader already routes Resources + Direct.
            _service = new PopupManager(transition: new FadePopupTransition(0.15f));

            var backdrop = new Color(0f, 0f, 0f, 0.3f);
            _deckRewardStack = new PopupStackController(new DeckStackLayout(maxVisible: 3), backdropColor: backdrop);
            if (_rewardGroupContainer != null)
                _groupRewardStack = new PopupStackController(new GroupStackLayout(_rewardGroupContainer, maxVisible: 3), backdropColor: backdrop);

            // maxVisible: 1 turns the same Deck layout into a one-at-a-time notification queue: nothing to
            // peek behind since only one card ever exists, so each reward shows, counts down, and — whether
            // collected or timed out — the next one is promoted into the same single slot.
            _timedRewardStack = new PopupStackController(new DeckStackLayout(maxVisible: 1), backdropColor: backdrop);
        }

        private void OnEnable()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_alertButton != null) _alertButton.onClick.AddListener(OnAlertClicked);
            if (_stackedButton != null) _stackedButton.onClick.AddListener(OnStackedClicked);
            if (_announcementButton != null) _announcementButton.onClick.AddListener(OnAnnouncementClicked);
            if (_stackedAnnouncementButton != null) _stackedAnnouncementButton.onClick.AddListener(OnStackedAnnouncementClicked);
            if (_rewardsDeckButton != null) _rewardsDeckButton.onClick.AddListener(OnRewardsDeckClicked);
            if (_rewardsGroupButton != null) _rewardsGroupButton.onClick.AddListener(OnRewardsGroupClicked);
            if (_rewardsDeckTimedButton != null) _rewardsDeckTimedButton.onClick.AddListener(OnRewardsDeckTimedClicked);
        }

        private void OnDisable()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_alertButton != null) _alertButton.onClick.RemoveListener(OnAlertClicked);
            if (_stackedButton != null) _stackedButton.onClick.RemoveListener(OnStackedClicked);
            if (_announcementButton != null) _announcementButton.onClick.RemoveListener(OnAnnouncementClicked);
            if (_stackedAnnouncementButton != null) _stackedAnnouncementButton.onClick.RemoveListener(OnStackedAnnouncementClicked);
            if (_rewardsDeckButton != null) _rewardsDeckButton.onClick.RemoveListener(OnRewardsDeckClicked);
            if (_rewardsGroupButton != null) _rewardsGroupButton.onClick.RemoveListener(OnRewardsGroupClicked);
            if (_rewardsDeckTimedButton != null) _rewardsDeckTimedButton.onClick.RemoveListener(OnRewardsDeckTimedClicked);
        }

        private void OnDestroy()
        {
            _service?.Dispose();
            _deckRewardStack?.Dispose();
            _groupRewardStack?.Dispose();
            _timedRewardStack?.Dispose();
        }

        // Forwards to async handlers; Forget keeps the button callback synchronous.
        private void OnConfirmClicked() => ShowConfirm().Forget();
        private void OnAlertClicked() => ShowAlert().Forget();
        private void OnStackedClicked() => ShowStacked().Forget();
        private void OnAnnouncementClicked() => ShowAnnouncement().Forget();
        private void OnStackedAnnouncementClicked() => ShowStackedAnnouncements().Forget();
        private void OnRewardsDeckClicked() => ShowRewards(_deckRewardStack, "Deck").Forget();
        private void OnRewardsGroupClicked() => ShowRewards(_groupRewardStack, "Layout").Forget();
        private void OnRewardsDeckTimedClicked() => ShowTimedRewards().Forget();

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

        private async UniTaskVoid ShowAnnouncement()
        {
            SetStatus("Announcement: waiting…");
            var result = await _service.ShowAsync<PopupResult>(
                PopupRef.Resources(_announcementResourcePath),
                new AnnouncementPopup.Content("Daily bonus!", "+50 coins", 3f),
                new PopupOptions
                {
                    AutoDismissAfter = 3f,
                    DismissOnBackdropClick = true,
                    BackdropColor = new Color(0f, 0f, 0f, 0.3f),
                });
            SetStatus(result == PopupResult.Confirmed ? "Announcement: tapped" : "Announcement: timed out");
        }

        private async UniTaskVoid ShowStackedAnnouncements()
        {
            SetStatus("Stacked announcements: waiting…");

            var lower = _service.ShowAsync<PopupResult>(
                PopupRef.Resources(_announcementResourcePath),
                new AnnouncementPopup.Content("Event ends soon", "Closes in 6s", 6f),
                new PopupOptions { AutoDismissAfter = 6f, BackdropColor = new Color(0f, 0f, 0f, 0.3f) });

            // Raised on top so the reentrant stack (and its own shorter timer) is visible while the lower
            // announcement is still counting down.
            var upper = _service.ShowAsync<PopupResult>(
                PopupRef.Resources(_announcementResourcePath),
                new AnnouncementPopup.Content("On top", "Closes in 3s", 3f),
                new PopupOptions { AutoDismissAfter = 3f, BackdropColor = new Color(0f, 0f, 0f, 0.3f) });

            await UniTask.WhenAll(lower, upper);
            SetStatus("Stacked announcements: both auto-closed");
        }

        private async UniTaskVoid ShowRewards(IPopupStack stack, string label)
        {
            if (stack == null)
            {
                SetStatus($"Rewards ({label}): container not assigned");
                return;
            }

            SetStatus($"Rewards ({label}): 5 granted, 3 visible at once…");
            var pending = new UniTask<PopupResult>[5];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = stack.Enqueue<PopupResult>(PopupRef.Resources(_rewardResourcePath), $"Reward #{i + 1}");

            await UniTask.WhenAll(pending);
            SetStatus($"Rewards ({label}): all 5 collected");
        }

        private async UniTaskVoid ShowTimedRewards()
        {
            if (_timedRewardStack == null)
            {
                SetStatus("Rewards (Timed): container not assigned");
                return;
            }

            const float seconds = 4f;
            const int count = 3;
            SetStatus($"Rewards (Timed): 1 of {count} — collect it or it auto-closes and the next one shows…");

            int collected = 0;
            for (int i = 0; i < count; i++)
            {
                var result = await _timedRewardStack.Enqueue<PopupResult>(
                    PopupRef.Resources(_rewardResourcePath),
                    new RewardCardPopup.Content($"Reward #{i + 1}", seconds),
                    autoDismissAfter: seconds);
                if (result == PopupResult.Confirmed) collected++;

                if (i + 1 < count)
                    SetStatus($"Rewards (Timed): {i + 2} of {count} — collect it or it auto-closes and the next one shows…");
            }

            SetStatus($"Rewards (Timed): {collected}/{count} collected, rest auto-closed");
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }
    }
}
