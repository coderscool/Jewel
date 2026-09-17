using System;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Một phép chỉnh màu, dùng chung cho cả cửa sổ Editor lẫn lúc chạy game.
    [Serializable]
    public struct ColorAdjustment
    {
        private const float RedWeight = 0.299f;
        private const float GreenWeight = 0.587f;
        private const float BlueWeight = 0.114f;

        [Tooltip("Độ rực, 0 là giữ nguyên.")]
        [Range(-1f, 1f)]
        [SerializeField] private float _saturation;

        [Tooltip("Độ tương phản, 0 là giữ nguyên.")]
        [Range(-1f, 1f)]
        [SerializeField] private float _contrast;

        [Tooltip("Độ sáng cộng vào cả ba kênh.")]
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

        public static ColorAdjustment None => default;

        public bool IsNone =>
            Mathf.Approximately(_saturation, 0f) &&
            Mathf.Approximately(_contrast, 0f) &&
            Mathf.Approximately(_brightness, 0f);

        /// Áp phép chỉnh màu theo thứ tự rực, tương phản, sáng.
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
