using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract do Gameplay định nghĩa cho việc tô màu. UI phụ thuộc interface này,
    /// Gameplay không bao giờ using ngược lên UI.
    public interface IPaintService
    {
        /// -1 khi chưa chọn màu nào.
        int SelectedPaletteIndex { get; }

        /// Các chỉ số màu ảnh thật sự dùng, tăng dần.
        IReadOnlyList<int> UsedPaletteIndices { get; }

        void SelectColor(int paletteIndex);

        /// Ô này có tô được bằng màu đang chọn không — cũng chính là điều kiện để nó
        /// đang hiện dấu gợi ý. false khi sai màu, đã tô, ngoài bảng, hoặc chưa chọn màu.
        ///
        /// Trong lúc FreePaintActive thì câu hỏi đổi thành "ô này có màu và chưa tô
        /// không" — bên gọi KHÔNG phải tự biết điều đó, cứ hỏi như thường.
        bool CanPaint(int x, int y);

        /// true nếu ô được tô lần này. Sai màu, đã tô, hoặc ngoài bảng đều trả false.
        bool TryPaint(int x, int y);

        /// false nếu chưa nạp lưới hoặc toạ độ ngoài bảng.
        bool IsPainted(int x, int y);

        /// Mọi ô có màu đều đã được tô. false khi chưa nạp lưới.
        bool IsComplete { get; }

        /// Chưa tô ô nào trong màn này, kể cả từ phiên chơi trước. false khi chưa nạp lưới.
        bool IsUntouched { get; }

        int RemainingFor(int paletteIndex);

        /// Ô chưa tô thứ `ordinal` (đếm từ 0) của một màu, quét trái→phải, trên→dưới.
        /// false khi không đủ ô. Cận trên hợp lệ của ordinal là RemainingFor(paletteIndex).
        bool TryGetUnpaintedCell(int paletteIndex, int ordinal, out Vector2Int cell);

        /// Tỉ lệ ô đã tô của một màu, thang 0..1. Dùng cho vòng tiến độ trên ô màu.
        float ProgressFor(int paletteIndex);

        /// Lưới mới đã sẵn sàng — thanh màu dựng lại từ đầu.
        event Action OnBoardReady;

        event Action<int> OnColorSelected;

        /// Có ai đó vừa CHỈ ĐỊNH một màu — kể cả khi đó đúng là màu đang chọn sẵn.
        ///
        /// Tách khỏi OnColorSelected vì hai câu hỏi khác nhau. OnColorSelected là "màu
        /// đang chọn đã ĐỔI", nên nó im lặng khi người chơi chỉ định lại đúng màu cũ —
        /// đúng cho mọi thứ đang nghe nó, vì chẳng có lớp nào phải dựng lại.
        ///
        /// Cái này là "hãy đưa màu này vào tầm mắt", và câu đó vẫn còn việc để làm khi
        /// màu không đổi: giữ tay vào một ô để bắt màu trong lúc thanh màu đang cuộn ở
        /// tận đầu kia thì ô màu ấy vẫn nằm ngoài màn hình.
        event Action<int> OnColorFocusRequested;

        event Action<Vector2Int, int> OnCellPainted;

        /// Người chơi vừa làm một việc cần có màu đang chọn, mà chưa chọn màu nào.
        ///
        /// Gộp cả hai đường vào một sự kiện — chạm ô tô được, và bấm nút gợi ý — để chỗ
        /// hiển thị chỉ phải nghe một chỗ.
        event Action OnColorRequired;

        /// Bên phát hiện gọi. Im lặng bỏ qua nếu thật ra đang có màu được chọn, nên bên
        /// gọi không cần tự kiểm tra trước.
        void RequireColor();

        /// Booster "tô tự do" đang chạy: MỌI ô chưa tô đều tô được, và tô ra chính màu
        /// của ô đó chứ không phải màu đang chọn.
        ///
        /// Nằm ở đây chứ không ở IFreePaintService vì đây là một LUẬT TÔ, và chỗ hỏi nó
        /// nhiều nhất là những chỗ vốn đã cầm IPaintService — lớp gợi ý, lớp nhận chạm.
        /// Cái nút, số lượt, đồng hồ đếm ngược thì thuộc về IFreePaintService.
        ///
        /// KHÔNG có hàm bật/tắt ở đây: chỉ Gameplay được phép bật, và nó cầm PaintManager
        /// chứ không cầm interface này.
        bool FreePaintActive { get; }

        /// Bắn khi booster bật hoặc tắt. Lớp gợi ý nghe cái này để dựng lại dấu hiệu.
        event Action<bool> OnFreePaintChanged;

        /// Đang có một booster chạy dở mà màu KHÔNG được đổi giữa chừng.
        ///
        /// Gộp hai nguồn vào một câu hỏi vì mọi chỗ hỏi đều hỏi đúng câu đó: tô tự do
        /// đang đếm ngược (đổi màu lúc ấy vô nghĩa — mọi ô đều tô được), và đợt tô của
        /// booster tô hết màu đang chạy (màu đã chốt từ lúc bấm nút, đổi giữa chừng chỉ
        /// làm người chơi tưởng đợt tô sẽ đổi theo).
        ///
        /// Ba cái nút booster cũng đọc chính cờ này thay vì tự kể ra từng trường hợp —
        /// thêm một booster nữa sau này thì chỉ phải thêm một nguồn, không phải đi sửa
        /// ba chỗ điều kiện.
        bool ColorLocked { get; }

        /// Bắn khi ColorLocked đổi. Chỉ bắn lúc ĐỔI.
        event Action<bool> OnColorLockChanged;

        /// Đã tô được ít nhất một ô ở màn đang chơi — cũng chính là điều kiện để nút
        /// Tô lại có việc để làm. false khi chưa nạp lưới.
        ///
        /// Không dùng !IsUntouched: hai câu đó trùng nhau hôm nay, nhưng IsUntouched là
        /// câu hỏi của phần hướng dẫn người chơi mới, còn đây là câu hỏi của một cái nút.
        /// Gộp lại thì sau này đổi luật cho bên này sẽ lặng lẽ đổi luôn bên kia.
        bool CanReset { get; }

        /// Xoá sạch tiến độ tô của màn đang chơi rồi nạp lại màn đó từ đầu.
        ///
        /// Đi qua đúng luồng nạp màn thật, không tự dọn bảng: mọi lớp hiển thị đều dựng
        /// lại theo OnLevelStarted, nên tự xoá tay sẽ bỏ sót đúng những lớp mà người viết
        /// quên mất là có.
        void ResetCurrentLevel();
    }
}
