namespace JewelPainter.Core.Services
{
    public interface ISoundService
    {
        bool IsSoundEnabled { get; }
        bool IsMusicEnabled { get; }

        void Play(SoundKey key);

        MusicKey CurrentMusic { get; }

        /// Chuyển sang bản nhạc khác, fade chéo.
        void PlayMusic(MusicKey key);

        void StopMusic();

        void SetSoundEnabled(bool enabled);
        void SetMusicEnabled(bool enabled);
    }
}
