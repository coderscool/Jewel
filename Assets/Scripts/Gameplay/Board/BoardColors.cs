using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Phép tính màu dùng chung cho bảng. Thuần C#, không trạng thái.
    ///
    /// Hệ số 0.299 / 0.587 / 0.114 là trọng số độ sáng cảm nhận (Rec. 601):
    /// mắt người nhạy với xanh lá hơn hẳn xanh dương, nên trung bình cộng ba kênh
    /// cho ra ảnh xám sai lệch rõ.
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

        /// Bản xám của một màu. Giữ nguyên alpha.
        ///
        /// Ảnh xám giữ đúng độ sáng của màu gốc, nên mọi quyết định dựa trên Luminance
        /// cho cùng kết quả dù bảng đang xám hay đang màu.
        public static Color32 ToGrayscale(Color32 color)
        {
            var gray = (byte)Mathf.Clamp(Mathf.RoundToInt(Luminance(color)), 0, 255);

            return new Color32(gray, gray, gray, color.a);
        }
    }
}
