namespace JewelPainter.Core.Services
{
    /// Bật tắt rung và gọi rung.
    public interface IVibrationService
    {
        bool IsEnabled { get; }

        void SetEnabled(bool enabled);

        /// Rung máy nếu đang bật rung.
        void Vibrate();
    }
}
