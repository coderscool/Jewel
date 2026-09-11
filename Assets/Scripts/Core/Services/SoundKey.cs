namespace JewelPainter.Core.Services
{
    /// Giá trị số ghi CỐ ĐỊNH và chỉ được THÊM vào cuối.
    ///
    /// SoundConfig lưu key theo con số, không theo tên. Chèn một giá trị vào giữa hay
    /// đổi số của một giá trị cũ là mọi dòng đã gán trong asset lặng lẽ trỏ sang clip
    /// khác — không lỗi biên dịch, không cảnh báo, chỉ là tiếng bấm nút bỗng thành
    /// tiếng đóng popup.
    public enum SoundKey
    {
        None = 0,
        ButtonClick = 1,
        PopupOpen = 2,
        PopupClose = 3,
        LevelComplete = 4,

        /// Chọn một viên ngọc trên thanh màu — kể cả khi chọn bằng cách giữ tay vào tranh.
        ChooseJewel = 5,

        /// Một đồng xu chạm đích ở popup thắng màn. Bắn nhiều lần trong một lượt.
        Coin = 6,

        Hint = 7,

        /// Booster tô tự do.
        FreePaint = 8,

        /// Một viên ngọc đáp xuống tranh. Đây là tiếng bắn DÀY nhất trong game — xem
        /// Min Interval và Pitch Variance ở SoundConfig.
        Pop = 9,

        /// Booster tô hết màu.
        MagicWand = 10,

        /// Đóng một popup.
        Cancel = 11,

        /// Các nút ĐI TỚI: Play ở Home, Home trong popup Cài đặt, Continue ở popup
        /// thắng màn, và hai công tắc âm thanh.
        Direction = 12,

        /// Cả một màu vừa được tô xong — tiếng đi kèm đợt loé của ColorCompleteSparkle.
        /// Bắn đúng MỘT lần cho mỗi màu, không phải một lần cho mỗi ô loé.
        ColorComplete = 13,
    }
}
