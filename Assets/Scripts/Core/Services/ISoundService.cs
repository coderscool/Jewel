namespace JewelPainter.Core.Services
{
    public interface ISoundService
    {
        bool IsSoundEnabled { get; }
        bool IsMusicEnabled { get; }

        void Play(SoundKey key);

        /// Bản nhạc đang được YÊU CẦU chạy — không phải bản đang nghe thấy. Tắt nhạc
        /// bằng công tắc thì con số này giữ nguyên, để bật lại là nó chạy tiếp đúng bản.
        MusicKey CurrentMusic { get; }

        /// Chuyển sang bản nhạc khác, fade chéo. Gọi lại đúng bản đang chạy thì không
        /// làm gì — nên bên gọi cứ gọi thoải mái mỗi lần đổi màn hình.
        void PlayMusic(MusicKey key);

        void StopMusic();

        void SetSoundEnabled(bool enabled);
        void SetMusicEnabled(bool enabled);
    }
}
