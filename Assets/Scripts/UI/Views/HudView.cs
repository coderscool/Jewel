using JewelPainter.Gameplay.Interfaces;
using TMPro;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// View thuần trình bày: nghe ILevelService, đổ chữ ra màn hình.
    /// Chỉ SetText khi giá trị thật sự đổi — SetText mỗi frame sinh rác GC.
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;

        private ILevelService _levelService;
        private int _displayedLevel = -1;

        public void Init(ILevelService levelService)
        {
            _levelService = levelService;
            _levelService.OnLevelStarted += HandleLevelStarted;

            SetLevel(_levelService.CurrentLevel);
        }

        /// Huỷ đăng ký để tránh gọi vào object đã bị huỷ.
        private void OnDestroy()
        {
            if (_levelService == null) return;

            _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId) => SetLevel(levelId);

        private void SetLevel(int level)
        {
            if (level == _displayedLevel) return;

            _displayedLevel = level;
            _levelText.SetText("Level {0}", level);
        }
    }
}
