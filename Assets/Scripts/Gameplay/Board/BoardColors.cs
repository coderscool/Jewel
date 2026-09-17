using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Phép tính màu dùng chung cho bảng.
    public static class BoardColors
    {
        private const float RedWeight = 0.299f;
        private const float GreenWeight = 0.587f;
        private const float BlueWeight = 0.114f;

        /// Độ sáng cảm nhận, thang 0..255.
        public static float Luminance(Color32 color)
        {
            return RedWeight * color.r + GreenWeight * color.g + BlueWeight * color.b;
        }

        /// Bản xám của một màu.
        public static Color32 ToGrayscale(Color32 color)
        {
            var gray = (byte)Mathf.Clamp(Mathf.RoundToInt(Luminance(color)), 0, 255);

            return new Color32(gray, gray, gray, color.a);
        }
    }
}
