using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Trạng thái tô của một màn chơi.
    public class PaintState
    {
        private readonly PixelGrid _grid;
        private readonly bool[] _painted;
        private readonly Dictionary<int, int> _remaining = new();

        private readonly Dictionary<int, int> _totals = new();
        private readonly List<int> _usedPaletteIndices = new();

        private int _remainingTotal;

        private int _totalColored;

        public PaintState(PixelGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _painted = new bool[grid.Width * grid.Height];

            ScanGrid();
        }

        public IReadOnlyList<int> UsedPaletteIndices => _usedPaletteIndices;

        public bool IsComplete => _remainingTotal == 0;

        public bool IsUntouched => _remainingTotal == _totalColored;

        /// Ô đã tô chưa; false nếu ngoài bảng.
        public bool IsPainted(int x, int y) => IsInside(x, y) && _painted[Index(x, y)];

        /// Ảnh có dùng màu này không.
        public bool IsUsed(int paletteIndex) => _remaining.ContainsKey(paletteIndex);

        public int RemainingFor(int paletteIndex)
        {
            return _remaining.TryGetValue(paletteIndex, out var count) ? count : 0;
        }

        public int TotalFor(int paletteIndex)
        {
            return _totals.TryGetValue(paletteIndex, out var count) ? count : 0;
        }

        /// Tỉ lệ ô đã tô của một màu, thang 0..1.
        public float ProgressFor(int paletteIndex)
        {
            var total = TotalFor(paletteIndex);
            if (total <= 0) return 1f;

            return (total - RemainingFor(paletteIndex)) / (float)total;
        }

        public int PaintedBitsLength => (_painted.Length + 7) / 8;

        /// Đóng gói trạng thái tô để đem đi lưu.
        public byte[] ToPaintedBits()
        {
            var bytes = new byte[PaintedBitsLength];

            for (var i = 0; i < _painted.Length; i++)
            {
                if (_painted[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            }

            return bytes;
        }

        /// Nạp trạng thái tô từ bản lưu; false khi không khớp cỡ lưới.
        public bool RestorePaintedBits(byte[] bytes)
        {
            if (bytes == null || bytes.Length != PaintedBitsLength) return false;

            for (var i = 0; i < _painted.Length; i++)
            {
                _painted[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
            }

            RecountRemaining();
            return true;
        }

        /// Đánh dấu mọi ô có màu là đã tô.
        public void PaintAll()
        {
            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    if (_grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                    _painted[Index(x, y)] = true;
                }
            }

            RecountRemaining();
        }

        /// Đếm lại số ô còn lại của từng màu.
        private void RecountRemaining()
        {
            _remainingTotal = 0;

            foreach (var paletteIndex in _usedPaletteIndices)
            {
                _remaining[paletteIndex] = _totals[paletteIndex];
            }

            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    var cell = _grid.GetCell(x, y);
                    if (cell == PixelGrid.EmptyCell) continue;
                    if (!_remaining.ContainsKey(cell)) continue;

                    if (_painted[Index(x, y)]) _remaining[cell] -= 1;
                    else _remainingTotal++;
                }
            }
        }

        /// Ô chưa tô thứ `ordinal` của một màu (đếm từ 0), quét trái→phải, trên→dưới.
        public bool TryGetUnpainted(int paletteIndex, int ordinal, out Vector2Int cell)
        {
            cell = default;

            if (ordinal < 0) return false;
            if (paletteIndex == PixelGrid.EmptyCell) return false;

            var seen = 0;

            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    if (_grid.GetCell(x, y) != paletteIndex) continue;
                    if (_painted[Index(x, y)]) continue;

                    if (seen++ != ordinal) continue;

                    cell = new Vector2Int(x, y);
                    return true;
                }
            }

            return false;
        }

        /// Tỉ lệ ô đã tô của một lưới, thang 0..1, tính thẳng từ bản lưu.
        public static float FractionPainted(PixelGrid grid, byte[] paintedBits)
        {
            if (grid == null) return 0f;

            var cellCount = grid.Width * grid.Height;
            if (paintedBits == null || paintedBits.Length != (cellCount + 7) / 8) return 0f;

            var colored = 0;
            var painted = 0;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                    colored++;

                    var index = y * grid.Width + x;

                    if ((paintedBits[index >> 3] & (1 << (index & 7))) != 0) painted++;
                }
            }

            return colored > 0 ? painted / (float)colored : 1f;
        }

        /// Gom mọi ô chưa tô của một màu vào danh sách cho sẵn, quét trái→phải, trên→dưới.
        public int CollectUnpainted(int paletteIndex, List<Vector2Int> buffer)
        {
            if (buffer == null) return 0;

            buffer.Clear();

            if (paletteIndex == PixelGrid.EmptyCell) return 0;

            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    if (_grid.GetCell(x, y) != paletteIndex) continue;
                    if (_painted[Index(x, y)]) continue;

                    buffer.Add(new Vector2Int(x, y));
                }
            }

            return buffer.Count;
        }

        /// false nếu toạ độ ngoài bảng, ô rỗng, ô đã tô, hoặc màu không khớp.
        public bool CanPaint(int x, int y, int paletteIndex)
        {
            if (!IsInside(x, y)) return false;
            if (_painted[Index(x, y)]) return false;

            var cell = _grid.GetCell(x, y);

            return cell != PixelGrid.EmptyCell && cell == paletteIndex;
        }

        public bool TryPaint(int x, int y, int paletteIndex)
        {
            if (!CanPaint(x, y, paletteIndex)) return false;

            _painted[Index(x, y)] = true;
            _remaining[paletteIndex] -= 1;
            _remainingTotal -= 1;

            return true;
        }

        /// Ô này tô được không cần đúng màu: chỉ cần nằm trong bảng, có màu, và chưa tô.
        public bool CanPaintAny(int x, int y)
        {
            if (!IsInside(x, y)) return false;
            if (_painted[Index(x, y)]) return false;

            return _grid.GetCell(x, y) != PixelGrid.EmptyCell;
        }

        /// Tô một ô bằng chính màu của nó, bất kể người chơi đang chọn màu nào.
        public bool TryPaintAny(int x, int y, out int paletteIndex)
        {
            paletteIndex = PixelGrid.EmptyCell;

            if (!CanPaintAny(x, y)) return false;

            paletteIndex = _grid.GetCell(x, y);

            _painted[Index(x, y)] = true;
            _remaining[paletteIndex] -= 1;
            _remainingTotal -= 1;

            return true;
        }

        private void ScanGrid()
        {
            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    var cell = _grid.GetCell(x, y);
                    if (cell == PixelGrid.EmptyCell) continue;

                    if (_remaining.TryGetValue(cell, out var count))
                    {
                        _remaining[cell] = count + 1;
                        _totals[cell] = count + 1;
                    }
                    else
                    {
                        _remaining[cell] = 1;
                        _totals[cell] = 1;
                        _usedPaletteIndices.Add(cell);
                    }

                    _remainingTotal++;
                }
            }

            _usedPaletteIndices.Sort();

            _totalColored = _remainingTotal;
        }

        private bool IsInside(int x, int y) => x >= 0 && x < _grid.Width && y >= 0 && y < _grid.Height;

        private int Index(int x, int y) => y * _grid.Width + x;
    }
}
