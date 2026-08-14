namespace JewelPainter.Core.Persistence
{
    /// Nơi duy nhất khai báo key lưu trữ. Không rải chuỗi thô trong code.
    public static class PreferenceKeys
    {
        public const string Level = "level";

        /// Nối thêm Level Id vào sau: "painted_3". Mỗi màn một key riêng để xoá màn đã
        /// xong không đụng tới màn khác.
        public const string PaintedPrefix = "painted_";
        public const string MusicEnabled = "music_enabled";
        public const string SoundEnabled = "sound_enabled";
    }
}
