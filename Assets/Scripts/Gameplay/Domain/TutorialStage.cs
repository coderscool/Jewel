namespace JewelPainter.Gameplay.Domain
{
    /// Màn hướng dẫn đang ở nhịp nào.
    ///
    /// Cần một enum chứ không phải một cờ bool, vì hai nhịp khoá những thứ KHÁC NHAU:
    /// nhịp đầu bảng đứng yên hoàn toàn, nhịp sau người chơi phải tô được — đó chính là
    /// thứ đang được dạy — nhưng vẫn chưa được kéo hay zoom.
    ///
    /// Thuần C#, nằm ở Domain để cả Gameplay lẫn UI cùng đọc được. Xem TutorialState.
    public enum TutorialStage
    {
        /// Không có hướng dẫn nào chạy. Mọi thứ mở bình thường.
        None = 0,

        /// Ngón tay chỉ vào ô màu. Bảng đứng yên hoàn toàn, thanh màu không cuộn, chỉ
        /// đúng một ô màu bấm được.
        PickColor = 1,

        /// Ngón tay kéo qua những ô đang hiện gợi ý. Người chơi TÔ ĐƯỢC, nhưng vẫn chưa
        /// kéo/zoom bảng và chưa cuộn được thanh màu — cho tới khi ô đầu tiên được tô.
        PaintCells = 2,

        /// Camera đã thu về toàn cảnh, ngón tay quay lại chỉ vào thanh màu. KHÔNG khoá gì
        /// nữa: mọi ô màu đều chọn được, bảng kéo và zoom được, thanh màu cuộn được.
        ///
        /// Nhịp này chỉ còn là một lời mời. Người chơi đã tô được ô đầu tiên, tức là đã
        /// biết đủ để tự đi tiếp — giữ họ lại thêm một giây nào nữa cũng là giữ vô cớ.
        PickNextColor = 3,
    }
}
