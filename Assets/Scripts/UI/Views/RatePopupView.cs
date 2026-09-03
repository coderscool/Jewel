using JewelPainter.Gameplay.Domain;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup mời người chơi đánh giá.
    ///
    /// RatePopupPresenter quyết định KHI NÀO mở; view này chỉ lo hai cái nút.
    ///
    /// Bấm đánh giá là chốt vĩnh viễn: RatePrompt ghi cờ và từ đó không ai mở popup này
    /// nữa. Bấm đóng thì không ghi gì, nên xong thêm mấy màn nữa sẽ được hỏi lại.
    public class RatePopupView : PopupView
    {
        [SerializeField] private Button _rateButton;

        [Tooltip("Nút bỏ qua. Đóng popup mà KHÔNG ghi cờ — lần sau vẫn được mời.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("Trang cửa hàng mở ra khi bấm đánh giá.\n\n" +
                 "Để TRỐNG thì chỉ ghi cờ và đóng popup. Tiện lúc còn đang làm: chưa có " +
                 "app id thật mà mở link thì người chơi rơi vào một trang lỗi.\n\n" +
                 "Android: market://details?id=com.congty.tengame\n" +
                 "iOS: itms-apps://itunes.apple.com/app/id123456789")]
        [SerializeField] private string _storeUrl;

        private RatePrompt _prompt;

        [Inject]
        public void Construct(RatePrompt prompt) => _prompt = prompt;

        private void Awake()
        {
            if (_rateButton != null) _rateButton.onClick.AddListener(HandleRateClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_rateButton != null) _rateButton.onClick.RemoveListener(HandleRateClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// Ghi cờ TRƯỚC, mở link SAU.
        ///
        /// Application.OpenURL đưa người chơi ra khỏi game, và trên nhiều máy Unity bị
        /// tạm dừng ngay tại dòng đó. Ghi cờ sau lời gọi ấy là đặt cược rằng game còn
        /// chạy tiếp — thua cược thì người chơi đã đánh giá rồi mà lần sau vẫn bị hỏi.
        private void HandleRateClicked()
        {
            if (_prompt == null)
            {
                Debug.LogWarning($"{nameof(RatePopupView)} chưa được inject {nameof(RatePrompt)} — " +
                                 "popup phải do PopupManager tạo qua IObjectResolver, " +
                                 "Object.Instantiate thường thì [Inject] không chạy.", this);
            }
            else
            {
                _prompt.MarkRated();
            }

            Hide();

            if (!string.IsNullOrWhiteSpace(_storeUrl)) Application.OpenURL(_storeUrl);
        }
    }
}
