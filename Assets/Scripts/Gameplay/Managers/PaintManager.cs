using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// MonoBehaviour mỏng: giữ PaintState và phát sự kiện.
    /// Toàn bộ luật tô nằm ở PaintState (thuần C#).
    public class PaintManager : MonoBehaviour, IPaintService
    {
        private static readonly int[] NoIndices = Array.Empty<int>();

        private ILevelService _levelService;
        private PaintState _state;

        public int SelectedPaletteIndex { get; private set; } = -1;

        public IReadOnlyList<int> UsedPaletteIndices =>
            _state != null ? _state.UsedPaletteIndices : NoIndices;

        public event Action OnBoardReady;
        public event Action<int> OnColorSelected;
        public event Action<Vector2Int, int> OnCellPainted;

        /// Bootstrap đưa phụ thuộc xuống — không tự đi tìm.
        public void Init(ILevelService levelService)
        {
            _levelService = levelService;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId)
        {
            _state = null;
            SelectedPaletteIndex = -1;

            var data = _levelService.CurrentGrid;
            var grid = data != null ? data.ToGrid() : null;

            if (grid != null) _state = new PaintState(grid);

            OnBoardReady?.Invoke();
        }

        public void SelectColor(int paletteIndex)
        {
            if (_state == null) return;
            if (paletteIndex == SelectedPaletteIndex) return;
            if (!_state.IsUsed(paletteIndex)) return;

            SelectedPaletteIndex = paletteIndex;
            OnColorSelected?.Invoke(paletteIndex);
        }

        public bool CanPaint(int x, int y)
        {
            if (_state == null) return false;
            if (SelectedPaletteIndex < 0) return false;

            return _state.CanPaint(x, y, SelectedPaletteIndex);
        }

        public bool TryPaint(int x, int y)
        {
            if (_state == null) return false;
            if (SelectedPaletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, SelectedPaletteIndex)) return false;

            OnCellPainted?.Invoke(new Vector2Int(x, y), SelectedPaletteIndex);
            return true;
        }

        public bool IsPainted(int x, int y)
        {
            return _state != null && _state.IsPainted(x, y);
        }

        public bool IsComplete => _state != null && _state.IsComplete;

        public int RemainingFor(int paletteIndex)
        {
            return _state != null ? _state.RemainingFor(paletteIndex) : 0;
        }

        public bool TryGetUnpaintedCell(int paletteIndex, int ordinal, out Vector2Int cell)
        {
            cell = default;

            return _state != null && _state.TryGetUnpainted(paletteIndex, ordinal, out cell);
        }

        public float ProgressFor(int paletteIndex)
        {
            return _state != null ? _state.ProgressFor(paletteIndex) : 0f;
        }
    }
}
