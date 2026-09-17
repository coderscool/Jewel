using DG.Tweening;
using JewelPainter.Gameplay.Domain;
using JewelPainter.UI.Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup mở khi người chơi bấm một nút booster mà đã hết lượt miễn phí.
    public class CreditPopupView : PopupView
    {
        [Tooltip("Kho lượt mà popup này bán.")]
        [SerializeField] private CreditPoolKind _pool = CreditPoolKind.Hint;

        [Tooltip("Số lượt còn lại của kho đang chọn.")]
        [SerializeField] private TMP_Text _creditsText;

        [SerializeField] private Button _closeButton;

        [Header("Đổi tiền lấy lượt")]
        [Tooltip("Nút mua lượt bằng tiền.")]
        [SerializeField] private Button _coinButton;

        [Tooltip("Giá một lần mua, tính bằng tiền.")]
        [SerializeField] private int _coinCost = 250;

        [Tooltip("Mua một lần được bao nhiêu lượt.")]
        [FormerlySerializedAs("_coinHintReward")]
        [SerializeField] private int _coinCreditReward = 1;

        [Tooltip("Nhãn hiện giá.")]
        [SerializeField] private TMP_Text _coinCostText;

        [Header("Lượt tặng")]
        [Tooltip("Nút nhận lượt miễn phí.")]
        [SerializeField] private Button _freeButton;

        [Tooltip("Bấm một lần được bao nhiêu lượt.")]
        [FormerlySerializedAs("_freeHintReward")]
        [SerializeField] private int _freeCreditReward = 3;

        [Header("Hiệu ứng báo không đủ tiền")]
        [Tooltip("Object bị lắc khi không đủ tiền.")]
        [SerializeField] private RectTransform _denyShakeTarget;

        [Tooltip("Object loé màu cảnh báo khi không đủ tiền.")]
        [SerializeField] private Graphic _denyFlashTarget;

        [SerializeField] private Color _denyFlashColor = new Color(1f, 0.32f, 0.32f, 1f);

        [Tooltip("Thời lượng cú lắc, tính bằng giây.")]
        [SerializeField] private float _denyDuration = 0.4f;

        [Tooltip("Biên độ lắc, tính bằng pixel của Canvas.")]
        [SerializeField] private float _denyShakeStrength = 26f;

        [Tooltip("Số cái đẩy qua lại trong cú lắc.")]
        [SerializeField] private int _denyVibrato = 12;

        [Header("Hiệu ứng khi nhận được lượt")]
        [Tooltip("Độ nảy của con số lượt còn lại khi vừa cộng thêm.")]
        [SerializeField] private float _grantPunchScale = 0.35f;

        [SerializeField] private float _grantPunchDuration = 0.35f;

        private CreditPool _credits;
        private PlayerWallet _wallet;

        private Sequence _feedback;

        private Vector2 _shakeBasePosition;
        private Color _flashBaseColor;
        private Vector3 _creditsBaseScale;

        /// Nhận các kho lượt cần dùng.
        [Inject]
        public void Construct(
            HintCredits hintCredits,
            FreePaintCredits freePaintCredits,
            FillColorCredits fillColorCredits,
            PlayerWallet wallet)
        {
            _credits = _pool switch
            {
                CreditPoolKind.FreePaint => freePaintCredits,
                CreditPoolKind.FillColor => fillColorCredits,
                _ => hintCredits,
            };

            _wallet = wallet;
        }

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_coinButton != null) _coinButton.onClick.AddListener(BuyWithCoins);
            if (_freeButton != null) _freeButton.onClick.AddListener(GrantFreeCredits);

            if (_denyShakeTarget == null && _coinButton != null)
            {
                _denyShakeTarget = _coinButton.transform as RectTransform;
            }

            if (_denyFlashTarget == null) _denyFlashTarget = _coinCostText;

            if (_coinCostText != null) _coinCostText.SetText("{0}", Mathf.Max(0, _coinCost));
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
            if (_coinButton != null) _coinButton.onClick.RemoveListener(BuyWithCoins);
            if (_freeButton != null) _freeButton.onClick.RemoveListener(GrantFreeCredits);

            if (_credits != null) _credits.OnCreditsChanged -= SetCredits;

            StopFeedback();
        }

        /// Trả nút về trạng thái gốc khi popup đóng.
        private void OnDisable() => StopFeedback();

        public override void Show()
        {
            base.Show();

            if (_credits == null) return;

            _credits.OnCreditsChanged -= SetCredits;
            _credits.OnCreditsChanged += SetCredits;

            SetCredits(_credits.Remaining);
        }

        public override void Hide()
        {
            if (_credits != null) _credits.OnCreditsChanged -= SetCredits;

            base.Hide();
        }

        /// Trừ tiền rồi cộng lượt.
        public void BuyWithCoins()
        {
            if (!EnsureCredits()) return;

            if (_wallet == null)
            {
                WarnMissingDependency(nameof(PlayerWallet));
                return;
            }

            var cost = Mathf.Max(0, _coinCost);

            if (cost > 0 && !_wallet.TrySpend(cost))
            {
                PlayDeniedFeedback();
                return;
            }

            GrantCredits(Mathf.Max(1, _coinCreditReward));
        }

        /// Cộng lượt không mất gì.
        public void GrantFreeCredits()
        {
            GrantCredits(Mathf.Max(1, _freeCreditReward));
            Hide();
        }

        /// Cộng lượt vào kho đang chọn.
        public void GrantCredits(int amount)
        {
            if (!EnsureCredits()) return;

            if (amount <= 0) return;

            _credits.Grant(amount);

            PlayGrantedFeedback();
        }

        private bool EnsureCredits()
        {
            if (_credits != null) return true;

            WarnMissingDependency($"kho lượt {_pool}");
            return false;
        }

        private void WarnMissingDependency(string what)
        {
            Debug.LogWarning($"{nameof(CreditPopupView)} chưa được inject {what} — popup phải " +
                             "do PopupManager tạo qua IObjectResolver, Object.Instantiate " +
                             "thường thì [Inject] không chạy.", this);
        }

        private void SetCredits(int remaining)
        {
            if (_creditsText == null) return;

            _creditsText.SetText("{0}", remaining);
        }

        /// Hiệu ứng lắc và loé đỏ khi không đủ tiền.
        private void PlayDeniedFeedback()
        {
            StopFeedback();
            CacheBaseValues();

            var duration = Mathf.Max(0.05f, _denyDuration);

            var sequence = DOTween.Sequence().SetUpdate(true);
            var hasStep = false;

            if (_denyShakeTarget != null)
            {
                var strength = Mathf.Max(0f, _denyShakeStrength);

                var cycles = Mathf.Max(1, _denyVibrato) * 0.25f;

                sequence.Join(DOVirtual.Float(0f, 1f, duration, t =>
                {
                    if (_denyShakeTarget == null) return;

                    var offset = Mathf.Sin(t * cycles * 2f * Mathf.PI) * strength * (1f - t);

                    _denyShakeTarget.anchoredPosition = _shakeBasePosition + new Vector2(offset, 0f);
                }));

                hasStep = true;
            }

            if (_denyFlashTarget != null)
            {
                sequence.Join(DOVirtual.Float(0f, 1f, duration * 0.5f, t =>
                {
                    if (_denyFlashTarget == null) return;

                    var k = 1f - Mathf.Abs(t * 2f - 1f);

                    _denyFlashTarget.color = Color.Lerp(_flashBaseColor, _denyFlashColor, k);
                }));

                hasStep = true;
            }

            StartFeedback(sequence, hasStep);
        }

        /// Con số lượt còn lại nảy lên một cái.
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
