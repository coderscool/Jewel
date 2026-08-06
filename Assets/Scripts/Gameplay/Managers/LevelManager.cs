using System;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// MonoBehaviour mỏng: điều phối vòng đời màn chơi.
    /// Toàn bộ luật tiến trình nằm ở PlayerProgress (thuần C#).
    public class LevelManager : MonoBehaviour, ILevelService
    {
        [SerializeField] private LevelConfig[] _levels = Array.Empty<LevelConfig>();

        private PlayerProgress _progress;
        private LevelConfig _currentConfig;

        public int CurrentLevel => _progress?.Level ?? 0;
        public LevelConfig CurrentConfig => _currentConfig;

        // Không dùng `_currentConfig?.GridData` — LevelConfig là UnityEngine.Object,
        // toán tử ?. bỏ qua phép so sánh null của Unity nên object đã huỷ vẫn lọt qua.
        public LevelGridData CurrentGrid => _currentConfig != null ? _currentConfig.GridData : null;

        public event Action<int> OnLevelStarted;
        public event Action<int> OnLevelCompleted;

        /// Bootstrap đưa phụ thuộc xuống — không tự đi tìm.
        public void Init(PlayerProgress progress)
        {
            _progress = progress;
        }

        public bool HasLevel(int levelId) => FindConfig(levelId) != null;

        public void LoadLevel(int levelId)
        {
            _currentConfig = FindConfig(levelId);
            OnLevelStarted?.Invoke(levelId);
        }

        public void CompleteCurrentLevel()
        {
            var finishedLevel = _progress.Level;
            _progress.Advance();
            OnLevelCompleted?.Invoke(finishedLevel);
        }

        private LevelConfig FindConfig(int levelId)
        {
            foreach (var config in _levels)
            {
                if (config != null && config.LevelId == levelId) return config;
            }

            return null;
        }
    }
}
