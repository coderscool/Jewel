namespace JewelPainter.Core.Messages
{
    /// Message báo một màn vừa bắt đầu.
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
