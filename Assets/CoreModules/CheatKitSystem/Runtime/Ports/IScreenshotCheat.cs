namespace CoreModules.CheatKit.Ports
{
    /// <summary>
    /// PORT — chụp screenshot level hiện tại. Adapter của game cung cấp camera template +
    /// world-bounds (vd từ board) rồi gọi ScreenshotCaptureService. Tách port để module screenshot
    /// không phụ thuộc cách game xác định vùng chụp (DIP/ISP).
    ///
    /// (Adapter WordJigsort theo board-bounds được hoãn — xem Bước 6b.)
    /// </summary>
    public interface IScreenshotCheat
    {
        /// <summary>Có thể chụp ngay không (đã có camera + level đang hiển thị).</summary>
        bool IsAvailable { get; }

        /// <summary>Chụp level hiện tại, lưu ra file (đường dẫn do adapter quyết định/log).</summary>
        void Capture();
    }
}
