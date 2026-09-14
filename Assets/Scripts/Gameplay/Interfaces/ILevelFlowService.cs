using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho luồng thắng màn. UI nghe tín hiệu và bấm nút qua đây, Gameplay
    /// không bao giờ using ngược lên UI.
    ///
    /// Tách khỏi ILevelService vì hai thứ khác nhau: ILevelService giữ DỮ LIỆU màn
    /// chơi, còn đây là DIỄN BIẾN — lúc nào coi như thắng, và ai quyết định đi tiếp.
    public interface ILevelFlowService
    {
        /// Màn ăn mừng VỪA BẮT ĐẦU: dải quét sắp chạy, camera sắp thu về. Đây là lúc UI
        /// của lượt chơi phải dọn đi để nhường màn hình cho bức tranh.
        ///
        /// Tách hẳn khỏi OnLevelCleared vì hai mốc cách nhau gần hai giây, và mỗi mốc
        /// phục vụ một việc: cái này là "dọn sân", cái kia là "mở popup". Gộp chung thì
        /// hoặc thanh màu nằm đè lên suốt màn ăn mừng, hoặc popup nhảy vào quá sớm.
        ///
        /// KHÔNG bắn ở lượt chơi lại một màn đã xong: lượt đó ăn mừng vẫn chạy nhưng
        /// không có popup nào theo sau, nên dọn HUD đi là bỏ người chơi lại trước một bức
        /// tranh xong xuôi mà không còn đường nào bấm tiếp.
        event Action OnCelebrationStarted;

        /// Bức tranh đã tô xong, mọi hiệu ứng ăn mừng đã chạy hết, và đã chờ thêm một
        /// nhịp. Đây là lúc mở popup thắng màn.
        event Action OnLevelCleared;

        /// Không còn màn nào mang id kế tiếp. Popup dựa vào đây để ẩn nút đi tiếp.
        bool IsLastLevel { get; }

        /// Màn vừa hoàn thành. -1 khi chưa có màn nào xong trong phiên chơi này.
        ///
        /// Cần con số riêng vì tiến trình đã nhích sang màn kế NGAY lúc tô xong, không
        /// đợi người chơi bấm nút — tô xong là đã xong, bấm nút chỉ là chuyện đi tiếp.
        /// Nhờ vậy thắng màn rồi thoát game trong lúc popup đang mở vẫn được ghi nhận.
        ///
        /// Hệ quả: từ lúc popup mở, CurrentLevel KHÔNG còn là màn người chơi vừa tô.
        /// Ai cần nói về màn vừa xong thì đọc ở đây.
        int ClearedLevel { get; }
    }
}
