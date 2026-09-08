using JewelPainter.Gameplay.Interfaces;

namespace JewelPainter.Gameplay.Board
{
    /// Lớp hiện chỉ số bảng màu lên từng ô. Có hai bản cài đặt đổi chỗ được cho nhau:
    ///
    ///   BoardNumberLayer — mỗi ô một TextMeshPro riêng. Đơn giản, nhưng số object bằng
    ///                      số ô trong tầm nhìn, và ở bảng lớn con số đó là hàng nghìn.
    ///   BoardNumberMesh  — CẢ BẢNG gom vào một mesh duy nhất, một draw call. Kéo và
    ///                      zoom không tốn gì vì không có object nào để cull hay sắp xếp.
    ///
    /// Cùng khuôn với IBoardGridLines: GameEntryPoint đi qua interface này nên đổi bản
    /// cài đặt chỉ phải sửa một dòng đăng ký trong GameLifetimeScope.
    ///
    /// flyEffect là nguồn sự thật cho "ô này xong chưa" — số chỉ được gỡ khi viên ngọc
    /// ĐÁP XUỐNG, không phải lúc bấm tô. Gỡ sớm thì ô trống trơn suốt quãng viên đang bay.
    public interface IBoardNumbers
    {
        void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect);
    }
}
