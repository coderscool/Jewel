using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho booster "tô hết màu": bấm một cái thì mọi ô còn lại của MÀU ĐANG
    /// CHỌN được tô hết.
    ///
    /// Cùng khuôn với IHintService và IFreePaintService — UI chỉ biết "bấm được hay
    /// không", "bấm đi", "còn mấy lượt".
    public interface IFillColorService
    {
        /// Bấm nút lúc này có tác dụng không: đã nạp lưới, màn chưa xong, và không có cú
        /// tô nào đang chạy dở.
        ///
        /// KHÔNG đòi phải chọn màu trước, và cũng KHÔNG xét số lượt còn lại — cùng lý do
        /// đã ghi ở IHintService.CanUseHint: chưa chọn màu thì chính cú bấm đó là thứ mở
        /// lời nhắc chọn màu, hết lượt thì nó là thứ mở popup mời thêm lượt. Nút xám ngắt
        /// không nói được gì, mà đó lại đúng lúc người chơi cần biết nhất.
        bool CanUse { get; }

        /// Đang có một cú tô hàng loạt chạy dở.
        bool IsFilling { get; }

        /// Phần đã tô xong của cú đang chạy, thang 0..1. 1 khi không chạy.
        float FillProgress { get; }

        int RemainingCredits { get; }

        /// false khi không dùng được — bên gọi không cần tự kiểm tra trước.
        bool Use();

        /// Bắn khi cú tô bắt đầu (true) và khi nó xong hoặc bị cắt (false).
        event Action<bool> OnFillingChanged;

        event Action<int> OnCreditsChanged;

        /// Bấm nút mà không còn lượt nào. UI nghe cái này để mở popup — Gameplay không
        /// được biết popup tồn tại.
        event Action OnCreditsExhausted;

        /// Bắn khi giá trị CanUse đổi, để nút tự bật/tắt. Chỉ bắn lúc ĐỔI.
        event Action<bool> OnAvailabilityChanged;
    }
}
