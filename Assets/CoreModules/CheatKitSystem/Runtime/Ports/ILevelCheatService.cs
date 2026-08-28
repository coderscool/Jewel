namespace CoreModules.CheatKit.Ports
{
    /// <summary>
    /// PORT — điều hướng level cho cheat tool. CheatKit chỉ phụ thuộc abstraction này (DIP);
    /// mỗi game cung cấp 1 adapter map sang flow load-level thật của mình.
    ///
    /// Index quy ước 0-based để khớp nội bộ engine; UI hiển thị +1 cho người dùng.
    /// </summary>
    public interface ILevelCheatService
    {
        /// <summary>Tổng số level. Trả 0 nếu vô hạn / không xác định (panel ẩn thanh "x / N").</summary>
        int Count { get; }

        /// <summary>Level đang chơi (0-based). -1 nếu chưa load.</summary>
        int CurrentIndex { get; }

        /// <summary>Adapter đã sẵn sàng nhận lệnh chưa (DI đã dựng xong, level đã load).</summary>
        bool IsReady { get; }

        /// <summary>Tên hiển thị của level (vd "Level 12" hoặc theme). Rỗng nếu không có.</summary>
        string NameOf(int index);

        /// <summary>Load level theo index. Adapter tự clamp về vùng hợp lệ.</summary>
        void Load(int index);

        /// <summary>Reload level hiện tại từ đầu.</summary>
        void ReloadCurrent();
    }
}
