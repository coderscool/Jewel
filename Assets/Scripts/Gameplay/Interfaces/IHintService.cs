using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho nút gợi ý. UI chỉ biết "bấm được hay không" và "bấm đi" —
    /// việc tìm ô nào, đưa camera tới đâu là chuyện của Gameplay.
    ///
    /// Tách ra thành interface riêng thay vì nhét thêm vào IPaintService: sau này nút
    /// gợi ý gần như chắc chắn sẽ có giới hạn lượt, thời gian chờ, hoặc phải xem quảng
    /// cáo — toàn bộ những thứ đó thuộc về đây chứ không phải về việc tô màu.
    public interface IHintService
    {
        /// Bấm nút lúc này có tác dụng không: đã chọn màu, và màu đó còn ô chưa tô.
        ///
        /// KHÔNG xét số lượt còn lại. Hết lượt thì nút vẫn phải bấm được, vì chính cú bấm
        /// đó là thứ mở popup mời xem quảng cáo. Nút xám ngắt không nói được gì, mà đó lại
        /// đúng lúc cần nói nhất.
        bool CanUseHint { get; }

        /// Số lượt gợi ý miễn phí còn lại.
        int RemainingCredits { get; }

        /// false khi không dùng được — bên gọi không cần tự kiểm tra trước.
        bool UseHint();

        /// Bắn khi số lượt đổi, để chỗ hiển thị không phải hỏi lại mỗi frame.
        event Action<int> OnCreditsChanged;

        /// Bấm nút mà không còn lượt nào. UI nghe cái này để mở popup.
        ///
        /// Bắn sự kiện chứ không tự mở popup: Gameplay không được biết popup tồn tại —
        /// cùng khuôn với RequireColor và OnColorRequired.
        event Action OnCreditsExhausted;

        /// Bắn khi giá trị CanUseHint đổi, để nút tự bật/tắt. Chỉ bắn lúc ĐỔI,
        /// không bắn mỗi lần tô một ô.
        event Action<bool> OnHintAvailabilityChanged;
    }
}
