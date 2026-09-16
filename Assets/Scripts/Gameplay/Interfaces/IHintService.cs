using System;
using UnityEngine;

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

        /// Y HỆT một lần gợi ý, nhưng KHÔNG trừ lượt và không bao giờ đổi màu đang chọn:
        /// camera phóng sát rồi bay tới một ô chưa tô của màu đó, dấu gợi ý rơi xuống.
        ///
        /// Có mặt cho màn hướng dẫn. Người chơi mới vừa chọn màu đầu tiên trong đời và
        /// đang nhìn một bảng toàn ô trống giống hệt nhau — cú bay này trả lời đúng câu
        /// hỏi kế tiếp của họ, "giờ tô vào đâu". Bắt họ trả một lượt gợi ý cho câu đó là
        /// thu tiền vé của người còn chưa biết mình đang ở đâu.
        ///
        /// Tách hẳn khỏi UseHint thay vì thêm một tham số bool: "có trừ lượt không" là
        /// câu hỏi mà mọi chỗ gọi UseHint đều phải trả lời lại, và chỉ cần một chỗ trả
        /// lời sai là người chơi mất lượt mà không hiểu vì sao.
        ///
        /// KHÔNG thả dấu gợi ý: ở màn hướng dẫn, chỗ cần tô được chỉ bằng chính ngón tay
        /// trượt qua mấy ô đó. Hai thứ cùng lúc là hai cái cùng đòi được nhìn.
        ///
        /// Trả ra ô đã bay tới, để bên gọi dựng đường đi cho ngón tay từ đó.
        ///
        /// false khi chưa chọn màu, màu đang chọn đã tô hết, hoặc thiếu camera.
        bool FocusHintWithoutSpending(out Vector2Int cell);

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
