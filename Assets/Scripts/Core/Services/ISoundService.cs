namespace JewelPainter.Core.Services
{
    public interface ISoundService
    {
        bool IsSoundEnabled { get; }
        bool IsMusicEnabled { get; }

        void Play(SoundKey key);
        void SetSoundEnabled(bool enabled);
        void SetMusicEnabled(bool enabled);
    }
}
