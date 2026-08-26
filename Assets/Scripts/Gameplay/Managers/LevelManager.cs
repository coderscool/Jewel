using System;
using System.Collections.Generic;
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

        [Tooltip("Phép chỉnh từ màu đất sang màu viên ngọc, dùng chung cho mọi màn. " +
                 "Bỏ trống thì ngọc mang đúng màu đất.\n\n" +
                 "Dò bộ số bằng JewelPainter > Chỉnh màu viên ngọc.")]
        [SerializeField] private JewelTintConfig _jewelTint;

        private PlayerProgress _progress;
        private LevelConfig _currentConfig;
        private IReadOnlyList<Color32> _jewelColors = Array.Empty<Color32>();

        public int CurrentLevel => _progress?.Level ?? 0;
        public LevelConfig CurrentConfig => _currentConfig;

        // Không dùng `_currentConfig?.GridData` — LevelConfig là UnityEngine.Object,
        // toán tử ?. bỏ qua phép so sánh null của Unity nên object đã huỷ vẫn lọt qua.
        public LevelGridData CurrentGrid => _currentConfig != null ? _currentConfig.GridData : null;

        public IReadOnlyList<LevelConfig> Levels => _levels;

        public IReadOnlyList<Color32> CurrentJewelColors => _jewelColors;

        /// Mở khoá theo tiến trình, không lưu riêng cờ cho từng màn: game chỉ chơi tuần
        /// tự nên "đã tới màn 5" đã nói đủ rằng 1–4 xong rồi.
        public bool IsUnlocked(int levelId) => levelId <= CurrentLevel;

        /// Tiến trình chỉ nhích khi một màn tô xong, nên "đứng trước màn hiện tại" đã đủ
        /// nghĩa là "đã hoàn thành" — không cần lưu riêng cờ cho từng màn.
        public bool IsCompleted(int levelId) => levelId < CurrentLevel;

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

            // Dựng TRƯỚC khi bắn event: BoardView và ColorPaletteBar đều đọc bảng này
            // ngay trong lượt xử lý OnLevelStarted.
            _jewelColors = BuildJewelColors();

            OnLevelStarted?.Invoke(levelId);
        }

        /// Trả thẳng bảng màu đất khi không có gì để chỉnh — không copy một mảng chỉ để
        /// nó giống hệt mảng gốc.
        private IReadOnlyList<Color32> BuildJewelColors()
        {
            var grid = CurrentGrid;
            if (grid == null) return Array.Empty<Color32>();

            var ground = grid.Colors;
            if (_jewelTint == null || _jewelTint.Tint.IsNone) return ground;

            var jewel = new Color32[ground.Count];
            for (var i = 0; i < jewel.Length; i++) jewel[i] = _jewelTint.Tint.Apply(ground[i]);

            return jewel;
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
