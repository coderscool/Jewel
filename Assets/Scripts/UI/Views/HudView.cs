using JewelPainter.Gameplay.Interfaces;
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
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Nút gợi ý. Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _hintButton;

        private ILevelService _levelService;
        private IHintService _hintService;
        private int _displayedLevel = -1;

        public void Init(ILevelService levelService, IHintService hintService)
        {
            _levelService = levelService;
            _hintService = hintService;

            _levelService.OnLevelStarted += HandleLevelStarted;
            _hintService.OnHintAvailabilityChanged += SetHintAvailable;

            SetLevel(_levelService.CurrentLevel);

            if (_hintButton == null) return;

            _hintButton.onClick.AddListener(HandleHintClicked);
            SetHintAvailable(_hintService.CanUseHint);
        }

        /// Huỷ đăng ký để tránh gọi vào object đã bị huỷ.
        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_hintService != null) _hintService.OnHintAvailabilityChanged -= SetHintAvailable;
            if (_hintButton != null) _hintButton.onClick.RemoveListener(HandleHintClicked);
        }

        private void HandleLevelStarted(int levelId) => SetLevel(levelId);

        private void HandleHintClicked() => _hintService.UseHint();

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
