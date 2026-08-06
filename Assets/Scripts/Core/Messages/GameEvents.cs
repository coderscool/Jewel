namespace JewelPainter.Core.Messages
{
    /// Chỉ khai báo KIỂU DỮ LIỆU của message, không chứa logic.
    /// Project chưa dùng MessagePipe nên các manager tự expose event Action&lt;T&gt;.
    /// Khi cài MessagePipe, đăng ký các struct này làm kênh trong LifetimeScope.
    public readonly struct LevelStartedMessage
    {
        public readonly int LevelId;

        public LevelStartedMessage(int levelId) => LevelId = levelId;
    }

    public readonly struct LevelCompletedMessage
    {
        public readonly int LevelId;

        public LevelCompletedMessage(int levelId) => LevelId = levelId;
    }
}
