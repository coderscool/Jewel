namespace JewelPainter.Core.Services
{
    /// Nhạc nền. Tách khỏi SoundKey vì đây là hai thứ khác nhau về mọi mặt: nhạc thì
    /// LẶP, mỗi lúc chỉ có đúng một bản đang chạy, và nó theo công tắc Music chứ không
    /// theo công tắc Sound.
    ///
    /// Cùng luật đánh số như SoundKey: chỉ thêm vào cuối.
    public enum MusicKey
    {
        None = 0,
        Home = 1,
        Gameplay = 2,
    }
}
