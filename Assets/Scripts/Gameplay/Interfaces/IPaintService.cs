using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho việc tô màu.
    public interface IPaintService
    {
        int SelectedPaletteIndex { get; }

        IReadOnlyList<int> UsedPaletteIndices { get; }

        void SelectColor(int paletteIndex);

        /// Ô này có tô được bằng màu đang chọn không.
        bool CanPaint(int x, int y);

        /// Tô một ô; true nếu tô được.
        bool TryPaint(int x, int y);

        /// Ô đã tô chưa; false nếu chưa nạp lưới hoặc ngoài bảng.
        bool IsPainted(int x, int y);

        bool IsComplete { get; }

        bool IsUntouched { get; }

        int RemainingFor(int paletteIndex);

        /// Ô chưa tô thứ `ordinal` (đếm từ 0) của một màu, quét trái→phải, trên→dưới.
        bool TryGetUnpaintedCell(int paletteIndex, int ordinal, out Vector2Int cell);

        /// Tỉ lệ ô đã tô của một màu, thang 0..1.
        float ProgressFor(int paletteIndex);

        event Action OnBoardReady;

        event Action<int> OnColorSelected;

        event Action<int> OnColorFocusRequested;

        event Action<Vector2Int, int> OnCellPainted;

        event Action OnColorRequired;

        /// Báo người chơi cần chọn màu.
        void RequireColor();

        bool FreePaintActive { get; }

        event Action<bool> OnFreePaintChanged;

        bool ColorLocked { get; }

        event Action<bool> OnColorLockChanged;

        bool CanReset { get; }

        /// Xoá sạch tiến độ tô của màn đang chơi rồi nạp lại màn đó từ đầu.
        void ResetCurrentLevel();
    }
}
