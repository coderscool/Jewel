namespace JewelPainter.Core.Persistence
{
    /// Nơi khai báo key lưu trữ.
    public static class PreferenceKeys
    {
        public const string Level = "level";
        public const string Coins = "coins";

        public const string PaintedPrefix = "painted_";
        public const string MusicEnabled = "music_enabled";
        public const string SoundEnabled = "sound_enabled";

        public const string VibrationEnabled = "vibration_enabled";

        public const string HintCredits = "hint_credits";

        public const string FreePaintCredits = "free_paint_credits";

        public const string FreePaintCreditsGranted = "free_paint_credits_granted";

        public const string FillColorCredits = "fill_color_credits";

        public const string FillColorCreditsGranted = "fill_color_credits_granted";

        public const string HasPaintedOnce = "has_painted_once";

        public const string HintCreditsGranted = "hint_credits_granted";

        public const string HasRated = "has_rated";

        public const string BoosterUnlockShownPrefix = "booster_unlock_shown_";

        public const string LevelsSinceRatePrompt = "levels_since_rate_prompt";
    }
}
