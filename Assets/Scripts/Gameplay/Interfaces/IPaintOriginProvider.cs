using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Cho biết viên ngọc nên bay ra TỪ ĐÂU khi người chơi tô một ô.
    ///
    /// Contract do Gameplay định nghĩa, UI hiện thực — hiệu ứng chạy trong world cần
    /// biết vị trí ô màu trên thanh chọn, mà thanh đó thuộc tầng UI. Gameplay không
    /// được using lên UI, nên đảo lại: bên cần thì khai contract, bên có thì cung cấp.
    public interface IPaintOriginProvider
    {
        /// false khi màu đó không có ô nào đang hiện trên thanh — lúc đó bỏ qua hiệu ứng.
        bool TryGetOriginWorldPosition(int paletteIndex, out Vector3 world);
    }
}
