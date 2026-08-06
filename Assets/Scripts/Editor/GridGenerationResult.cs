using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Kết quả một lần cắt ảnh: lưới chỉ số cộng bảng màu rút ra từ chính ảnh đó.
    /// Hai thứ đi liền nhau — chỉ số trong lưới vô nghĩa nếu tách khỏi bảng màu này.
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
