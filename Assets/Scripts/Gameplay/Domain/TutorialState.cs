using System;
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

        /// Hướng dẫn ĐANG chạy trên màn hình ngay lúc này.
        ///
        /// KHÔNG ghi xuống đĩa, khác hẳn cờ trên. Hai cờ nói hai loại chuyện: cái trên là
        /// tiểu sử của người chơi và phải sống qua mọi lần mở app; cái này là một trạng
        /// thái tồn tại vài giây rồi thôi. Lưu nó lại là mở đường cho một cú tắt app giữa
        /// chừng khoá cứng bảng chơi ở lần mở sau.
        ///
        /// Nằm ở ĐÂY chứ không ở TutorialOverlayView, dù view mới là thứ biết rõ nhất, vì
        /// người cần hỏi nằm ở tầng dưới: BoardInput thuộc Gameplay, mà Gameplay thì không
        /// được phép nhìn lên UI. Một cờ trong Domain thì cả hai tầng cùng với tới, và
        /// chiều phụ thuộc vẫn đi đúng một hướng.
        public TutorialStage Stage { get; private set; } = TutorialStage.None;

        /// Có hướng dẫn nào đang chạy không, bất kể nhịp nào.
        public bool IsRunning => Stage != TutorialStage.None;

        /// Nhịp này có khoá thao tác của người chơi không.
        ///
        /// Tách hẳn khỏi IsRunning vì nhịp cuối CÓ chạy mà KHÔNG khoá gì. Trước đây hai
        /// câu hỏi đó trùng nhau nên một cờ là đủ; giờ thì không, và chỗ nào hỏi nhầm câu
        /// sẽ khoá cứng người chơi ở đúng nhịp đáng lẽ đã thả họ ra.
        ///
        /// Một property ở đây chứ không để mỗi chỗ tự so với hai giá trị enum: thêm nhịp
        /// thứ tư là phải sửa mọi chỗ đó, mà quên một chỗ thì không có gì báo.
        public bool LocksInput =>
            Stage == TutorialStage.PickColor || Stage == TutorialStage.PaintCells;

        /// Bắn mỗi lần đổi nhịp, kèm nhịp MỚI.
        ///
        /// Cần sự kiện chứ không chỉ cờ vì có bên phải làm việc ở đúng khoảnh khắc chuyển:
        /// thanh màu tắt trục cuộn lúc hướng dẫn bắt đầu, và phải bật lại lúc nó thả ra.
        /// Chỉ đọc cờ thì không ai báo cho nó biết lúc nào mà bật lại.
        ///
        /// Mang cả NHỊP chứ không chỉ một bool: chỗ nghe cần phân biệt chúng — lời nhắc
        /// "hãy chọn màu" chỉ đi cùng nhịp 1, không đi cùng hai nhịp còn lại.
        ///
        /// Ai đăng ký nhớ gỡ: lớp này sống suốt phiên chơi, lâu hơn mọi view nghe nó.
        public event Action<TutorialStage> OnStageChanged;

        /// Chỉ TutorialOverlayView gọi, mỗi lần hướng dẫn sang nhịp khác.
        public void SetStage(TutorialStage stage)
        {
            if (Stage == stage) return;

            Stage = stage;

            OnStageChanged?.Invoke(stage);
        }

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
