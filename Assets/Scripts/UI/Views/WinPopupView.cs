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

        [Tooltip("Pháo hoa ăn mừng NẰM TRONG chính prefab popup — kéo victory_1 vào đây. " +
                 "Để trống thì bỏ qua.\n\n" +
                 "Nhớ TẮT Play On Awake trên hệ hạt đó. Bật thì nó bắn đúng một lần vào " +
                 "lúc popup được tạo — tức là lần đầu popup mở, còn những lần sau im lặng. " +
                 "Popup sống suốt phiên chơi chứ không sinh lại mỗi màn.\n\n" +
                 "Nếu hệ hạt dùng Renderer kiểu Sprite/Mesh thường (không phải UI) thì nó " +
                 "vẽ trong world chứ không theo Canvas — lúc đó Sorting Layer và Order phải " +
                 "cao hơn mọi thứ, hoặc đặt Canvas của popup sang Screen Space - Camera.")]
        [SerializeField] private ParticleSystem _victoryEffect;

        [Tooltip("Chờ ngần này giây sau khi popup hiện rồi mới bắn pháo hoa.")]
        [SerializeField] private float _victoryDelay;

        [Header("Tiền thưởng")]
        [SerializeField] private CoinFlyVFX _coinFly;

        [Tooltip("Coin bắn ra từ đây — thường là chỗ dòng chữ Reward.")]
        [SerializeField] private RectTransform _coinFrom;

        [Tooltip("Coin bay tới đây — icon tiền ở góc trên.")]
        [SerializeField] private RectTransform _coinTarget;

        [Tooltip("Dòng 'Reward: 10'.")]
        [SerializeField] private TMP_Text _rewardText;

        [Tooltip("Tổng tiền hiện ở góc trên. Tăng dần theo từng coin bay tới.")]
        [SerializeField] private Text _coinTotalText;

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

            PlayVictoryEffect();

            PlayShowSequence(reward);
        }

        /// Bắn pháo hoa nằm sẵn trong popup.
        ///
        /// Gọi ở Show chứ không nhờ WinCelebration bên Gameplay: hệ hạt nằm TRONG prefab
        /// popup, mà popup thì đang tắt suốt cho tới đúng lúc này. Lớp bên kia có giữ
        /// tham chiếu tới nó cũng không gọi Play() được — Play() trên một object đang tắt
        /// thì không có gì xảy ra.
        ///
        /// Đứng ở đây nhịp cũng đúng: popup mở sau khi ô cuối đáp xuống một quãng
        /// (LevelFlowController → Popup Delay Seconds), mà quãng đó vốn đã dài hơn cả dải
        /// quét lấp lánh. Loé xong rồi mới tới pháo hoa.
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
            // Thời gian KHÔNG phụ thuộc timeScale, giống mọi hiệu ứng khác của popup này.
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

            // Bật lại object phòng khi nó được để TẮT sẵn trong prefab — cách dựng rất
            // thường gặp, và Play() trên object đang tắt thì im lặng không làm gì.
            if (!_victoryEffect.gameObject.activeSelf) _victoryEffect.gameObject.SetActive(true);

            // withChildren = true ở cả Clear lẫn Play: pháo hoa gần như luôn là một chùm
            // hệ hạt con, mà hệ gốc thường lại rỗng, chỉ dùng để gom nhóm. Gọi không kèm
            // children là chạy đúng cái hệ rỗng đó.
            _victoryEffect.Clear(true);
            _victoryEffect.Play(true);
        }

        public override void Hide()
        {
            // Dập pháo hoa trước khi popup tan: hạt còn lơ lửng trên một popup đã ẩn sẽ
            // hiện lại nguyên si ở lần mở sau, vì popup này không bị huỷ giữa các màn.
            if (_victoryEffect != null)
            {
                _victoryEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

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

                    // Mỗi đồng chạm đích một tiếng. Đợt coin bắn dày nên tiếng này cũng
                    // cần Min Interval và Pitch Variance trong SoundConfig, y như Pop.
                    if (Sound != null) Sound.Play(SoundKey.Coin);

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

            _coinTotalText.text = string.Format("{0}", value);
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

        /// Popup TAN DẦN trong đúng khoảng lặng trước khi Home hiện ra.
        ///
        /// Ba cách đều đã thử, và đây là cách duy nhất không có tì vết:
        ///
        ///   ẩn ngay rồi mới mở Home — ba trạng thái nối đuôi: có popup, KHÔNG CÓ GÌ, có
        ///     Home. Cái ở giữa là một khoảng trống mà mắt đọc thành cú giật.
        ///   để nguyên cho Home phủ lên — nền Home không đục tuyệt đối nên popup lấp ló
        ///     xuyên qua, rồi biến mất phựt một cái khi Home vào xong. Tệ hơn cả cách đầu.
        ///   tan dần đúng bằng Enter Delay — popup về 0 ĐÚNG LÚC Home bắt đầu hiện. Không
        ///     có khoảng trống, không có lúc nào hai màn hình cùng trên màn.
        private void HandleContinueClicked()
        {
            if (Sound != null) Sound.Play(SoundKey.Direction);

            if (_home == null)
            {
                // HideSilently ở cả hai đường ra: popup này không bao giờ bị "huỷ", nó chỉ
                // được đi tiếp. Cú bấm đã có tiếng Direction rồi.
                HideSilently();
                return;
            }

            // Khoá chạm NGAY mà không ẩn. Popup vẫn còn trên màn suốt lúc Home đi vào,
            // nên không khoá thì bấm Continue lần nữa sẽ chạy lại cả đoạn này.
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            // Tiến trình đã nhích từ lúc tô xong, ở đây chỉ còn việc điều hướng.
            //
            // ShowCelebrating tự gọi Show bên trong, nên KHÔNG được gọi Show thêm lần nữa
            // ở đây: hai đường cùng dựng lại danh sách, và cú thứ hai giật ngang cú thứ
            // nhất ngay giữa lúc hiệu ứng bay đang chạy.
            //
            // ClearedLevel còn giá trị tới tận đây — nó chỉ bị đặt lại khi màn kế tiếp
            // bắt đầu, mà lúc này người chơi còn chưa bấm Play.
            var clearedLevel = _levelFlow != null ? _levelFlow.ClearedLevel : -1;

            if (clearedLevel >= 0) _home.ShowCelebrating(clearedLevel);
            else _home.Show();

            StartCoroutine(FadeOutBeforeHomeEnters());
        }

        /// Tan dần về 0 trong đúng khoảng lặng của Home, rồi mới tắt hẳn.
        ///
        /// Home để trống Fade Group thì EnterDelaySeconds bằng 0 và hàm này tắt popup
        /// ngay — đúng bằng hành vi cũ, không cần cấu hình gì thêm.
        private IEnumerator FadeOutBeforeHomeEnters()
        {
            var duration = _home != null ? _home.EnterDelaySeconds : 0f;

            if (duration > 0f)
            {
                var from = CanvasGroup.alpha;
                var elapsed = 0f;

                // Thời gian KHÔNG phụ thuộc timeScale, giống mọi hiệu ứng khác của popup
                // này — game có thể đang dừng lúc popup mở.
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;

                    CanvasGroup.alpha = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                CanvasGroup.alpha = 0f;
            }

            // Hide vẫn chạy lượt mờ của riêng nó, nhưng alpha đã là 0 nên không ai thấy.
            // Việc còn lại của nó mới là thứ cần: tắt object, dọn tween, trả băng về chỗ.
            HideSilently();
        }
    }
}
