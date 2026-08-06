using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Data
{
    /// Dữ liệu lưới của một màn chơi, do Editor tool sinh ra.
    ///
    /// Giữ luôn bảng màu của riêng nó: màu được rút từ chính ảnh nguồn nên mỗi màn
    /// một bộ khác nhau, không có bảng màu dùng chung nào cả. Chỉ số trong lưới vô
    /// nghĩa nếu tách khỏi bảng màu này.
    ///
    /// Tách khỏi LevelConfig vì tool ghi đè toàn bộ asset này mỗi lần sinh lại —
    /// không nên để tool đụng vào file người ta chỉnh tay.
    [CreateAssetMenu(fileName = "LevelGridData", menuName = "JewelPainter/Gameplay/Level Grid Data")]
    public class LevelGridData : ScriptableObject
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private Color32[] _colors = Array.Empty<Color32>();
        [SerializeField] private int[] _cells = Array.Empty<int>();

        public int Width => _width;
        public int Height => _height;

        public IReadOnlyList<Color32> Colors => _colors;

        /// Trả về null nếu asset chưa được tool sinh dữ liệu.
        public PixelGrid ToGrid()
        {
            if (_width <= 0 || _height <= 0) return null;
            if (_cells == null || _cells.Length != _width * _height) return null;

            return PixelGrid.FromArray(_width, _height, _cells);
        }

#if UNITY_EDITOR
        /// Chỉ dành cho Editor tool. Không gọi lúc chạy game.
        public void SetData(int width, int height, Color32[] colors, int[] cells)
        {
            _width = width;
            _height = height;
            _colors = colors;
            _cells = cells;
        }
#endif
    }
}
