namespace JewelPainter.Core.Services
{
    /// Công tắc rung, và một chỗ duy nhất để gọi rung.
    ///
    /// Tách khỏi ISoundService dù hai công tắc nằm cạnh nhau trong cùng một bảng cài đặt:
    /// rung không phải âm thanh, và một "sound service" mọc thêm hàm Vibrate là cái tên
    /// bắt đầu nói dối. Bảng cài đặt gom chúng lại là chuyện của giao diện, không phải
    /// lý do để gom ở tầng dưới.
    ///
    /// Phần RUNG THẬT hiện còn để trống — xem VibrationService.Vibrate. Công tắc thì đã
    /// chạy thật: nó lưu xuống đĩa và hai bảng cài đặt cùng đọc từ đây nên luôn khớp.
    public interface IVibrationService
    {
        bool IsEnabled { get; }

        void SetEnabled(bool enabled);

        /// Gọi ở mọi chỗ muốn máy rung. An toàn để gọi bất cứ đâu: nếu người chơi đã tắt
        /// công tắc thì hàm này tự đi ra.
        void Vibrate();
    }
}
