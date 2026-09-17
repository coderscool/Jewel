using System;
using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Điều phối vòng đời màn chơi.
    public class LevelManager : MonoBehaviour, ILevelService
    {
        [SerializeField] private LevelConfig[] _levels = Array.Empty<LevelConfig>();

        [Tooltip("Phép chỉnh từ màu đất sang màu viên ngọc, dùng chung cho mọi màn.")]
        [SerializeField] private JewelTintConfig _jewelTint;

        private PlayerProgress _progress;
        private LevelConfig _currentConfig;
        private IReadOnlyList<Color32> _jewelColors = Array.Empty<Color32>();

        private int RawLevel => _progress?.Level ?? 0;

        public int CurrentLevel => HasLevel(RawLevel) ? RawLevel : HighestLevelId();
        public LevelConfig CurrentConfig => _currentConfig;

        public LevelGridData CurrentGrid => _currentConfig != null ? _currentConfig.GridData : null;

        public IReadOnlyList<LevelConfig> Levels => _levels;

        public IReadOnlyList<Color32> CurrentJewelColors => _jewelColors;

        /// Màn đã mở khoá chưa.
        public bool IsUnlocked(int levelId) => levelId <= RawLevel;

        /// Màn đã hoàn thành chưa.
        public bool IsCompleted(int levelId) => levelId < RawLevel;

        public event Action<int> OnLevelLoadStarted;
        public event Action<int> OnLevelStarted;
        public event Action<int> OnLevelCompleted;

        private Coroutine _loadRoutine;

        /// Khởi tạo phụ thuộc.
        public void Init(PlayerProgress progress)
        {
            _progress = progress;
        }

        public bool HasLevel(int levelId) => FindConfig(levelId) != null;

        /// Id màn lớn nhất đang khai.
        private int HighestLevelId()
        {
            var highest = 0;

            foreach (var config in _levels)
            {
                if (config != null && config.LevelId > highest) highest = config.LevelId;
            }

            return highest;
        }

        /// Nạp một màn chơi.
        public void LoadLevel(int levelId)
        {
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);

            if (!isActiveAndEnabled)
            {
                Build(levelId);
                return;
            }

            _loadRoutine = StartCoroutine(LoadRoutine(levelId));
        }

        private IEnumerator LoadRoutine(int levelId)
        {
            OnLevelLoadStarted?.Invoke(levelId);

            yield return null;
            yield return null;

            _loadRoutine = null;

            Build(levelId);
        }

        private void Build(int levelId)
        {
            _currentConfig = FindConfig(levelId);

            _jewelColors = BuildJewelColors();

            OnLevelStarted?.Invoke(levelId);
        }

        /// Dựng bảng màu ngọc từ bảng màu đất.
        private IReadOnlyList<Color32> BuildJewelColors()
        {
            var grid = CurrentGrid;
            if (grid == null) return Array.Empty<Color32>();

            var ground = grid.Colors;
            if (_jewelTint == null) return ground;

            var hasTint = !_jewelTint.Tint.IsNone;
            var hasOverrides = _jewelTint.HasOverrides;

            if (!hasTint && !hasOverrides) return ground;

            var jewel = new Color32[ground.Count];

            for (var i = 0; i < jewel.Length; i++)
            {
                var source = ground[i];

                if (_jewelTint.TryGetOverride(source, out var forced))
                {
                    forced.a = source.a;
                    jewel[i] = forced;
                    continue;
                }

                jewel[i] = hasTint ? _jewelTint.Tint.Apply(source) : source;
            }

            return jewel;
        }

        public void CompleteCurrentLevel()
        {
            var finishedLevel = _progress.Level;

            _progress.Advance();

            MarkLevelFinished(finishedLevel);
        }

        /// Chỉ bắn tín hiệu "màn này đã tô xong", không đụng tới tiến trình.
        public void MarkLevelFinished(int levelId) => OnLevelCompleted?.Invoke(levelId);

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
