using System;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Toán toạ độ của bảng. Thuần C# — không MonoBehaviour, không camera, không texture.
    ///
    /// Bảng căn giữa gốc toạ độ, mỗi ô rộng đúng một world unit.
    /// Quy ước: ô (0, 0) nằm ở góc TRÊN BÊN TRÁI, khớp với PixelGrid.
    /// World có +y hướng lên, nên cellY tăng thì world y giảm.
    public class BoardLayout
    {
        public BoardLayout(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Phải dương");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Phải dương");

            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public Bounds WorldBounds => new Bounds(Vector3.zero, new Vector3(Width, Height, 0f));

        /// Một ô cao đúng một world unit; camera orthographic thấy 2 * size world unit
        /// theo chiều dọc. Dùng chung cho mọi quyết định phụ thuộc mức zoom.
        public static float CellScreenPixels(float screenHeight, float orthographicSize)
        {
            if (orthographicSize <= 0f) return 0f;

            return screenHeight / (2f * orthographicSize);
        }

        public Vector2 CellToWorldCenter(int x, int y)
        {
            var worldX = x - Width / 2f + 0.5f;
            var worldY = Height / 2f - y - 0.5f;

            return new Vector2(worldX, worldY);
        }

        /// false nếu điểm nằm ngoài bảng. cell vẫn được gán để bên gọi xem được nó lệch đâu.
        public bool TryWorldToCell(Vector2 world, out Vector2Int cell)
        {
            var x = Mathf.FloorToInt(world.x + Width / 2f);
            var y = Mathf.FloorToInt(Height / 2f - world.y);

            cell = new Vector2Int(x, y);

            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// Giao của tầm nhìn với bảng, đã kẹp trong biên. Trả hình chữ nhật rỗng
        /// nếu tầm nhìn nằm hẳn ngoài bảng.
        public RectInt VisibleCells(Rect viewportWorldRect)
        {
            var minX = Mathf.FloorToInt(viewportWorldRect.xMin + Width / 2f);
            var maxX = Mathf.CeilToInt(viewportWorldRect.xMax + Width / 2f);

            // world y lớn ứng với cell y nhỏ, nên hai đầu đảo nhau
            var minY = Mathf.FloorToInt(Height / 2f - viewportWorldRect.yMax);
            var maxY = Mathf.CeilToInt(Height / 2f - viewportWorldRect.yMin);

            minX = Mathf.Clamp(minX, 0, Width);
            maxX = Mathf.Clamp(maxX, 0, Width);
            minY = Mathf.Clamp(minY, 0, Height);
            maxY = Mathf.Clamp(maxY, 0, Height);

            return new RectInt(minX, minY, Mathf.Max(0, maxX - minX), Mathf.Max(0, maxY - minY));
        }
    }
}
