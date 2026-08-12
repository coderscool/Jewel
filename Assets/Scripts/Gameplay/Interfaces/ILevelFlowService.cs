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

        /// Sang màn kế. Không làm gì nếu đang ở màn cuối.
        void GoToNextLevel();
    }
}
