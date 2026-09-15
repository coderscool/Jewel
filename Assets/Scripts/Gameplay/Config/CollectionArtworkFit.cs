namespace JewelPainter.Gameplay.Config
{
    /// Bức tranh của màn này nằm thế nào trong ô bộ sưu tập.
    ///
    /// Là lựa chọn của TỪNG MÀN chứ không phải một luật chung, vì nó phụ thuộc vào chính
    /// bức ảnh: ảnh vẽ tràn hết khung tranh thì chạm sát mép ô nhìn mới đầy đặn, còn ảnh
    /// có nền trắng hoặc có viền sẵn thì chạm sát mép là hai lớp lề chồng lên nhau và ô
    /// trông chật.
    ///
    /// Giá trị số phải GIỮ NGUYÊN: nó được lưu trong LevelConfig asset. Thêm kiểu mới thì
    /// nối vào cuối, đừng chèn vào giữa — chèn giữa là mọi màn từ chỗ đó trở đi lặng lẽ
    /// đổi sang kiểu khác.
    public enum CollectionArtworkFit
    {
        /// Tranh ăn sát mép ô. Cạnh dài chạm khung trước thì dừng ở đó, cạnh kia hở ra
        /// hai dải nền — vẫn giữ đúng tỉ lệ, không bị bóp méo.
        Full = 0,

        /// Như trên nhưng thu vào cả bốn phía một quãng, để bức tranh có lề thở.
        /// Quãng thu là bao nhiêu thì do CollectionItemView quyết định, không phải màn.
        Inset = 1,
    }
}
