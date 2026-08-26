using System;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Một phép chỉnh màu, dùng chung cho cả cửa sổ Editor lẫn lúc chạy game.
    ///
    /// Vì sao phải nằm ở Domain chứ không nằm trong cửa sổ Editor: chừng nào phép toán
    /// chỉ có một bản trong Editor thì bộ số dò được là số chết — game không chạy nó,
    /// nên xem trước bao nhiêu cũng không bảo đảm điều gì. Để ở đây thì hai bên gọi
    /// đúng một hàm.
    ///
    /// KHÔNG có núm "độ loé" riêng. Cộng trắng vào cả ba kênh chính là Độ sáng — hai
    /// tên cho một phép toán chỉ làm người chỉnh tưởng mình có hai chiều tự do. Viên
    /// ngọc sáng hơn nền là do NÓ CÓ ColorAdjustment RIÊNG với độ sáng dương, không
    /// phải do có thêm một núm.
    ///
    /// 0 LÀ GIỮ NGUYÊN cho cả ba núm, kể cả rực và tương phản vốn quen dùng thang nhân
    /// quanh mốc 1. Đây là chủ ý: `default(ColorAdjustment)` phải là phép không làm gì.
    /// Struct chưa gán trong Inspector sẽ về toàn số 0, mà 0 theo thang nhân nghĩa là
    /// "xám hết" — cả bảng màu biến thành xám chỉ vì ai đó thêm một field. Photoshop
    /// cũng đánh thang -100..+100 quanh mốc 0 vì đúng lý do này.
    [Serializable]
    public struct ColorAdjustment
    {
        // Trọng số độ sáng cảm nhận (Rec. 601), giống BoardColors.Luminance.
        //
        // Chép lại ba hằng số thay vì gọi sang BoardColors vì Domain không được phụ
        // thuộc ngược lên Board. Chỗ đúng của BoardColors là ở Domain — chuyển nó
        // xuống đây rồi thì hai bên dùng chung được.
        private const float RedWeight = 0.299f;
        private const float GreenWeight = 0.587f;
        private const float BlueWeight = 0.114f;

        [Tooltip("0 giữ nguyên. -1 xám hết. +1 đẩy gấp đôi ra xa mức xám.")]
        [Range(-1f, 1f)]
        [SerializeField] private float _saturation;

        [Tooltip("Xoay quanh mốc xám giữa: 0 giữ nguyên, dương thì màu sáng sáng thêm " +
                 "và màu tối tối thêm, âm thì cả bảng dồn về giữa.")]
        [Range(-1f, 1f)]
        [SerializeField] private float _contrast;

        [Tooltip("Cộng thẳng vào cả ba kênh. 0 giữ nguyên.\n\n" +
                 "Với viên ngọc đây là núm quan trọng nhất: tint của SpriteRenderer là " +
                 "phép NHÂN với ảnh, nên chỗ sáng nhất của viên ngọc bằng đúng màu tint. " +
                 "Muốn ngọc nổi lên trên nền đất cùng màu thì phải cộng ở đây.")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float _brightness;

        public float Saturation => _saturation;
        public float Contrast => _contrast;
        public float Brightness => _brightness;

        public ColorAdjustment(float saturation, float contrast, float brightness)
        {
            _saturation = saturation;
            _contrast = contrast;
            _brightness = brightness;
        }

        /// Không làm gì cả. Bằng đúng `default(ColorAdjustment)`.
        public static ColorAdjustment None => default;

        public bool IsNone =>
            Mathf.Approximately(_saturation, 0f) &&
            Mathf.Approximately(_contrast, 0f) &&
            Mathf.Approximately(_brightness, 0f);

        /// Rực -> tương phản -> sáng, đúng thứ tự Photoshop.
        ///
        /// Thứ tự có ý nghĩa: tương phản xoay quanh mốc 0.5 nên nó khuếch đại luôn cả
        /// phần lệch mà bước tăng độ rực vừa tạo ra. Đảo hai bước cho ra kết quả khác hẳn.
        public Color32 Apply(Color32 color)
        {
            if (IsNone) return color;

            var gray = (RedWeight * color.r + GreenWeight * color.g + BlueWeight * color.b) / 255f;

            return new Color32(
                ToByte(ApplyChannel(color.r / 255f, gray)),
                ToByte(ApplyChannel(color.g / 255f, gray)),
                ToByte(ApplyChannel(color.b / 255f, gray)),
                color.a);
        }

        private float ApplyChannel(float channel, float gray)
        {
            // KHÔNG dùng Mathf.Lerp: nó kẹp t về 0..1 nên độ rực dương sẽ dừng lại đúng
            // ở màu gốc và núm có kéo tiếp cũng không đổi gì.
            var value = gray + (channel - gray) * (1f + _saturation);

            value = (value - 0.5f) * (1f + _contrast) + 0.5f;

            return value + _brightness;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
