using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Cho biết viên ngọc nên bay ra từ đâu khi người chơi tô một ô.
    public interface IPaintOriginProvider
    {
        /// Vị trí xuất phát của viên ngọc; false khi màu đó không hiện trên thanh.
        bool TryGetOriginWorldPosition(int paletteIndex, out Vector3 world);
    }
}
