using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Data
{
    /// Dữ liệu lưới của một màn chơi, do Editor tool sinh ra.
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

        /// Đổi dữ liệu thành PixelGrid; null nếu chưa có dữ liệu.
        public PixelGrid ToGrid()
        {
            if (_width <= 0 || _height <= 0) return null;
            if (_cells == null || _cells.Length != _width * _height) return null;

            return PixelGrid.FromArray(_width, _height, _cells);
        }

#if UNITY_EDITOR
        /// Ghi dữ liệu lưới từ Editor tool.
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
