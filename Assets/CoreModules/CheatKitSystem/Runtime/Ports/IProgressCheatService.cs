namespace CoreModules.CheatKit.Ports
{
    /// <summary>
    /// PORT — tiến trình &amp; tài nguyên người chơi (progression, tiền, save). Tách riêng theo ISP:
    /// một game không có tiền tệ vẫn implement được phần còn lại, hoặc bỏ qua port này hoàn toàn.
    /// </summary>
    public interface IProgressCheatService
    {
        /// <summary>Số coin/tài nguyên hiện có. Trả -1 nếu game không có tiền tệ.</summary>
        int Coins { get; }

        /// <summary>Mở khoá toàn bộ level (đặt progression lên mức tối đa).</summary>
        void UnlockAll();

        /// <summary>Đặt mốc tiến trình đã mở khoá (0-based). Adapter tự clamp.</summary>
        void SetProgress(int levelIndex);

        /// <summary>Cộng (hoặc trừ nếu âm) coin/tài nguyên.</summary>
        void AddCoins(int amount);

        /// <summary>Xoá session-save của level hiện tại (test deal lại từ đầu).</summary>
        void ClearSave();
    }
}
