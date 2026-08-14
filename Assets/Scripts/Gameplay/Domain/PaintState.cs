using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Trạng thái tô của một màn chơi. Thuần C# — không MonoBehaviour, không scene,
    /// nên toàn bộ luật tô test được ở EditMode.
    public class PaintState
    {
        private readonly PixelGrid _grid;
        private readonly bool[] _painted;
        private readonly Dictionary<int, int> _remaining = new();

        /// Tổng số ô của mỗi màu lúc mới vào màn. Cần giữ riêng vì _remaining giảm dần,
        /// không suy ngược ra được mẫu số để tính phần trăm.
        private readonly Dictionary<int, int> _totals = new();
        private readonly List<int> _usedPaletteIndices = new();

        private int _remainingTotal;

        public PaintState(PixelGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _painted = new bool[grid.Width * grid.Height];

            ScanGrid();
        }

        /// Các chỉ số màu lưới thật sự dùng, tăng dần. Ô rỗng không tính.
        /// Đây là thứ cho thanh màu biết chỉ hiện 7 màu thay vì cả 16.
        public IReadOnlyList<int> UsedPaletteIndices => _usedPaletteIndices;

        public bool IsComplete => _remainingTotal == 0;

        /// false nếu toạ độ nằm ngoài bảng — bên gọi quét lưới không phải tự kiểm biên.
        public bool IsPainted(int x, int y) => IsInside(x, y) && _painted[Index(x, y)];

        /// Ảnh có dùng màu này không. Tra dictionary, không duyệt danh sách.
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
        /// Màu không có ô nào coi như đã xong — không có gì để tô thì không thể dở dang.
        public float ProgressFor(int paletteIndex)
        {
            var total = TotalFor(paletteIndex);
            if (total <= 0) return 1f;

            return (total - RemainingFor(paletteIndex)) / (float)total;
        }

        /// Số byte cần để gói trạng thái tô của lưới này. Mỗi ô một BIT, nên bảng 64x64
        /// (4096 ô) gói vừa 512 byte.
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

        /// false khi dữ liệu không khớp cỡ lưới hiện tại.
        ///
        /// Từ chối thay vì cố khôi phục một phần: người ta sinh lại lưới cho một màn là
        /// chuyện thường, mà bản lưu cũ đắp lên lưới mới sẽ tô sai chỗ hàng loạt — mất
        /// tiến độ còn dễ hiểu hơn là một bức tranh lem nhem không rõ vì sao.
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

        /// Đếm lại từ đầu sau khi nạp: _remaining giảm dần theo từng nước tô nên không
        /// suy ngược ra được từ mảng _painted, phải quét lưới một lượt.
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

                    // Ô rỗng bị đánh dấu đã tô trong bản lưu hỏng thì bỏ qua ở nhánh
                    // trên, không làm lệch số đếm.
                    if (_painted[Index(x, y)]) _remaining[cell] -= 1;
                    else _remainingTotal++;
                }
            }
        }

        /// Ô CHƯA TÔ thứ `ordinal` của một màu (đếm từ 0), quét trái→phải, trên→dưới.
        /// false khi màu đó không có đủ ngần ấy ô chưa tô.
        ///
        /// Nhận ordinal thay vì tự bốc ngẫu nhiên: Domain giữ được tính tất định nên
        /// test được ở EditMode, còn ai muốn ngẫu nhiên thì bốc số ở tầng trên rồi
        /// truyền xuống. RemainingFor() chính là cận trên hợp lệ của ordinal.
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
        }

        private bool IsInside(int x, int y) => x >= 0 && x < _grid.Width && y >= 0 && y < _grid.Height;

        private int Index(int x, int y) => y * _grid.Width + x;
    }
}
