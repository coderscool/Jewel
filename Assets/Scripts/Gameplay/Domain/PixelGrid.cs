using System;

namespace JewelPainter.Gameplay.Domain
{
    /// Ma trận chỉ số bảng màu. Thuần C# — không biết Texture2D hay ScriptableObject,
    /// nên test EditMode được mà không cần scene.
    /// Quy ước: y = 0 là hàng TRÊN CÙNG. Bên gọi chịu trách nhiệm lật cho đúng.
    public class PixelGrid
    {
        /// Ô không được tô — thường là vùng trong suốt của ảnh gốc.
        public const int EmptyCell = -1;

        private readonly int[] _cells;

        public PixelGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Chiều rộng phải dương");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Chiều cao phải dương");

            Width = width;
            Height = height;
            _cells = new int[width * height];

            for (var i = 0; i < _cells.Length; i++) _cells[i] = EmptyCell;
        }

        public int Width { get; }
        public int Height { get; }

        public int GetCell(int x, int y) => _cells[Index(x, y)];

        public void SetCell(int x, int y, int paletteIndex) => _cells[Index(x, y)] = paletteIndex;

        /// Trả về bản sao — người gọi sửa mảng nhận được không ảnh hưởng lưới gốc.
        public int[] ToArray() => (int[])_cells.Clone();

        public static PixelGrid FromArray(int width, int height, int[] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            var grid = new PixelGrid(width, height);
            if (cells.Length != grid._cells.Length)
            {
                throw new ArgumentException(
                    $"Cần {grid._cells.Length} ô cho lưới {width}x{height}, nhận được {cells.Length}",
                    nameof(cells));
            }

            Array.Copy(cells, grid._cells, cells.Length);
            return grid;
        }

        private int Index(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x), x, $"Ngoài phạm vi 0..{Width - 1}");
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y), y, $"Ngoài phạm vi 0..{Height - 1}");

            return y * Width + x;
        }
    }
}
