using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Trạng thái tiến trình người chơi. Thuần C# — KHÔNG có using UnityEngine,
    /// nên chạy được trong EditMode test mà không cần vào Play Mode.
    /// Đây là file mẫu cho luật: logic càng quan trọng càng phải ít phụ thuộc Unity.
    public class PlayerProgress
    {
        private const int FirstLevel = 1;

        private readonly ISaveService _save;
        private int _level;

        public PlayerProgress(ISaveService save)
        {
            _save = save;
            _level = _save.GetInt(PreferenceKeys.Level, FirstLevel);
        }

        public int Level => _level;

        public void Advance()
        {
            _level++;
            _save.SetInt(PreferenceKeys.Level, _level);
            _save.Save();
        }

        /// Đặt THẲNG mốc tiến trình, không đi qua Advance từng bước.
        ///
        /// Chỉ công cụ dev gọi (CheatKit: Unlock All / Set Progress). Luồng chơi thật vẫn
        /// đi qua Advance — nó là thứ mô tả "vừa xong một màn", còn hàm này chỉ là đặt số.
        ///
        /// Kẹp về màn đầu thay vì ném lỗi: cheat gõ nhầm một con số âm không đáng làm sập
        /// game, mà bảng trống vì tiến trình bằng 0 thì còn khó hiểu hơn.
        public void SetLevel(int level)
        {
            if (level < FirstLevel) level = FirstLevel;
            if (level == _level) return;

            _level = level;
            _save.SetInt(PreferenceKeys.Level, _level);
            _save.Save();
        }

        public void Reset()
        {
            _level = FirstLevel;
            _save.SetInt(PreferenceKeys.Level, _level);
            _save.Save();
        }
    }
}
