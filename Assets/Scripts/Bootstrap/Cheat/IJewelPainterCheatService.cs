#if CHEAT_ENABLED
namespace JewelPainter.Bootstrap.Cheat
{
    /// Port cheat riêng của JewelPainter.
    public interface IJewelPainterCheatService
    {
        int RemainingCells { get; }

        int HintCredits { get; }

        int FreePaintCredits { get; }

        int FillColorCredits { get; }

        bool IsFilling { get; }

        /// Tô thêm `count` ô, rải đều qua nhiều frame.
        void PaintCells(int count);

        /// Tô nốt mọi ô còn lại của màu đang chọn hoặc màu đầu tiên còn ô.
        void PaintOneColor();

        /// Dừng cú tô hàng loạt đang chạy.
        void StopFilling();

        /// Cộng lượt gợi ý, để test nút gợi ý mà không phải xem quảng cáo.
        void AddHintCredits(int amount);

        /// Cộng lượt tô tự do.
        void AddFreePaintCredits(int amount);

        /// Cộng lượt tô hết màu.
        void AddFillColorCredits(int amount);

        bool IsHudHidden { get; }

        /// Ẩn hoặc hiện HUD mà vẫn giữ tương tác.
        void SetHudHidden(bool hidden);
    }
}
#endif
