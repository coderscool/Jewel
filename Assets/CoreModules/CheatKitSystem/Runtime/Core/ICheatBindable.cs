using CoreModules.CheatKit.Ports;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Seam inject service cho module: module nào cần port của game thì implement.
    /// Installer (phía game) gọi <see cref="Bind"/> sau khi panel dựng xong — module tự
    /// phụ thuộc abstraction (<see cref="CheatServices"/>), không hề biết game cụ thể (DIP).
    ///
    /// Bind có thể gọi LẠI khi level/scene đổi → cài đặt phải idempotent (chỉ cache &amp; refresh UI).
    /// </summary>
    public interface ICheatBindable
    {
        void Bind(CheatServices services);
    }
}
