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

        public void Reset()
        {
            _level = FirstLevel;
            _save.SetInt(PreferenceKeys.Level, _level);
            _save.Save();
        }
    }
}
