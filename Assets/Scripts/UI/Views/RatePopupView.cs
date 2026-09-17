using JewelPainter.Gameplay.Domain;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup mời người chơi đánh giá.
    public class RatePopupView : PopupView
    {
        [SerializeField] private Button _rateButton;

        [Tooltip("Nút bỏ qua.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("Trang cửa hàng mở ra khi bấm đánh giá.")]
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

        /// Ghi nhận đã đánh giá rồi mở trang cửa hàng.
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
