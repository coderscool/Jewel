namespace CoreModules.CheatKit
{
    /// <summary>
    /// Chiến lược "cử chỉ bí mật" để MỞ cheat panel (Strategy pattern → OCP: đổi cách mở mà không
    /// sửa <see cref="UITestPanel"/>). <see cref="UITestPanel"/> gọi <see cref="Poll"/> mỗi frame
    /// khi panel đang ẩn; true = mở panel.
    /// </summary>
    public interface IPanelRevealGesture
    {
        /// <summary>Trả true đúng frame cử chỉ vừa hoàn tất (tự reset tiến trình bên trong).</summary>
        bool Poll();

        /// <summary>Xoá tiến trình cử chỉ (gọi khi panel mở → lần sau bắt đầu lại sạch).</summary>
        void Reset();
    }
}
