using DG.Tweening;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup hiện ra khi tô xong bức tranh: băng chúc mừng, tranh vừa hoàn thành, tiền
    /// thưởng bay về icon tiền, rồi nút Continue đưa người chơi về Home.
    ///
    /// Nhịp cố ý xếp nối đuôi nhau chứ không nổ cùng lúc: băng rơi xuống → tiền vãi ra
    /// rồi bay lên → nút hiện. Mỗi thứ có một khoảnh khắc riêng, và nút chỉ xuất hiện
    /// khi phần thưởng đã cộng xong nên không ai bấm mất hiệu ứng.
    public class WinPopupView : PopupView
    {
        [SerializeField] private Button _continueButton;

        [Header("Hiệu ứng hiện ra")]
        [Tooltip("Băng CONGRATULATION. Rơi từ trên xuống rồi nảy nhẹ.")]
        [SerializeField] private RectTransform _banner;

        [Tooltip("Băng bắt đầu ở đâu so với chỗ đứng cuối, tính bằng pixel UI. " +
                 "Dương là rơi từ trên xuống.")]
        [SerializeField] private float _bannerDropDistance = 260f;

        [SerializeField] private float _bannerDuration = 0.45f;

        [Tooltip("Nút Continue phóng từ 0 lên 1. Chỉ hiện SAU khi tiền bay xong.")]
        [SerializeField] private float _buttonDuration = 0.35f;

        [Header("Tiền thưởng")]
        [SerializeField] private CoinFlyVFX _coinFly;

        [Tooltip("Coin bắn ra từ đây — thường là chỗ dòng chữ Reward.")]
        [SerializeField] private RectTransform _coinFrom;

        [Tooltip("Coin bay tới đây — icon tiền ở góc trên.")]
        [SerializeField] private RectTransform _coinTarget;

        [Tooltip("Dòng 'Reward: 10'.")]
        [SerializeField] private TMP_Text _rewardText;

        [Tooltip("Tổng tiền hiện ở góc trên. Tăng dần theo từng coin bay tới.")]
        [SerializeField] private TMP_Text _coinTotalText;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Object hiện thay cho nút khi đã hết màn, ví dụ dòng 'Hết màn rồi'.")]
        [SerializeField] private GameObject _lastLevelNotice;

        private ILevelService _levelService;
        private ILevelFlowService _levelFlow;
        private PlayerWallet _wallet;
        private HomeScreenView _home;

        /// Popup thắng màn KHÔNG làm tối nền: cả màn ăn mừng nằm ở bức tranh phía sau —
        /// dải lấp lánh quét qua, camera thu về giữa. Phủ một lớp tối lên đó là che mất
        /// đúng phần thưởng mà popup này sinh ra để chúc mừng.
        public override bool DimsBackground => false;

        private Sequence _showSequence;
        private Vector2 _bannerHomePosition;
        private bool _hasBannerHome;

        /// Tổng tiền đang hiện trên màn. Đếm riêng thay vì đọc ví, vì lúc coin đang bay
        /// thì ví đã cộng xong rồi — con số phải đi theo coin, không đi theo ví.
        private int _displayedCoins;

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

            CaptureBannerHome();

            var isLastLevel = _levelFlow != null && _levelFlow.IsLastLevel;
            var reward = RewardForCurrentLevel();

            // ClearedLevel chứ không phải CurrentLevel: tiến trình đã nhích sang màn kế
            // ngay lúc tô xong, nên CurrentLevel giờ là màn SAU màn vừa thắng.
            if (_levelText != null && _levelFlow != null && _levelFlow.ClearedLevel >= 0)
            {
                _levelText.SetText("Level {0}", _levelFlow.ClearedLevel);
            }

            if (_rewardText != null) _rewardText.SetText("{0}", reward);
            if (_lastLevelNotice != null) _lastLevelNotice.SetActive(isLastLevel);

            // Cộng tiền NGAY, không đợi coin bay xong. Hiệu ứng chỉ là hình ảnh — người
            // chơi thoát app giữa chừng vẫn phải có tiền.
            _displayedCoins = _wallet != null ? _wallet.Coins : 0;
            SetCoinTotal(_displayedCoins);

            if (_wallet != null) _wallet.Add(reward);

            PlayShowSequence(reward);
        }

        public override void Hide()
        {
            KillSequence();

            if (_coinFly != null) _coinFly.StopAll();

            RestoreBanner();

            base.Hide();
        }

        private void PlayShowSequence(int reward)
        {
            KillSequence();

            var button = _continueButton != null ? _continueButton.transform : null;

            // Nút bắt đầu ở scale 0 chứ không phải SetActive(false): object đang tắt thì
            // Layout Group bỏ nó ra khỏi hàng và mọi thứ quanh nó nhảy chỗ một nhịp.
            if (button != null) button.localScale = Vector3.zero;

            if (_banner != null && _hasBannerHome)
            {
                _banner.anchoredPosition = _bannerHomePosition + Vector2.up * _bannerDropDistance;
            }

            _showSequence = DOTween.Sequence().SetUpdate(true);

            if (_banner != null && _hasBannerHome)
            {
                // OutBack cho băng nảy nhẹ quá đích rồi lùi về — đó là thứ làm nó ra dáng
                // một tấm biển được thả xuống, thay vì một ảnh trượt vào.
                _showSequence.Append(MoveBannerHome());
            }

            // Thiếu bất cứ mảnh nào của phần tiền thì bỏ qua thẳng sang nút. Nút PHẢI
            // hiện trong mọi trường hợp — nó là đường duy nhất ra khỏi popup này.
            var canPlayCoins = reward > 0
                               && _coinFly != null
                               && _coinFrom != null
                               && _coinTarget != null;

            if (canPlayCoins)
            {
                _showSequence.AppendCallback(() => PlayCoinFly(reward, button));
                return;
            }

            AppendButtonPop(_showSequence, button);
        }

        /// Chia đều phần thưởng cho từng coin, phần lẻ dồn vào coin cuối — cộng thiếu một
        /// đồng vì làm tròn thì con số cuối cùng trên màn không khớp với ví.
        private void PlayCoinFly(int reward, Transform button)
        {
            var coinCount = _coinFly.CoinCount;
            var arrived = 0;

            _coinFly.Play(
                _coinFrom,
                _coinTarget,
                onEachArrive: () =>
                {
                    arrived++;

                    var shown = arrived >= coinCount
                        ? reward
                        : Mathf.RoundToInt(reward * (arrived / (float)coinCount));

                    SetCoinTotal(_displayedCoins + shown);
                },
                onAllDone: () =>
                {
                    SetCoinTotal(_displayedCoins + reward);

                    var pop = DOTween.Sequence().SetUpdate(true);
                    AppendButtonPop(pop, button);

                    _showSequence = pop;
                });
        }

        /// DOTween.To trên anchoredPosition chứ không dùng RectTransform.DOAnchorPos:
        /// DOAnchorPos nằm trong module UI của DOTween, mà project cố ý chỉ dùng phần
        /// core để khỏi phải khai thêm assembly. Cùng kết quả, một dòng dài hơn.
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

            _coinTotalText.SetText("{0}", value);
        }

        private int RewardForCurrentLevel()
        {
            var config = _levelService != null ? _levelService.CurrentConfig : null;

            return config != null ? config.RewardCoins : 0;
        }

        /// Ghi lại chỗ đứng của băng ở lần mở ĐẦU TIÊN. Đọc muộn hơn là đọc nhầm vị trí
        /// mà chính hiệu ứng vừa đặt nó vào.
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

        /// Ẩn TRƯỚC khi động vào tiến trình: mở Home kéo theo cả loạt việc dựng lại danh
        /// sách, để popup còn đứng đó thì nó nằm chình ình trên màn hình Home.
        private void HandleContinueClicked()
        {
            Hide();

            if (_home == null) return;

            // Tiến trình đã nhích từ lúc tô xong, ở đây chỉ còn việc điều hướng.
            var clearedLevel = _levelFlow != null ? _levelFlow.ClearedLevel : -1;

            if (clearedLevel >= 0) _home.ShowCelebrating(clearedLevel);
            else _home.Show();
        }
    }
}
