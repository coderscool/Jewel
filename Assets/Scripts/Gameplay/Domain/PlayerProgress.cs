using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Trạng thái tiến trình người chơi.
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

        /// Đặt thẳng mốc tiến trình.
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
