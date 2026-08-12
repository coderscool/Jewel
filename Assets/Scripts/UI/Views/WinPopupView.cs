using JewelPainter.Gameplay.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup hiện ra khi tô xong bức tranh. Một nút duy nhất: sang màn kế.
    ///
    /// Popup được PopupManager tạo qua IObjectResolver nên [Inject] ở đây chạy được —
    /// Object.Instantiate thường thì không.
    public class WinPopupView : PopupView
    {
        [Tooltip("Nút sang màn kế. Tự ẩn khi đang ở màn cuối.")]
        [SerializeField] private Button _nextButton;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Dòng chữ số màn vừa hoàn thành.")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Object hiện thay cho nút khi đã hết màn, ví dụ dòng 'Hết màn rồi'.")]
        [SerializeField] private GameObject _lastLevelNotice;

        [Tooltip("Nút đóng popup mà không đi tiếp. Để trống thì chỉ có nút sang màn kế.")]
        [SerializeField] private Button _closeButton;

        private ILevelService _levelService;
        private ILevelFlowService _levelFlow;

        [Inject]
        public void Construct(ILevelService levelService, ILevelFlowService levelFlow)
        {
            _levelService = levelService;
            _levelFlow = levelFlow;
        }

        private void Awake()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(HandleNextClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_nextButton != null) _nextButton.onClick.RemoveListener(HandleNextClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// Dựng lại nội dung ở MỖI lần mở, không phải ở Awake: popup sống suốt phiên
        /// chơi và được tái dùng cho mọi màn.
        public override void Show()
        {
            base.Show();

            var isLastLevel = _levelFlow != null && _levelFlow.IsLastLevel;

            if (_nextButton != null) _nextButton.gameObject.SetActive(!isLastLevel);
            if (_lastLevelNotice != null) _lastLevelNotice.SetActive(isLastLevel);

            if (_levelText != null && _levelService != null)
            {
                _levelText.SetText("Level {0}", _levelService.CurrentLevel);
            }
        }

        /// Ẩn TRƯỚC khi nạp màn mới: nạp màn kéo theo cả loạt sự kiện dựng lại bảng,
        /// để popup còn đứng đó thì người chơi thấy nó nằm trên một bức tranh đã đổi.
        private void HandleNextClicked()
        {
            Hide();

            _levelFlow?.GoToNextLevel();
        }
    }
}
