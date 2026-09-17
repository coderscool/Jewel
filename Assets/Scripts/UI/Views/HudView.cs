using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Data;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Hiển thị HUD của màn chơi.
    public class HudView : MonoBehaviour
    {
        [Tooltip("Số tiền đang có.")]
        [SerializeField] private Text _coinsText;

        [Tooltip("Nút gợi ý.")]
        [SerializeField] private Button _hintButton;

        [Tooltip("Số lượt gợi ý miễn phí còn lại.")]
        [SerializeField] private TMP_Text _hintCreditsText;

        [Tooltip("Huy hiệu bọc con số.")]
        [SerializeField] private GameObject _hintCreditsBadge;

        [Tooltip("Dấu + mời thêm lượt khi booster đã mở khoá mà hết lượt.")]
        [SerializeField] private GameObject _hintAddIcon;

        [Header("Booster tô tự do")]
        [Tooltip("Nút bật booster tô tự do.")]
        [SerializeField] private Button _freePaintButton;

        [Tooltip("Số lượt tô tự do còn lại.")]
        [SerializeField] private TMP_Text _freePaintCreditsText;

        [Tooltip("Huy hiệu bọc con số.")]
        [SerializeField] private GameObject _freePaintCreditsBadge;

        [Tooltip("Dấu + mời thêm lượt khi booster đã mở khoá mà hết lượt.")]
        [SerializeField] private GameObject _freePaintAddIcon;

        [Tooltip("Object bọc khung đồng hồ.")]
        [SerializeField] private GameObject _freePaintTimerRoot;

        [Tooltip("Đồng hồ đếm ngược của booster.")]
        [SerializeField] private Text _freePaintTimerText;

        [Tooltip("Thanh thời gian còn lại, thang 0..1.")]
        [SerializeField] private Image _freePaintTimerFill;

        [Header("Booster tô hết màu")]
        [Tooltip("Nút bật booster tô hết màu.")]
        [SerializeField] private Button _fillColorButton;

        [Tooltip("Số lượt tô hết màu còn lại.")]
        [SerializeField] private TMP_Text _fillColorCreditsText;

        [Tooltip("Huy hiệu bọc con số.")]
        [SerializeField] private GameObject _fillColorCreditsBadge;

        [Tooltip("Dấu + mời thêm lượt khi booster đã mở khoá mà hết lượt.")]
        [SerializeField] private GameObject _fillColorAddIcon;

        [Tooltip("Thanh tiến độ đợt tô, thang 0..1.")]
        [SerializeField] private Image _fillColorProgressFill;

        [Tooltip("Nút Tô lại.")]
        [SerializeField] private Button _resetButton;

        [Tooltip("Nút mở popup Cài đặt.")]
        [FormerlySerializedAs("_collectionButton")]
        [FormerlySerializedAs("_homeButton")]
        [SerializeField] private Button _settingsButton;

        [Header("Mở khoá booster")]
        [Tooltip("Bảng mốc mở khoá của từng booster.")]
        [SerializeField] private BoosterUnlockConfig _boosterUnlock;

        [Tooltip("Phần mặt đã mở khoá của nút gợi ý.")]
        [SerializeField] private GameObject _hintUnlockRoot;

        [Tooltip("Phần mặt đang khoá của nút gợi ý.")]
        [SerializeField] private GameObject _hintLockRoot;

        [Tooltip("Chữ ghi màn mở khoá của nút gợi ý.")]
        [SerializeField] private Text _hintLockLevelText;

        [Tooltip("Phần mặt đã mở khoá của nút tô tự do.")]
        [SerializeField] private GameObject _freePaintUnlockRoot;

        [Tooltip("Phần mặt đang khoá của nút tô tự do.")]
        [SerializeField] private GameObject _freePaintLockRoot;

        [SerializeField] private Text _freePaintLockLevelText;

        [Tooltip("Phần mặt đã mở khoá của nút tô hết màu.")]
        [SerializeField] private GameObject _fillColorUnlockRoot;

        [Tooltip("Phần mặt đang khoá của nút tô hết màu.")]
        [SerializeField] private GameObject _fillColorLockRoot;

        [SerializeField] private Text _fillColorLockLevelText;

        [Tooltip("Khuôn chữ ghi màn mở khoá.")]
        [SerializeField] private string _lockLevelFormat = "Level {0}";

        [Tooltip("Object bị ẩn khi thắng màn.")]
        [SerializeField] private GameObject _content;

        [Tooltip("CanvasGroup để HUD mờ dần lúc màn ăn mừng bắt đầu.")]
        [SerializeField] private CanvasGroup _celebrationFadeGroup;

        [Tooltip("Thời gian HUD mờ đi.")]
        [SerializeField] private float _celebrationFadeDuration = 0.25f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private IHintService _hintService;
        private IFreePaintService _freePaintService;
        private IFillColorService _fillColorService;
        private PlayerWallet _wallet;
        private ILevelFlowService _levelFlow;

        private Tween _celebrationFade;
        private IPopupService _popupService;
        private ISoundService _sound;

        private TutorialState _tutorialState;
        private PlayerProgress _progress;

        private bool _hintUnlocked = true;
        private bool _freePaintUnlocked = true;
        private bool _fillColorUnlocked = true;

        private int _displayedLevel = -1;
        private int _displayedCredits = -1;
        private int _displayedFreePaintCredits = -1;
        private int _displayedFillColorCredits = -1;

        private int _displayedCoins = -1;

        private int _displayedFreePaintSeconds = -1;

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            IHintService hintService,
            IFreePaintService freePaintService,
            IFillColorService fillColorService,
            ILevelFlowService levelFlow,
            IPopupService popupService,
            PlayerWallet wallet,
            ISoundService sound,
            PlayerProgress progress,
            TutorialState tutorialState)
        {
            _progress = progress;
            _tutorialState = tutorialState;

            _levelService = levelService;
            _paintService = paintService;
            _hintService = hintService;
            _freePaintService = freePaintService;
            _fillColorService = fillColorService;
            _levelFlow = levelFlow;
            _popupService = popupService;
            _wallet = wallet;
            _sound = sound;

            RefreshBoosterLocks();

            _levelService.OnLevelStarted += HandleLevelStarted;
            _paintService.OnCellPainted += HandleCellPainted;
            _hintService.OnHintAvailabilityChanged += SetHintAvailable;
            _hintService.OnCreditsChanged += SetHintCredits;

            _hintService.OnCreditsExhausted += HandleCreditsExhausted;
            _levelFlow.OnCelebrationStarted += HandleCelebrationStarted;
            _levelFlow.OnLevelCleared += HandleLevelCleared;

            if (_freePaintService != null)
            {
                _freePaintService.OnAvailabilityChanged += SetFreePaintAvailable;
                _freePaintService.OnCreditsChanged += SetFreePaintCredits;
                _freePaintService.OnCreditsExhausted += HandleFreePaintExhausted;
                _freePaintService.OnActiveChanged += HandleFreePaintActiveChanged;
            }

            if (_fillColorService != null)
            {
                _fillColorService.OnAvailabilityChanged += SetFillColorAvailable;
                _fillColorService.OnCreditsChanged += SetFillColorCredits;
                _fillColorService.OnCreditsExhausted += HandleFillColorExhausted;
                _fillColorService.OnFillingChanged += HandleFillingChanged;
            }

            if (_wallet != null)
            {
                _wallet.OnCoinsChanged += SetCoins;
                SetCoins(_wallet.Coins);
            }

            SetLevel(_levelService.CurrentLevel);
            SetHintCredits(_hintService.RemainingCredits);

            if (_freePaintButton != null) _freePaintButton.onClick.AddListener(HandleFreePaintClicked);

            if (_freePaintService != null)
            {
                SetFreePaintCredits(_freePaintService.RemainingCredits);
                SetFreePaintAvailable(_freePaintService.CanUse);
                HandleFreePaintActiveChanged(_freePaintService.IsActive);
            }

            if (_fillColorButton != null) _fillColorButton.onClick.AddListener(HandleFillColorClicked);

            if (_fillColorService != null)
            {
                SetFillColorCredits(_fillColorService.RemainingCredits);
                SetFillColorAvailable(_fillColorService.CanUse);
                HandleFillingChanged(_fillColorService.IsFilling);
            }

            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettingsClicked);

            if (_resetButton != null) _resetButton.onClick.AddListener(HandleResetClicked);

            RefreshResetAvailable();

            SetVisible(false);

            if (_hintButton == null) return;

            _hintButton.onClick.AddListener(HandleHintClicked);
            SetHintAvailable(_hintService.CanUseHint);
        }

        /// Huỷ đăng ký sự kiện.
        private void OnDestroy()
        {
            KillCelebrationFade();

            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_paintService != null) _paintService.OnCellPainted -= HandleCellPainted;
            if (_hintService != null)
            {
                _hintService.OnHintAvailabilityChanged -= SetHintAvailable;
                _hintService.OnCreditsChanged -= SetHintCredits;
                _hintService.OnCreditsExhausted -= HandleCreditsExhausted;
            }

            if (_freePaintService != null)
            {
                _freePaintService.OnAvailabilityChanged -= SetFreePaintAvailable;
                _freePaintService.OnCreditsChanged -= SetFreePaintCredits;
                _freePaintService.OnCreditsExhausted -= HandleFreePaintExhausted;
                _freePaintService.OnActiveChanged -= HandleFreePaintActiveChanged;
            }

            if (_fillColorService != null)
            {
                _fillColorService.OnAvailabilityChanged -= SetFillColorAvailable;
                _fillColorService.OnCreditsChanged -= SetFillColorCredits;
                _fillColorService.OnCreditsExhausted -= HandleFillColorExhausted;
                _fillColorService.OnFillingChanged -= HandleFillingChanged;
            }

            if (_wallet != null) _wallet.OnCoinsChanged -= SetCoins;

            if (_levelFlow != null)
            {
                _levelFlow.OnCelebrationStarted -= HandleCelebrationStarted;
                _levelFlow.OnLevelCleared -= HandleLevelCleared;
            }

            if (_fillColorButton != null) _fillColorButton.onClick.RemoveListener(HandleFillColorClicked);
            if (_freePaintButton != null) _freePaintButton.onClick.RemoveListener(HandleFreePaintClicked);
            if (_hintButton != null) _hintButton.onClick.RemoveListener(HandleHintClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        private void HandleLevelStarted(int levelId)
        {
            SetVisible(true);
            SetLevel(levelId);

            RefreshBoosterLocks();

            RefreshResetAvailable();
        }

        /// Đọc lại mốc mở khoá rồi dựng lại cả ba nút.
        private void RefreshBoosterLocks()
        {
            var level = _progress != null ? _progress.Level : int.MaxValue;

            _hintUnlocked = _boosterUnlock == null
                            || _boosterUnlock.IsUnlocked(CreditPoolKind.Hint, level);
            _freePaintUnlocked = _boosterUnlock == null
                                 || _boosterUnlock.IsUnlocked(CreditPoolKind.FreePaint, level);
            _fillColorUnlocked = _boosterUnlock == null
                                 || _boosterUnlock.IsUnlocked(CreditPoolKind.FillColor, level);

            ApplyLockVisual(CreditPoolKind.Hint, _hintUnlocked,
                _hintUnlockRoot, _hintLockRoot, _hintLockLevelText);
            ApplyLockVisual(CreditPoolKind.FreePaint, _freePaintUnlocked,
                _freePaintUnlockRoot, _freePaintLockRoot, _freePaintLockLevelText);
            ApplyLockVisual(CreditPoolKind.FillColor, _fillColorUnlocked,
                _fillColorUnlockRoot, _fillColorLockRoot, _fillColorLockLevelText);

            if (_hintService != null) SetHintAvailable(_hintService.CanUseHint);
            if (_freePaintService != null) SetFreePaintAvailable(_freePaintService.CanUse);
            if (_fillColorService != null) SetFillColorAvailable(_fillColorService.CanUse);

            if (_hintService != null) SetHintCredits(_hintService.RemainingCredits);
            else ApplyCreditVisual(_hintCreditsBadge, _hintAddIcon, 0, false);

            if (_freePaintService != null) SetFreePaintCredits(_freePaintService.RemainingCredits);
            else ApplyCreditVisual(_freePaintCreditsBadge, _freePaintAddIcon, 0, false);

            if (_fillColorService != null) SetFillColorCredits(_fillColorService.RemainingCredits);
            else ApplyCreditVisual(_fillColorCreditsBadge, _fillColorAddIcon, 0, false);
        }

        /// Hiện huy hiệu số lượt hoặc dấu cộng.
        private static void ApplyCreditVisual(GameObject badge, GameObject addIcon, int remaining, bool unlocked)
        {
            var hasCredits = remaining > 0;

            if (badge != null) badge.SetActive(unlocked && hasCredits);
            if (addIcon != null) addIcon.SetActive(unlocked && !hasCredits);
        }

        /// Hiện mặt khoá hoặc mặt mở khoá của nút booster.
        private void ApplyLockVisual(CreditPoolKind booster, bool unlocked,
            GameObject unlockRoot, GameObject lockRoot, Text levelText)
        {
            if (unlockRoot != null) unlockRoot.SetActive(unlocked);
            if (lockRoot != null) lockRoot.SetActive(!unlocked);

            if (levelText == null) return;

            levelText.gameObject.SetActive(!unlocked);

            if (unlocked || _boosterUnlock == null) return;

            var level = _boosterUnlock.UnlockLevelFor(booster);

            levelText.text = string.IsNullOrEmpty(_lockLevelFormat)
                ? level.ToString()
                : string.Format(_lockLevelFormat, level);
        }

        /// Cập nhật nút Tô lại khi có ô được tô.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex) => RefreshResetAvailable();

        /// Ẩn HUD khi màn ăn mừng bắt đầu.
        private void HandleCelebrationStarted()
        {
            var target = _content != null ? _content : gameObject;
            if (!target.activeSelf) return;

            if (_celebrationFadeGroup == null || _celebrationFadeDuration <= 0f)
            {
                SetVisible(false);
                return;
            }

            KillCelebrationFade();

            _celebrationFadeGroup.interactable = false;
            _celebrationFadeGroup.blocksRaycasts = false;

            _celebrationFade = DOVirtual
                .Float(_celebrationFadeGroup.alpha, 0f, _celebrationFadeDuration, value =>
                {
                    if (_celebrationFadeGroup != null) _celebrationFadeGroup.alpha = value;
                })
                .SetUpdate(true)
                .OnComplete(() => SetVisible(false));
        }

        private void HandleLevelCleared() => SetVisible(false);

        /// Trả CanvasGroup về trạng thái hiện đủ.
        private void RestoreCelebrationFade()
        {
            KillCelebrationFade();

            if (_celebrationFadeGroup == null) return;

            _celebrationFadeGroup.alpha = 1f;
            _celebrationFadeGroup.interactable = true;
            _celebrationFadeGroup.blocksRaycasts = true;
        }

        private void KillCelebrationFade()
        {
            if (_celebrationFade != null && _celebrationFade.IsActive()) _celebrationFade.Kill();

            _celebrationFade = null;
        }

        /// Ẩn hoặc hiện HUD.
        public void SetVisible(bool visible)
        {
            if (visible) RestoreCelebrationFade();
            else KillCelebrationFade();

            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        /// Xử lý bấm nút gợi ý.
        private void HandleHintClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.Hint);

            _hintService.UseHint();
        }

        private void HandleFreePaintClicked()
        {
            if (_freePaintService == null) return;

            if (_sound != null) _sound.Play(SoundKey.FreePaint);

            _freePaintService.Use();
        }

        private void HandleFreePaintExhausted() => _popupService.Show(PopupKey.FreePaint);

        /// Bật/tắt phần đồng hồ đếm ngược.
        private void HandleFreePaintActiveChanged(bool active)
        {
            if (_freePaintTimerRoot != null)
            {
                _freePaintTimerRoot.SetActive(active);
            }
            else
            {
                if (_freePaintTimerText != null) _freePaintTimerText.gameObject.SetActive(active);
                if (_freePaintTimerFill != null) _freePaintTimerFill.gameObject.SetActive(active);
            }

            _displayedFreePaintSeconds = -1;

            if (active) TickFreePaintTimer();
        }

        private void HandleFillColorClicked()
        {
            if (_fillColorService == null) return;

            if (_sound != null) _sound.Play(SoundKey.MagicWand);

            _fillColorService.Use();
        }

        private void HandleFillColorExhausted() => _popupService.Show(PopupKey.FillColor);

        /// Bật/tắt vòng tiến độ của đợt tô.
        private void HandleFillingChanged(bool filling)
        {
            if (_fillColorProgressFill == null) return;

            _fillColorProgressFill.gameObject.SetActive(filling);

            if (filling) _fillColorProgressFill.fillAmount = 0f;
        }

        /// Cập nhật đồng hồ booster và tiến độ đợt tô.
        private void Update()
        {
            if (_freePaintService != null && _freePaintService.IsActive) TickFreePaintTimer();

            if (_fillColorProgressFill != null && _fillColorService != null && _fillColorService.IsFilling)
            {
                _fillColorProgressFill.fillAmount = Mathf.Clamp01(_fillColorService.FillProgress);
            }
        }

        private void TickFreePaintTimer()
        {
            var remaining = _freePaintService.RemainingSeconds;

            if (_freePaintTimerFill != null)
            {
                _freePaintTimerFill.fillAmount = Mathf.Clamp01(remaining / _freePaintService.DurationSeconds);
            }

            if (_freePaintTimerText == null) return;

            var seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            if (seconds == _displayedFreePaintSeconds) return;

            _displayedFreePaintSeconds = seconds;

            _freePaintTimerText.text = seconds < 10
                ? "00:0" + seconds
                : "00:" + seconds;
        }

        private void SetFreePaintCredits(int remaining)
        {
            ApplyCreditVisual(_freePaintCreditsBadge, _freePaintAddIcon, remaining, _freePaintUnlocked);

            if (_freePaintCreditsText == null) return;
            if (remaining == _displayedFreePaintCredits) return;

            _displayedFreePaintCredits = remaining;
            _freePaintCreditsText.SetText("{0}", remaining);
        }

        private void SetFreePaintAvailable(bool available)
        {
            if (_freePaintButton == null) return;

            _freePaintButton.interactable = available && _freePaintUnlocked;
        }

        private void SetFillColorCredits(int remaining)
        {
            ApplyCreditVisual(_fillColorCreditsBadge, _fillColorAddIcon, remaining, _fillColorUnlocked);

            if (_fillColorCreditsText == null) return;
            if (remaining == _displayedFillColorCredits) return;

            _displayedFillColorCredits = remaining;
            _fillColorCreditsText.SetText("{0}", remaining);
        }

        private void SetFillColorAvailable(bool available)
        {
            if (_fillColorButton == null) return;

            _fillColorButton.interactable = available && _fillColorUnlocked;
        }

        /// Xoá tiến độ tô của màn đang chơi rồi nạp lại.
        private void HandleResetClicked() => _paintService.ResetCurrentLevel();

        private void RefreshResetAvailable()
        {
            if (_resetButton == null || _paintService == null) return;

            _resetButton.interactable = _paintService.CanReset;
        }

        /// Mở popup cài đặt.
        private void HandleSettingsClicked()
        {
            if (_tutorialState != null && _tutorialState.IsRunning) return;

            if (_sound != null) _sound.Play(SoundKey.ButtonClick);

            _popupService.Show(PopupKey.Settings);
        }

        /// Bấm nút mà hết lượt: mở popup mời thêm lượt.
        private void HandleCreditsExhausted() => _popupService.Show(PopupKey.HintMove);

        /// Hiện số lượt gợi ý.
        private void SetHintCredits(int remaining)
        {
            ApplyCreditVisual(_hintCreditsBadge, _hintAddIcon, remaining, _hintUnlocked);

            if (_hintCreditsText == null) return;
            if (remaining == _displayedCredits) return;

            _displayedCredits = remaining;
            _hintCreditsText.SetText("{0}", remaining);
        }

        /// Bật tắt nút gợi ý theo khoá và trạng thái service.
        private void SetHintAvailable(bool available)
        {
            if (_hintButton == null) return;

            _hintButton.interactable = available && _hintUnlocked;
        }

        /// Hiện số tiền.
        private void SetCoins(int coins)
        {
            if (_coinsText == null) return;
            if (coins == _displayedCoins) return;

            _displayedCoins = coins;
            _coinsText.text = coins.ToString();
        }

        private void SetLevel(int level)
        {
            if (level == _displayedLevel) return;

            _displayedLevel = level;
        }
    }
}
