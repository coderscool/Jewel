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
    }
}
