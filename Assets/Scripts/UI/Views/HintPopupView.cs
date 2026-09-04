using DG.Tweening;
using JewelPainter.Gameplay.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup mở khi người chơi bấm nút gợi ý mà đã hết lượt miễn phí.
    ///
    /// HudView mở nó — không đi qua presenter nào, vì popup này bật lên từ chính cái nút
    /// mà HudView đang giữ, và HudView thì luôn sống. Presenter chỉ cần khi popup được mở
    /// bởi một sự kiện mà không ai đang nghe.
    ///
    /// Hai đường nhận lượt hiện có, cả hai đều đổ về GrantHints:
    ///   BuyHintWithCoins — trừ tiền rồi cộng lượt. Không đủ tiền thì lắc nút báo lại.
    ///   GrantFreeHints   — cộng lượt không mất gì. Nối nút xem quảng cáo vào đây.
    public class HintPopupView : PopupView
    {
        [Tooltip("Số lượt gợi ý còn lại. Thường là 0 lúc popup này mở, nhưng vẫn cập nhật " +
                 "để người chơi thấy con số nhảy lên ngay sau khi nhận thêm lượt.")]
        [SerializeField] private TMP_Text _creditsText;

        [SerializeField] private Button _closeButton;

        [Header("Đổi tiền lấy lượt")]
        [Tooltip("Nút mua lượt bằng tiền. Script tự nối vào OnClick lúc Awake — để trống " +
                 "danh sách OnClick trong prefab, gán vào đây là đủ.")]
        [SerializeField] private Button _coinButton;

        [Tooltip("Giá một lần mua, tính bằng tiền. Để 0 là cho không.")]
        [SerializeField] private int _coinCost = 250;

        [Tooltip("Mua một lần được bao nhiêu lượt gợi ý.")]
        [SerializeField] private int _coinHintReward = 1;

        [Tooltip("Nhãn hiện giá. Tuỳ chọn — gán vào thì script tự ghi con số ở Coin Cost " +
                 "lên nhãn lúc Awake, nên giá trên nút không bao giờ lệch với giá thật " +
                 "sự bị trừ. Để trống nếu giá của bạn là ảnh.")]
        [SerializeField] private TMP_Text _coinCostText;

        [Header("Lượt tặng")]
        [Tooltip("Nút nhận lượt không mất tiền — chỗ để nối phần thưởng xem quảng cáo. " +
                 "Cũng tự nối vào OnClick lúc Awake.")]
        [SerializeField] private Button _freeButton;

        [Tooltip("Bấm một lần được bao nhiêu lượt gợi ý.")]
        [SerializeField] private int _freeHintReward = 3;

        [Header("Hiệu ứng báo không đủ tiền")]
        [Tooltip("Thứ bị lắc. Để trống thì lắc chính nút tiền. Gán ô này khi muốn lắc cả " +
                 "khung chứa nút thay vì mỗi cái nút.")]
        [SerializeField] private RectTransform _denyShakeTarget;

        [Tooltip("Thứ loé màu cảnh báo. Để trống thì dùng nhãn giá.\n\n" +
                 "ĐỪNG gán Image của chính cái nút: Button đang để Transition = Color Tint " +
                 "sẽ ghi đè màu ngay khi nhả tay, và cú loé tắt ngóm giữa chừng. Nhãn giá " +
                 "hoặc icon đồng tiền là đích an toàn.")]
        [SerializeField] private Graphic _denyFlashTarget;

        [SerializeField] private Color _denyFlashColor = new Color(1f, 0.32f, 0.32f, 1f);

        [Tooltip("Thời lượng cú lắc, tính bằng giây. 0.4 là đủ để đọc ra 'không' mà chưa " +
                 "kịp thành phiền.")]
        [SerializeField] private float _denyDuration = 0.4f;

        [Tooltip("Biên độ lắc, tính bằng pixel của Canvas. Chỉ lắc NGANG — lắc ngang đọc " +
                 "ra là 'không', lắc dọc đọc ra là 'chú ý', hai nghĩa khác nhau.")]
        [SerializeField] private float _denyShakeStrength = 26f;

        [Tooltip("Số cái đẩy qua lại trong cú lắc. Cao thì run rẩy, thấp thì lừ đừ. " +
                 "12 là ba vòng qua-về trong 0.4 giây — đọc ra là một lời từ chối dứt khoát.")]
        [SerializeField] private int _denyVibrato = 12;

        [Header("Hiệu ứng khi nhận được lượt")]
        [Tooltip("Độ nảy của con số lượt còn lại khi vừa cộng thêm. Để 0 là tắt.")]
        [SerializeField] private float _grantPunchScale = 0.35f;

        [SerializeField] private float _grantPunchDuration = 0.35f;

        private HintCredits _credits;
        private PlayerWallet _wallet;

        /// Lượt hiệu ứng đang chạy. Hai hiệu ứng loại trừ nhau — hoặc mua được, hoặc
        /// không — nên một ô là đủ, và cái sau tự cắt cái trước.
        private Sequence _feedback;

        /// Giá trị gốc để trả về sau mỗi lượt hiệu ứng.
        ///
        /// Chụp NGAY TRƯỚC mỗi lượt chứ không chụp một lần ở Awake: Layout Group có thể
        /// đặt lại vị trí nút sau Awake, và lúc đó cái mốc chụp sớm đã sai — trả về nó là
        /// tự tay đẩy nút lệch đi.
        private Vector2 _shakeBasePosition;
        private Color _flashBaseColor;
        private Vector3 _creditsBaseScale;

        /// Nhận HintCredits chứ không nhận IHintService.
        ///
        /// IHintService là contract cho việc DÙNG gợi ý — nó cố tình không có đường thêm
        /// lượt, vì nút gợi ý không được phép tự phát lượt cho mình. Popup này thì ngược
        /// lại: việc duy nhất của nó là thêm lượt. Hai vai trò khác nhau nên nhận hai thứ
        /// khác nhau.
        [Inject]
        public void Construct(HintCredits credits, PlayerWallet wallet)
        {
            _credits = credits;
            _wallet = wallet;
        }

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_coinButton != null) _coinButton.onClick.AddListener(BuyHintWithCoins);
            if (_freeButton != null) _freeButton.onClick.AddListener(GrantFreeHints);

            if (_denyShakeTarget == null && _coinButton != null)
            {
                _denyShakeTarget = _coinButton.transform as RectTransform;
            }

            if (_denyFlashTarget == null) _denyFlashTarget = _coinCostText;

            // Một nguồn sự thật cho cái giá: ô Inspector. Ghi tay lên nhãn trong prefab
            // rồi đổi giá trong code là kiểu lệch không ai phát hiện cho tới khi người
            // chơi bị trừ một con số khác với con số họ đọc thấy.
            if (_coinCostText != null) _coinCostText.SetText("{0}", Mathf.Max(0, _coinCost));
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
            if (_coinButton != null) _coinButton.onClick.RemoveListener(BuyHintWithCoins);
            if (_freeButton != null) _freeButton.onClick.RemoveListener(GrantFreeHints);

            if (_credits != null) _credits.OnCreditsChanged -= SetCredits;

            StopFeedback();
        }

        /// Popup đóng giữa lúc hiệu ứng đang chạy thì phải trả nút về đúng chỗ, đúng màu.
        ///
        /// DOTween KHÔNG tự dừng tween chỉ vì object bị tắt — không có chỗ này thì lần mở
        /// sau nút nằm lệch sang một bên hoặc còn đỏ, và không ai đoán ra vì sao.
        private void OnDisable() => StopFeedback();

        public override void Show()
        {
            base.Show();

            if (_credits == null) return;

            // Đăng ký ở Show và huỷ ở Hide, không đăng ký một lần ở Awake: popup sống
            // suốt phiên chơi nhưng chỉ hiện vài giây, mà con số chỉ có nghĩa lúc đang hiện.
            _credits.OnCreditsChanged -= SetCredits;
            _credits.OnCreditsChanged += SetCredits;

            SetCredits(_credits.Remaining);
        }

        public override void Hide()
        {
            if (_credits != null) _credits.OnCreditsChanged -= SetCredits;

            base.Hide();
        }

        /// Trừ tiền rồi cộng lượt. Không đủ tiền thì không trừ gì và lắc nút báo lại.
        ///
        /// Đi qua PlayerWallet.TrySpend chứ không tự trừ tay: TrySpend kiểm đủ tiền, trừ,
        /// GHI ĐĨA và bắn sự kiện trong cùng một bước. Tách ba việc đó ra là đường ngắn
        /// nhất tới cảnh tiền đã mất mà lượt chưa cộng, hoặc ngược lại.
        ///
        /// KHÔNG tự đóng popup: người chơi có thể muốn mua thêm lần nữa.
        public void BuyHintWithCoins()
        {
            if (!EnsureCredits()) return;

            if (_wallet == null)
            {
                WarnMissingDependency(nameof(PlayerWallet));
                return;
            }

            var cost = Mathf.Max(0, _coinCost);

            // cost = 0 thì bỏ qua TrySpend hẳn: nó trả false cho mọi số không dương, và
            // đi qua nó là món quà miễn phí biến thành lời từ chối.
            if (cost > 0 && !_wallet.TrySpend(cost))
            {
                PlayDeniedFeedback();
                return;
            }

            // Sàn 1: mua rồi mà nhận 0 lượt thì đó là mất tiền, không phải là mua.
            GrantHints(Mathf.Max(1, _coinHintReward));
        }

        /// Cộng lượt không mất gì. Nối nút xem quảng cáo hoặc quà theo ngày vào đây.
        public void GrantFreeHints() 
        { 
            GrantHints(Mathf.Max(1, _freeHintReward));
            Hide();
        }

        /// Cửa DUY NHẤT để thêm lượt. Mọi đường nhận lượt đều đổ về đây.
        ///
        /// Gọi được từ sự kiện OnClick trong Inspector vì nó public và nhận đúng một int —
        /// nên thêm một đường nhận lượt mới không nhất thiết phải sửa file này.
        ///
        /// KHÔNG tự đóng popup: người chơi có thể muốn nhận thêm lần nữa, và quyết định
        /// đóng hay không thuộc về đường nhận lượt cụ thể chứ không thuộc về chỗ cộng số.
        public void GrantHints(int amount)
        {
            if (!EnsureCredits()) return;

            // Giữ nguyên nghĩa cũ của hàm này: số không dương thì không làm gì. Việc kẹp
            // sàn là của từng đường nhận lượt, không phải của cái cửa chung.
            if (amount <= 0) return;

            _credits.Grant(amount);

            PlayGrantedFeedback();
        }

        private bool EnsureCredits()
        {
            if (_credits != null) return true;

            WarnMissingDependency(nameof(HintCredits));
            return false;
        }

        private void WarnMissingDependency(string what)
        {
            Debug.LogWarning($"{nameof(HintPopupView)} chưa được inject {what} — popup phải " +
                             "do PopupManager tạo qua IObjectResolver, Object.Instantiate " +
                             "thường thì [Inject] không chạy.", this);
        }

        private void SetCredits(int remaining)
        {
            if (_creditsText == null) return;

            _creditsText.SetText("{0}", remaining);
        }

        /// Lắc ngang + loé đỏ. Đủ để nói "không" mà không cần thêm một popup nữa chồng lên.
        private void PlayDeniedFeedback()
        {
            StopFeedback();
            CacheBaseValues();

            var duration = Mathf.Max(0.05f, _denyDuration);

            // SetUpdate(true) — chạy theo thời gian KHÔNG phụ thuộc timeScale. Popup có
            // thể đang mở lúc game bị dừng, và một hiệu ứng đứng hình thì vô nghĩa.
            var sequence = DOTween.Sequence().SetUpdate(true);
            var hasStep = false;

            if (_denyShakeTarget != null)
            {
                var strength = Mathf.Max(0f, _denyShakeStrength);

                // Mỗi vòng sin đưa nút sang phải rồi sang trái rồi về — hai cái đẩy. Chia
                // tư để con số trong Inspector đọc ra là "bao nhiêu cái đẩy", không phải
                // "bao nhiêu vòng".
                var cycles = Mathf.Max(1, _denyVibrato) * 0.25f;

                // Tự lắc bằng DOVirtual.Float chứ KHÔNG dùng DOShakeAnchorPos.
                //
                // DOShakeAnchorPos và Graphic.DOColor nằm trong DOTweenModuleUI.cs — file
                // .cs rời trong Plugins, nên chúng biên dịch vào Assembly-CSharp, mà asmdef
                // JewelPainter.UI không với tới assembly đó. Chỉ DOTween.dll là auto-
                // reference. Muốn dùng phải sinh asmdef cho DOTween rồi khai thêm reference
                // ở ba asmdef — đắt hơn hẳn một công thức sin.
                //
                // Đổi lại còn được cái lợi thật: sóng sin tắt dần là cú lắc ĐỀU và lặp lại
                // y hệt mỗi lần, khác cú lắc ngẫu nhiên của DOTween. Một lời từ chối nên
                // trông giống nhau ở mọi lần bị từ chối.
                sequence.Join(DOVirtual.Float(0f, 1f, duration, t =>
                {
                    if (_denyShakeTarget == null) return;

                    // Biên độ tắt dần tuyến tính: cú lắc dừng lại êm chứ không cụt ngang.
                    var offset = Mathf.Sin(t * cycles * 2f * Mathf.PI) * strength * (1f - t);

                    _denyShakeTarget.anchoredPosition = _shakeBasePosition + new Vector2(offset, 0f);
                }));

                hasStep = true;
            }

            if (_denyFlashTarget != null)
            {
                // Loé rồi trả về ngay trong nửa đầu cú lắc, không kéo hết. Màu đỏ nán lại
                // quá lâu đọc ra như nút bị hỏng chứ không phải như một lời từ chối.
                sequence.Join(DOVirtual.Float(0f, 1f, duration * 0.5f, t =>
                {
                    if (_denyFlashTarget == null) return;

                    // Tam giác 0 → 1 → 0: tới màu cảnh báo rồi quay hẳn về màu gốc trong
                    // đúng một lượt, khỏi cần SetLoops Yoyo.
                    var k = 1f - Mathf.Abs(t * 2f - 1f);

                    _denyFlashTarget.color = Color.Lerp(_flashBaseColor, _denyFlashColor, k);
                }));

                hasStep = true;
            }

            StartFeedback(sequence, hasStep);
        }

        /// Con số lượt còn lại nảy lên một cái. Cộng thêm mà con số đổi lặng lẽ thì người
        /// chơi vừa trả 250 tiền không biết mình đã nhận được gì.
        private void PlayGrantedFeedback()
        {
            if (_creditsText == null || _grantPunchScale <= 0f) return;

            StopFeedback();
            CacheBaseValues();

            var sequence = DOTween.Sequence().SetUpdate(true);

            sequence.Join(_creditsText.transform.DOPunchScale(
                Vector3.one * _grantPunchScale,
                Mathf.Max(0.05f, _grantPunchDuration),
                8,
                0.6f));

            StartFeedback(sequence, true);
        }

        private void StartFeedback(Sequence sequence, bool hasStep)
        {
            if (!hasStep)
            {
                sequence.Kill();
                return;
            }

            // Trả giá trị về gốc ở cuối lượt. Cú lắc sin tắt về 0 và DOPunchScale trên lý
            // thuyết tự về chỗ cũ, nhưng "trên lý thuyết" không đủ cho một thứ mà sai sót
            // biểu hiện thành cái nút nằm lệch vĩnh viễn.
            sequence.OnComplete(RestoreBaseValues);

            _feedback = sequence;
        }

        private void CacheBaseValues()
        {
            if (_denyShakeTarget != null) _shakeBasePosition = _denyShakeTarget.anchoredPosition;
            if (_denyFlashTarget != null) _flashBaseColor = _denyFlashTarget.color;
            if (_creditsText != null) _creditsBaseScale = _creditsText.transform.localScale;
        }

        private void RestoreBaseValues()
        {
            if (_denyShakeTarget != null) _denyShakeTarget.anchoredPosition = _shakeBasePosition;
            if (_denyFlashTarget != null) _denyFlashTarget.color = _flashBaseColor;
            if (_creditsText != null) _creditsText.transform.localScale = _creditsBaseScale;
        }

        private void StopFeedback()
        {
            if (_feedback == null) return;

            if (_feedback.IsActive()) _feedback.Kill();

            _feedback = null;

            RestoreBaseValues();
        }
    }
}
