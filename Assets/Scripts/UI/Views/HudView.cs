using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// View thuần trình bày: nghe ILevelService, đổ chữ ra màn hình.
    /// Chỉ SetText khi giá trị thật sự đổi — SetText mỗi frame sinh rác GC.
    ///
    /// Nút gợi ý ở đây chỉ làm hai việc: bấm thì gọi IHintService, và tự bật/tắt theo
    /// tín hiệu của nó. Tìm ô nào, đưa camera đi đâu là chuyện của Gameplay.
    ///
    /// Cả HUD tự ẩn khi thắng màn và hiện lại khi màn mới bắt đầu, để popup thắng màn
    /// đứng một mình trên bức tranh vừa hoàn thành.
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Nút gợi ý. Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _hintButton;

        [Tooltip("Nút mở popup bộ sưu tập. Để trống cũng chạy.")]
        [SerializeField] private Button _collectionButton;

        [Tooltip("Object bị ẩn khi thắng màn. Để TRỐNG thì ẩn chính object này — cách " +
                 "đó vẫn chạy đúng, chỉ là không tách được phần nào của HUD ở lại.")]
        [SerializeField] private GameObject _content;

        private ILevelService _levelService;
        private IHintService _hintService;
        private IPopupService _popupService;
        private ILevelFlowService _levelFlow;
        private int _displayedLevel = -1;

        public void Init(
            ILevelService levelService,
            IHintService hintService,
            IPopupService popupService,
            ILevelFlowService levelFlow)
        {
            _levelService = levelService;
            _hintService = hintService;
            _popupService = popupService;
            _levelFlow = levelFlow;

            _levelService.OnLevelStarted += HandleLevelStarted;
            _hintService.OnHintAvailabilityChanged += SetHintAvailable;
            _levelFlow.OnLevelCleared += HandleLevelCleared;

            SetLevel(_levelService.CurrentLevel);

            if (_collectionButton != null) _collectionButton.onClick.AddListener(HandleCollectionClicked);

            if (_hintButton == null) return;

            _hintButton.onClick.AddListener(HandleHintClicked);
            SetHintAvailable(_hintService.CanUseHint);
        }

        /// Huỷ đăng ký để tránh gọi vào object đã bị huỷ.
        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_hintService != null) _hintService.OnHintAvailabilityChanged -= SetHintAvailable;
            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
            if (_hintButton != null) _hintButton.onClick.RemoveListener(HandleHintClicked);
            if (_collectionButton != null) _collectionButton.onClick.RemoveListener(HandleCollectionClicked);
        }

        private void HandleLevelStarted(int levelId)
        {
            SetVisible(true);
            SetLevel(levelId);
        }

        private void HandleLevelCleared() => SetVisible(false);

        /// Ẩn bằng SetActive chứ không đổi alpha: HUD tắt hẳn thì nút gợi ý cũng không
        /// còn nhận được cú chạm nào, khỏi phải nhớ khoá riêng từng nút.
        ///
        /// Ẩn chính object này vẫn an toàn dù handler nằm trên nó: sự kiện C# giữ tham
        /// chiếu tới instance, nên hàm vẫn chạy khi GameObject đang tắt — đó là cách
        /// HUD tự bật lại được ở màn sau.
        private void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private void HandleHintClicked() => _hintService.UseHint();

        private void HandleCollectionClicked() => _popupService.Show(PopupKey.Collection);

        /// Chưa chọn màu, hoặc màu đang chọn đã tô hết, thì nút xám đi — bấm vào không
        /// có gì xảy ra mà người chơi lại tưởng game đứng.
        private void SetHintAvailable(bool available)
        {
            if (_hintButton == null) return;

            _hintButton.interactable = available;
        }

        private void SetLevel(int level)
        {
            if (level == _displayedLevel) return;

            _displayedLevel = level;
            _levelText.SetText("Level {0}", level);
        }
    }
}
