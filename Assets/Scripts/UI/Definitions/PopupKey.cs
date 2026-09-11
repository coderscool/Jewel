namespace JewelPainter.UI.Definitions
{
    /// Chỉ là key định danh popup — KHÔNG chứa logic.
    public enum PopupKey
    {
        None = 0,
        Settings = 1,
        LevelComplete = 2,
        Pause = 3,
        Collection = 4,

        /// Bảng cài đặt mở từ màn hình Home. Tách key riêng khỏi Settings vì hai bên
        /// khác nội dung: bản trong game có nút về Home, bản ở Home thì không.
        SettingsHome = 5,

        /// Nhắc người chơi làm gì đó, ví dụ chưa chọn màu mà đã tô.
        Notification = 6,

        /// Hết lượt gợi ý miễn phí. Mở khi người chơi bấm nút gợi ý mà không còn lượt.
        HintMove = 7,

        /// Mời người chơi đánh giá. RatePopupPresenter mở sau mỗi vài màn, và tắt hẳn
        /// khi người chơi đã bấm đánh giá.
        Rate = 8,

        /// Hết lượt booster "tô tự do". Cùng vai với HintMove, nhưng cho cái nút kia.
        FreePaint = 9,

        /// Hết lượt booster "tô hết màu đang chọn".
        FillColor = 10,

        /// Ba popup báo booster VỪA MỞ KHOÁ, mỗi booster một cái.
        ///
        /// Mở đúng một lần cho mỗi booster, ở lần vào màn đầu tiên sau khi tiến trình
        /// chạm mốc. BoosterUnlockConfig quyết định booster nào gọi popup nào — ba con số
        /// dưới đây không bị code nào tra thẳng.
        BoosterUnlockHint = 11,
        BoosterUnlockFreePaint = 12,
        BoosterUnlockFillColor = 13,
    }
}
