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
    /// Hiện tại mới có nút đóng. Mọi đường THÊM LƯỢT — xem quảng cáo, đổi bằng tiền, tặng
    /// theo ngày — đều đi qua đúng một cửa là GrantHints, và cửa đó đã sẵn sàng.
    public class HintPopupView : PopupView
    {
        [Tooltip("Số lượt gợi ý còn lại. Thường là 0 lúc popup này mở, nhưng vẫn cập nhật " +
                 "để người chơi thấy con số nhảy lên ngay sau khi nhận thêm lượt.")]
        [SerializeField] private TMP_Text _creditsText;

        [SerializeField] private Button _closeButton;

        private HintCredits _credits;

        /// Nhận HintCredits chứ không nhận IHintService.
        ///
        /// IHintService là contract cho việc DÙNG gợi ý — nó cố tình không có đường thêm
        /// lượt, vì nút gợi ý không được phép tự phát lượt cho mình. Popup này thì ngược
        /// lại: việc duy nhất của nó là thêm lượt. Hai vai trò khác nhau nên nhận hai thứ
        /// khác nhau.
        [Inject]
        public void Construct(HintCredits credits)
        {
            _credits = credits;
        }

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);

            if (_credits != null) _credits.OnCreditsChanged -= SetCredits;
        }

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

        /// Cửa DUY NHẤT để thêm lượt. Nối nút "xem quảng cáo" hoặc "đổi bằng tiền" vào đây.
        ///
        /// Gọi được từ sự kiện OnClick trong Inspector vì nó public và nhận đúng một int —
        /// nên thêm một đường nhận lượt mới không nhất thiết phải sửa file này.
        ///
        /// KHÔNG tự đóng popup: người chơi có thể muốn nhận thêm lần nữa, và quyết định
        /// đóng hay không thuộc về đường nhận lượt cụ thể chứ không thuộc về chỗ cộng số.
        public void GrantHints(int amount)
        {
            if (_credits == null)
            {
                Debug.LogWarning($"{nameof(HintPopupView)} chưa được inject HintCredits — " +
                                 "popup phải do PopupManager tạo qua IObjectResolver, " +
                                 "Object.Instantiate thường thì [Inject] không chạy.", this);
                return;
            }

            _credits.Grant(amount);
        }

        private void SetCredits(int remaining)
        {
            if (_creditsText == null) return;

            _creditsText.SetText("{0}", remaining);
        }
    }
}
