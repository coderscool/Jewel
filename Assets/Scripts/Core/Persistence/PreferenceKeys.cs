namespace JewelPainter.Core.Persistence
{
    /// Nơi duy nhất khai báo key lưu trữ. Không rải chuỗi thô trong code.
    public static class PreferenceKeys
    {
        public const string Level = "level";
        public const string Coins = "coins";

        /// Nối thêm Level Id vào sau: "painted_3". Mỗi màn một key riêng để xoá màn đã
        /// xong không đụng tới màn khác.
        public const string PaintedPrefix = "painted_";
        public const string MusicEnabled = "music_enabled";
        public const string SoundEnabled = "sound_enabled";

        /// Số lượt gợi ý miễn phí còn lại.
        public const string HintCredits = "hint_credits";

        /// Người chơi đã từng tô được ô nào chưa. Hướng dẫn ngón tay đọc cờ này để chỉ
        /// hiện đúng một lần trong đời máy.
        public const string HasPaintedOnce = "has_painted_once";

        /// Đã phát lượt khởi đầu chưa. Tách khỏi con số ở trên vì "còn 0 lượt" và "chưa
        /// bao giờ được phát" là hai trạng thái khác nhau, mà cả hai đều đọc ra số 0.
        public const string HintCreditsGranted = "hint_credits_granted";

        /// Đã bấm nút đánh giá chưa. Bấm rồi thì popup mời đánh giá tắt vĩnh viễn.
        public const string HasRated = "has_rated";

        /// Số màn đã tô xong kể từ lần mời đánh giá gần nhất. Đếm lại từ 0 sau mỗi lần
        /// mời, nên bỏ qua một lần thì phải xong thêm đủ số màn nữa mới bị hỏi lại.
        public const string LevelsSinceRatePrompt = "levels_since_rate_prompt";
    }
}
