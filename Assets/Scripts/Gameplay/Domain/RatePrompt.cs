using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Đếm xem đã đến lúc mời người chơi đánh giá chưa.
    public class RatePrompt
    {
        private readonly ISaveService _save;
        private readonly int _levelsPerPrompt;

        private int _clearedSincePrompt;
        private bool _hasRated;

        public RatePrompt(ISaveService save, int levelsPerPrompt)
        {
            _save = save;

            _levelsPerPrompt = levelsPerPrompt < 1 ? 1 : levelsPerPrompt;

            _hasRated = _save.GetBool(PreferenceKeys.HasRated);
            _clearedSincePrompt = _save.GetInt(PreferenceKeys.LevelsSinceRatePrompt);
        }

        public bool HasRated => _hasRated;

        /// Gọi mỗi lần người chơi tô xong một màn.
        public bool RegisterLevelCleared()
        {
            if (_hasRated) return false;

            _clearedSincePrompt++;
            Persist();

            return _clearedSincePrompt >= _levelsPerPrompt;
        }

        /// Ghi nhận popup đánh giá đã hiện.
        public void MarkPrompted()
        {
            if (_hasRated || _clearedSincePrompt == 0) return;

            _clearedSincePrompt = 0;
            Persist();
        }

        /// Ghi nhận người chơi đã bấm đánh giá.
        public void MarkRated()
        {
            if (_hasRated) return;

            _hasRated = true;
            _clearedSincePrompt = 0;

            _save.SetBool(PreferenceKeys.HasRated, true);
            _save.SetInt(PreferenceKeys.LevelsSinceRatePrompt, 0);
            _save.Save();
        }

        private void Persist()
        {
            _save.SetInt(PreferenceKeys.LevelsSinceRatePrompt, _clearedSincePrompt);
            _save.Save();
        }
    }
}
