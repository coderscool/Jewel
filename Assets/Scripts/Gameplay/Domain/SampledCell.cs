using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Kết quả lấy mẫu một ô lưới: màu trung bình, hoặc cờ báo ô này không được tô.
    public readonly struct SampledCell
    {
        public SampledCell(Color32 color, bool isEmpty)
        {
            Color = color;
            IsEmpty = isEmpty;
        }

        public Color32 Color { get; }
        public bool IsEmpty { get; }

        public static SampledCell Empty => new SampledCell(default, true);
    }
}
