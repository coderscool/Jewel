namespace JewelPainter.UI.Definitions
{
    /// Popup mời thêm lượt đang nói về KHO LƯỢT NÀO. Chỉ là key định danh — không chứa
    /// logic, cùng vai với PopupKey.
    ///
    /// Cần một enum riêng thay vì để prefab tự kéo thả thẳng một HintCredits: ba kho đều
    /// là object thuần C# do container dựng, không phải Object của Unity nên không kéo
    /// vào Inspector được. Prefab chọn bằng TÊN, container đưa cả ba xuống, popup tự lấy
    /// đúng cái.
    ///
    /// Giá trị số phải GIỮ NGUYÊN: nó được lưu trong prefab. Thêm kho mới thì nối vào
    /// cuối, đừng chèn vào giữa — chèn giữa là mọi prefab từ chỗ đó trở đi lặng lẽ trỏ
    /// sang kho khác.
    public enum CreditPoolKind
    {
        /// Lượt gợi ý — nút chỉ ra một ô.
        Hint = 0,

        /// Lượt tô tự do — 20 giây tô ô nào cũng được.
        FreePaint = 1,

        /// Lượt tô hết màu đang chọn.
        FillColor = 2,
    }
}
