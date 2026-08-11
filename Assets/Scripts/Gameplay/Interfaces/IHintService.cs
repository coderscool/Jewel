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
        bool CanUseHint { get; }

        /// false khi không dùng được — bên gọi không cần tự kiểm tra trước.
        bool UseHint();

        /// Bắn khi giá trị CanUseHint đổi, để nút tự bật/tắt. Chỉ bắn lúc ĐỔI,
        /// không bắn mỗi lần tô một ô.
        event Action<bool> OnHintAvailabilityChanged;
    }
}
