#if CHEAT_ENABLED
namespace JewelPainter.Bootstrap.Cheat
{
    /// PORT ĐẶC THÙ của JewelPainter, theo đúng mẫu §10 trong README của CheatKit: cheat
    /// riêng của một dòng game KHÔNG được nhét vào ba port chuẩn.
    ///
    /// Ba port chuẩn cố tình trừu tượng để bê kit sang game khác không phải sửa gì. "Tô
    /// nhanh một phần bảng" thì ngược lại — nó chỉ có nghĩa với game tô màu. Mà đó lại
    /// đúng là cheat cần nhất ở đây: mọi lỗi thú vị đều nằm ở QUÃNG GIỮA — bảng tô dở,
    /// vài màu đã xong, vài màu còn dang dở, ngọc đang bay — và ngồi tô tay 5000 ô để tới
    /// được chỗ đó thì không ai kiểm được gì.
    ///
    /// Giao diện này thuộc về GAME, không thuộc về kit: nó nằm trong thư mục của game và
    /// đi tới module qua CheatServices.extras. Kit không đổi lấy một dòng (OCP).
    public interface IJewelPainterCheatService
    {
        /// Số ô còn CHƯA tô của cả bảng. -1 khi chưa nạp màn nào.
        int RemainingCells { get; }

        /// Số lượt gợi ý còn lại. -1 khi chưa dựng xong.
        int HintCredits { get; }

        /// Số lượt booster "tô tự do" còn lại. -1 khi chưa dựng xong.
        int FreePaintCredits { get; }

        /// Số lượt booster "tô hết màu" còn lại. -1 khi chưa dựng xong.
        int FillColorCredits { get; }

        /// Đang có một cú tô hàng loạt chạy dở.
        bool IsFilling { get; }

        /// Tô thêm `count` ô, rải đều qua nhiều frame.
        void PaintCells(int count);

        /// Tô nốt mọi ô còn lại của MỘT màu — màu đang chọn nếu nó còn ô, không thì màu
        /// đầu tiên còn ô. Đây là cách nhanh nhất để dựng lại cảnh "một màu vừa xong"
        /// mà không phải tô kín cả bảng.
        void PaintOneColor();

        /// Dừng cú tô hàng loạt đang chạy.
        void StopFilling();

        /// Cộng lượt gợi ý, để test nút gợi ý mà không phải xem quảng cáo.
        void AddHintCredits(int amount);

        /// Cộng lượt tô tự do. Người chơi mới chỉ được đúng một lượt, mà cái booster này
        /// thì phải bấm đi bấm lại mới soát hết được — hết lượt là tắc đường test.
        void AddFreePaintCredits(int amount);

        /// Cộng lượt tô hết màu.
        void AddFillColorCredits(int amount);

        /// HUD đang bị GIẤU đi hay không.
        bool IsHudHidden { get; }

        /// Giấu HUD đi mà KHÔNG khoá nó: nút vẫn ăn chạm, chỉ là không nhìn thấy.
        ///
        /// Để chụp ảnh và quay màn hình bức tranh cho sạch, nhưng vẫn bấm được booster
        /// ngay trong lúc quay. Tắt hẳn HUD thì phải bật lại mới bấm được, mà bật lại là
        /// nó nhảy vào khuôn hình đúng lúc không muốn.
        void SetHudHidden(bool hidden);
    }
}
#endif
