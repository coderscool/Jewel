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
