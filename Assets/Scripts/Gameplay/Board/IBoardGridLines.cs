namespace JewelPainter.Gameplay.Board
{
    /// Lớp kẻ viền quanh các ô có màu. Có hai bản cài đặt đổi chỗ được cho nhau:
    ///
    ///   BoardGridLines       — nướng sẵn nét vào một texture cỡ cả bảng.
    ///   BoardGridLinesShaded — chỉ giữ mask một texel mỗi ô, nét do shader kẻ tại chỗ.
    ///
    /// GameEntryPoint đi qua interface này nên đổi bản cài đặt chỉ phải sửa một dòng
    /// đăng ký trong GameLifetimeScope, không đụng tới thứ tự nối dây.
    public interface IBoardGridLines
    {
        void Init(BoardView boardView);
    }
}
