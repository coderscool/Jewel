using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Giữ trạng thái tô và phát sự kiện tô màu.
    public class PaintManager : MonoBehaviour, IPaintService
    {
        private static readonly int[] NoIndices = Array.Empty<int>();

        private ILevelService _levelService;
        private PaintProgressStore _progressStore;
        private PaintState _state;

        private int _loadedLevel = -1;

        public int SelectedPaletteIndex { get; private set; } = -1;

        public IReadOnlyList<int> UsedPaletteIndices =>
            _state != null ? _state.UsedPaletteIndices : NoIndices;

        public event Action OnBoardReady;
        public event Action<int> OnColorSelected;
        public event Action<int> OnColorFocusRequested;
        public event Action<Vector2Int, int> OnCellPainted;
        public event Action OnColorRequired;
        public event Action<bool> OnFreePaintChanged;
        public event Action<bool> OnColorLockChanged;

        private bool _fillLock;

        private bool _lastColorLock;

        public bool FreePaintActive { get; private set; }

        /// Bật/tắt luật tô tự do.
        public void SetFreePaint(bool active)
        {
            if (FreePaintActive == active) return;

            FreePaintActive = active;
            OnFreePaintChanged?.Invoke(active);

            RefreshColorLock();
        }

        public bool ColorLocked => FreePaintActive || _fillLock;

        /// Bật tắt khoá màu trong lúc booster tô hết màu chạy.
        public void SetFillLock(bool locked)
        {
            if (_fillLock == locked) return;

            _fillLock = locked;

            RefreshColorLock();
        }

        private void RefreshColorLock()
        {
            var locked = ColorLocked;
            if (locked == _lastColorLock) return;

            _lastColorLock = locked;
            OnColorLockChanged?.Invoke(locked);
        }

        public void RequireColor()
        {
            if (FreePaintActive) return;

            if (SelectedPaletteIndex >= 0) return;

            if (_state == null || _state.IsComplete) return;

            OnColorRequired?.Invoke();
        }

        /// Khởi tạo phụ thuộc.
        public void Init(ILevelService levelService, PaintProgressStore progressStore)
        {
            _levelService = levelService;
            _progressStore = progressStore;

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
            _loadedLevel = levelId;

            SetFreePaint(false);

            SetFillLock(false);

            var data = _levelService.CurrentGrid;
            var grid = data != null ? data.ToGrid() : null;

            if (grid != null) _state = new PaintState(grid);

            if (_state != null)
            {
                var restored = _progressStore != null && _progressStore.Restore(levelId, _state);

                if (!restored && _levelService.IsCompleted(levelId)) _state.PaintAll();
            }

            OnBoardReady?.Invoke();
        }

        public void SelectColor(int paletteIndex)
        {
            if (_state == null) return;

            if (ColorLocked) return;
            if (!_state.IsUsed(paletteIndex)) return;

            OnColorFocusRequested?.Invoke(paletteIndex);

            if (paletteIndex == SelectedPaletteIndex) return;

            SelectedPaletteIndex = paletteIndex;
            OnColorSelected?.Invoke(paletteIndex);
        }

        public bool CanPaint(int x, int y)
        {
            if (_state == null) return false;

            if (FreePaintActive) return _state.CanPaintAny(x, y);

            if (SelectedPaletteIndex < 0) return false;

            return _state.CanPaint(x, y, SelectedPaletteIndex);
        }

        public bool TryPaint(int x, int y)
        {
            if (_state == null) return false;

            if (FreePaintActive)
            {
                if (!_state.TryPaintAny(x, y, out var painted)) return false;

                _progressStore?.MarkDirty();

                OnCellPainted?.Invoke(new Vector2Int(x, y), painted);
                return true;
            }

            if (SelectedPaletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, SelectedPaletteIndex)) return false;

            _progressStore?.MarkDirty();

            OnCellPainted?.Invoke(new Vector2Int(x, y), SelectedPaletteIndex);
            return true;
        }

        /// Tô một ô bằng một màu chỉ định, không hỏi màu đang chọn.
        public bool TryPaintAs(int x, int y, int paletteIndex)
        {
            if (_state == null) return false;
            if (paletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, paletteIndex)) return false;

            _progressStore?.MarkDirty();

            OnCellPainted?.Invoke(new Vector2Int(x, y), paletteIndex);
            return true;
        }

        /// Gom mọi ô chưa tô của một màu vào danh sách cho sẵn.
        public int CollectUnpainted(int paletteIndex, List<Vector2Int> buffer)
        {
            if (_state == null)
            {
                buffer?.Clear();
                return 0;
            }

            return _state.CollectUnpainted(paletteIndex, buffer);
        }

        public bool IsPainted(int x, int y)
        {
            return _state != null && _state.IsPainted(x, y);
        }

        public bool IsComplete => _state != null && _state.IsComplete;

        public bool IsUntouched => _state != null && _state.IsUntouched;

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

        public bool CanReset => _state != null && !_state.IsUntouched;

        public void ResetCurrentLevel()
        {
            if (_state == null || _loadedLevel < 0) return;

            _progressStore?.ResetCurrent();

            _levelService.LoadLevel(_loadedLevel);
        }
    }
}
