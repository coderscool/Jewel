namespace CoreModules.CheatKit.Ports
{
    /// <summary>
    /// Gói các port mà adapter của game cung cấp cho cheat modules. Field nào game không
    /// hỗ trợ thì để <c>null</c> — module tự ẩn/disable phần tương ứng (graceful degradation).
    /// Đây là DTO bất biến truyền 1 chiều từ installer → panel → modules (qua <see cref="ICheatBindable"/>).
    /// </summary>
    public sealed class CheatServices
    {
        public ILevelCheatService Level { get; }
        public IFlowCheatService Flow { get; }
        public IProgressCheatService Progress { get; }

        // Túi service GENRE tuỳ chọn (RPG/Action…): module đặc thù lấy custom port qua Get<T>().
        // Giữ panel/kit ĐÓNG với sửa đổi, MỞ với mở rộng (OCP) — thêm genre = thêm extra, không sửa DTO.
        private readonly object[] _extras;

        public CheatServices(
            ILevelCheatService level = null,
            IFlowCheatService flow = null,
            IProgressCheatService progress = null,
            params object[] extras)
        {
            Level = level;
            Flow = flow;
            Progress = progress;
            _extras = extras;
        }

        /// <summary>
        /// Lấy port theo kiểu — ưu tiên <c>extras</c> (custom genre port) rồi tới 3 port chuẩn.
        /// Zero-reflection (chỉ <c>is</c>); trả null nếu không có → module tự ẩn/disable (graceful).
        /// </summary>
        public T Get<T>() where T : class
        {
            if (_extras != null)
                for (int i = 0; i < _extras.Length; i++)
                    if (_extras[i] is T match) return match;

            if (Level is T l) return l;
            if (Flow is T f) return f;
            if (Progress is T p) return p;
            return null;
        }
    }
}
