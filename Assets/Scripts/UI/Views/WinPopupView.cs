using System;
using System.Collections;
using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup thắng màn: băng chúc mừng, tranh, tiền thưởng và nút Continue.
    public class WinPopupView : PopupView
    {
        [SerializeField] private Button _continueButton;

        [Header("Hiệu ứng hiện ra")]
        [Tooltip("Băng chúc mừng.")]
        [SerializeField] private RectTransform _banner;

        [Tooltip("Khoảng cách băng rơi xuống, tính bằng pixel UI.")]
        [SerializeField] private float _bannerDropDistance = 260f;

        [SerializeField] private float _bannerDuration = 0.35f;

        [Tooltip("Thời gian nút Continue phóng từ 0 lên 1.")]
        [SerializeField] private float _buttonDuration = 0.3f;

        [Tooltip("Pháo hoa ăn mừng trong popup.")]
        [SerializeField] private ParticleSystem _victoryEffect;

        [Tooltip("Chờ ngần này giây sau khi popup hiện rồi mới bắn pháo hoa.")]
        [SerializeField] private float _victoryDelay;

        [Header("Tiền thưởng")]
        [SerializeField] private CoinFlyVFX _coinFly;

        [Tooltip("Điểm coin bắn ra.")]
        [SerializeField] private RectTransform _coinFrom;

        [Tooltip("Điểm coin bay tới.")]
        [SerializeField] private RectTransform _coinTarget;

        [Tooltip("Dòng chữ tiền thưởng.")]
        [SerializeField] private TMP_Text _rewardText;

        [Tooltip("Tổng tiền hiện ở góc trên.")]
        [SerializeField] private Text _coinTotalText;

        [Tooltip("Thời gian nán lại sau khi coin cuối đáp rồi mới về Home.")]
        [SerializeField] private float _coinTailSeconds = 0.2f;

        [Tooltip("Thời gian chờ tối đa cho đợt coin bay.")]
        [SerializeField] private float _coinMaxWaitSeconds = 4f;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Object hiện thay cho nút khi đã hết màn.")]
        [SerializeField] private GameObject _lastLevelNotice;

        public override bool BlocksBackground => false;

        private ILevelService _levelService;
        private ILevelFlowService _levelFlow;
        private PlayerWallet _wallet;
        private HomeScreenView _home;

        private Sequence _showSequence;
        private Vector2 _bannerHomePosition;
        private bool _hasBannerHome;

        private int _displayedCoins;

        private int _pendingReward;

        [Inject]
        public void Construct(
            ILevelService levelService,
            ILevelFlowService levelFlow,
            PlayerWallet wallet,
            HomeScreenView home)
        {
            _levelService = levelService;
            _levelFlow = levelFlow;
            _wallet = wallet;
            _home = home;
        }

        private void Awake()
        {
            if (_continueButton != null) _continueButton.onClick.AddListener(HandleContinueClicked);
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(HandleContinueClicked);

            KillSequence();
        }

        public override void Show()
        {
            base.Show();

            if (Sound != null) Sound.Play(SoundKey.LevelComplete);

            CaptureBannerHome();

            var isLastLevel = _levelFlow != null && _levelFlow.IsLastLevel;
            var reward = RewardForCurrentLevel();

            _pendingReward = reward;

            if (_levelText != null && _levelFlow != null && _levelFlow.ClearedLevel >= 0)
            {
                _levelText.SetText("Level {0}", _levelFlow.ClearedLevel);
            }

            if (_rewardText != null) _rewardText.SetText("{0}", reward);
            if (_lastLevelNotice != null) _lastLevelNotice.SetActive(isLastLevel);

            _displayedCoins = _wallet != null ? _wallet.Coins : 0;
            SetCoinTotal(_displayedCoins);

            if (_wallet != null) _wallet.Add(reward);

            PlayVictoryEffect();

            PlayShowSequence();
        }

        /// Bắn pháo hoa nằm sẵn trong popup.
        private void PlayVictoryEffect()
        {
            if (_victoryEffect == null) return;

            if (_victoryDelay > 0f)
            {
                StartCoroutine(VictoryRoutine());
                return;
            }

            FireVictory();
        }

        private IEnumerator VictoryRoutine()
        {
            var elapsed = 0f;

            while (elapsed < _victoryDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            FireVictory();
        }

        private void FireVictory()
        {
            if (_victoryEffect == null) return;

            if (!_victoryEffect.gameObject.activeSelf) _victoryEffect.gameObject.SetActive(true);

            _victoryEffect.Clear(true);
            _victoryEffect.Play(true);
        }

        public override void Hide()
        {
            if (_victoryEffect != null)
            {
                _victoryEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            KillSequence();

            if (_coinFly != null) _coinFly.StopAll();

            RestoreBanner();

            base.Hide();
        }

        /// Băng rơi xuống rồi nút mọc lên.
        private void PlayShowSequence()
        {
            KillSequence();

            var button = _continueButton != null ? _continueButton.transform : null;

            if (button != null) button.localScale = Vector3.zero;

            if (_banner != null && _hasBannerHome)
            {
                _banner.anchoredPosition = _bannerHomePosition + Vector2.up * _bannerDropDistance;
            }

            _showSequence = DOTween.Sequence().SetUpdate(true);

            if (_banner != null && _hasBannerHome)
            {
                _showSequence.Append(MoveBannerHome());
            }

            AppendButtonPop(_showSequence, button);
        }

        /// Chia phần thưởng cho từng coin rồi cho bay.
        private void PlayCoinFly(int reward, Action onAllDone)
        {
            var coinCount = _coinFly.CoinCount;
            var arrived = 0;

            _coinFly.Play(
                _coinFrom,
                _coinTarget,
                onEachArrive: () =>
                {
                    arrived++;

                    if (Sound != null) Sound.Play(SoundKey.Coin);

                    var shown = arrived >= coinCount
                        ? reward
                        : Mathf.RoundToInt(reward * (arrived / (float)coinCount));

                    SetCoinTotal(_displayedCoins + shown);
                },
                onAllDone: () =>
                {
                    SetCoinTotal(_displayedCoins + reward);

                    onAllDone?.Invoke();
                });
        }

        /// Tween băng chúc mừng về chỗ gốc.
        private Tween MoveBannerHome()
        {
            return DOTween
                .To(() => _banner.anchoredPosition,
                    p => _banner.anchoredPosition = p,
                    _bannerHomePosition,
                    _bannerDuration)
                .SetEase(Ease.OutBack)
                .SetTarget(_banner);
        }

        private void AppendButtonPop(Sequence sequence, Transform button)
        {
            if (button == null) return;

            sequence.Append(button.DOScale(1f, _buttonDuration).SetEase(Ease.OutBack));
        }

        private void SetCoinTotal(int value)
        {
            if (_coinTotalText == null) return;

            _coinTotalText.text = string.Format("{0}", value);
        }

        private int RewardForCurrentLevel()
        {
            var config = _levelService != null ? _levelService.CurrentConfig : null;

            return config != null ? config.RewardCoins : 0;
        }

        /// Ghi lại chỗ đứng của băng ở lần mở đầu tiên.
        private void CaptureBannerHome()
        {
            if (_hasBannerHome || _banner == null) return;

            _bannerHomePosition = _banner.anchoredPosition;
            _hasBannerHome = true;
        }

        private void RestoreBanner()
        {
            if (_banner == null || !_hasBannerHome) return;

            _banner.anchoredPosition = _bannerHomePosition;
        }

        private void KillSequence()
        {
            _showSequence?.Kill();
            _showSequence = null;
        }

        /// Xử lý bấm nút Continue.
        private void HandleContinueClicked()
        {
            if (Sound != null) Sound.Play(SoundKey.Direction);

            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            StartCoroutine(ContinueRoutine());
        }

        /// Đường ra khỏi popup: coin bay xong rồi mới quét về Home.
        private IEnumerator ContinueRoutine()
        {
            yield return CoinFlyRoutine();

            var transition = _home != null ? _home.Transition : null;

            if (transition != null)
            {
                transition.Play(GoHome);
                yield break;
            }

            GoHome();
        }

        /// Bắn đợt coin rồi đợi nó đáp hết.
        private IEnumerator CoinFlyRoutine()
        {
            var canPlayCoins = _pendingReward > 0
                               && _coinFly != null
                               && _coinFrom != null
                               && _coinTarget != null;

            if (!canPlayCoins) yield break;

            var done = false;

            PlayCoinFly(_pendingReward, () => done = true);

            var elapsed = 0f;
            var limit = Mathf.Max(0.1f, _coinMaxWaitSeconds);

            while (!done && elapsed < limit)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_coinTailSeconds > 0f) yield return new WaitForSecondsRealtime(_coinTailSeconds);
        }

        /// Đổi sang Home.
        private void GoHome()
        {
            if (_home == null)
            {
                HideSilently();
                return;
            }

            var clearedLevel = _levelFlow != null ? _levelFlow.ClearedLevel : -1;

            if (clearedLevel >= 0) _home.ShowCelebrating(clearedLevel);
            else _home.Show();

            StartCoroutine(FadeOutBeforeHomeEnters());
        }

        /// Tan dần về 0 trong đúng khoảng lặng của Home, rồi mới tắt hẳn.
        private IEnumerator FadeOutBeforeHomeEnters()
        {
            var duration = _home != null ? _home.EnterDelaySeconds : 0f;

            if (duration > 0f)
            {
                var from = CanvasGroup.alpha;
                var elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;

                    CanvasGroup.alpha = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                CanvasGroup.alpha = 0f;
            }

            HideSilently();
        }
    }
}
