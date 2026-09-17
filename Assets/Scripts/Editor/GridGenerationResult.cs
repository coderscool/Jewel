using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Kết quả một lần cắt ảnh: lưới chỉ số cộng bảng màu rút ra từ chính ảnh đó.
    public readonly struct GridGenerationResult
    {
        public GridGenerationResult(PixelGrid grid, Color32[] palette)
        {
            Grid = grid;
            Palette = palette;
        }

        public PixelGrid Grid { get; }
        public Color32[] Palette { get; }

        public bool IsValid => Grid != null && Palette != null && Palette.Length > 0;
    }
}
