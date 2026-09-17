using JewelPainter.Gameplay.Interfaces;

namespace JewelPainter.Gameplay.Board
{
    /// Lớp hiện chỉ số bảng màu lên từng ô.
    public interface IBoardNumbers
    {
        void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect);
    }
}
