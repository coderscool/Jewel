using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Người chơi đã từng tô được ô nào chưa — tính cho CẢ ĐỜI MÁY, không theo màn.
    ///
    /// Thuần C# — KHÔNG có using UnityEngine, nên test được ở EditMode. Cùng khuôn với
    /// PlayerProgress, PlayerWallet và HintCredits: giữ một giá trị, đọc lúc dựng, ghi
    /// mỗi lần đổi.
    ///
    /// Vì sao cần một cờ RIÊNG thay vì đọc trạng thái tô: "bảng này chưa tô ô nào" và
    /// "người này chưa bao giờ tô" là hai câu hoàn toàn khác nhau, mà cả hai đều cho ra
    /// cùng một bảng trống. Hướng dẫn mà đọc câu thứ nhất thì nó hiện lại mỗi lần vào
    /// màn 1 chưa tô — kể cả sau khi người chơi bấm nút Tô lại, tức là đúng lúc người ta
    /// đã thạo tới mức chủ động chơi lại.
    ///
    /// Cờ chỉ đi MỘT chiều: đã tô rồi thì không có đường quay lại. Đó là chủ ý — hướng
    /// dẫn cho người mới, mà không ai mới lại hai lần.
    public class TutorialState
    {
        private readonly ISaveService _save;
        private bool _hasPaintedOnce;

        public TutorialState(ISaveService save)
        {
            _save = save;
            _hasPaintedOnce = _save.GetBool(PreferenceKeys.HasPaintedOnce);
        }

        public bool HasPaintedOnce => _hasPaintedOnce;

        /// Gọi mỗi lần một ô được tô. Lần đầu thì ghi đĩa, những lần sau không làm gì.
        ///
        /// Rẻ nên gọi thoải mái: bên gọi không phải tự nhớ đã ghi hay chưa, và cũng không
        /// phải huỷ đăng ký sự kiện cho đúng lúc.
        public void MarkPainted()
        {
            if (_hasPaintedOnce) return;

            _hasPaintedOnce = true;

            _save.SetBool(PreferenceKeys.HasPaintedOnce, true);
            _save.Save();
        }
    }
}
