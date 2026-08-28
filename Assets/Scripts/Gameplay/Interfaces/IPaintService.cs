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

        event Action<Vector2Int, int> OnCellPainted;

        /// Người chơi vừa làm một việc cần có màu đang chọn, mà chưa chọn màu nào.
        ///
        /// Gộp cả hai đường vào một sự kiện — chạm ô tô được, và bấm nút gợi ý — để chỗ
        /// hiển thị chỉ phải nghe một chỗ.
        event Action OnColorRequired;

        /// Bên phát hiện gọi. Im lặng bỏ qua nếu thật ra đang có màu được chọn, nên bên
        /// gọi không cần tự kiểm tra trước.
        void RequireColor();

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
