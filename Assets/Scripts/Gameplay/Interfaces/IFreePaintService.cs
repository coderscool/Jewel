using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho booster "tô tự do": bấm một cái thì trong ít giây, MỌI ô chưa tô đều
    /// hiện dấu gợi ý và chạm vào ô nào cũng tô được ô đó.
    ///
    /// Cùng khuôn với IHintService — UI chỉ biết "bấm được hay không", "bấm đi", "còn
    /// mấy lượt", "còn mấy giây". Luật tô đổi thế nào là chuyện của Gameplay, và nó nằm
    /// sau IPaintService.FreePaintActive.
    ///
    /// Tách khỏi IHintService thay vì nhét thêm vào đó: hai booster này dùng chung một
    /// cái tên "gợi ý" nhưng là hai món hàng khác nhau — số lượt riêng, popup mời mua
    /// riêng, và sau này gần như chắc chắn giá riêng.
    public interface IFreePaintService
    {
        /// Bấm nút lúc này có tác dụng không: đã nạp lưới, màn chưa xong, và booster chưa
        /// chạy.
        ///
        /// KHÔNG xét số lượt còn lại — cùng lý do đã ghi ở IHintService.CanUseHint: hết
        /// lượt thì chính cú bấm đó là thứ mở popup mời thêm lượt.
        bool CanUse { get; }

        /// Booster đang chạy.
        bool IsActive { get; }

        /// Số giây còn lại. 0 khi không chạy.
        float RemainingSeconds { get; }

        /// Một lượt dùng kéo dài bao nhiêu giây. Cho chỗ hiển thị vẽ vòng đếm ngược mà
        /// không phải tự biết con số cấu hình trong Inspector.
        float DurationSeconds { get; }

        /// Số lượt còn lại.
        int RemainingCredits { get; }

        /// false khi không dùng được — bên gọi không cần tự kiểm tra trước.
        bool Use();

        /// Bắn khi booster bật hoặc tắt.
        event Action<bool> OnActiveChanged;

        event Action<int> OnCreditsChanged;

        /// Bấm nút mà không còn lượt nào. UI nghe cái này để mở popup — Gameplay không
        /// được biết popup tồn tại.
        event Action OnCreditsExhausted;

        /// Bắn khi giá trị CanUse đổi, để nút tự bật/tắt. Chỉ bắn lúc ĐỔI.
        event Action<bool> OnAvailabilityChanged;
    }
}
