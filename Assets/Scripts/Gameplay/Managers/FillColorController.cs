using System;
using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Booster "tô hết màu": tô nốt mọi ô còn lại của màu đang chọn.
    ///
    /// Tô RẢI QUA NHIỀU FRAME chứ không tô hết trong một lời gọi, và đó là điểm quan
    /// trọng nhất của lớp này. Mỗi ô được tô là một OnCellPainted, và bám sau sự kiện đó
    /// là cả một dây chuyền: viên ngọc bay, lớp ngọc dựng object, lớp gợi ý gỡ marker,
    /// kho tiến độ đánh dấu bẩn, thanh màu cập nhật vòng tiến độ. Đổ 2000 ô vào một frame
    /// là 2000 lần cả dây chuyền đó chạy trước khi hình kịp vẽ — game đứng hình vài giây
    /// rồi bức tranh hiện ra đã tô xong, mất sạch phần nhìn mà người chơi vừa trả lượt để
    /// được xem.
    ///
    /// Rải ra thì nó đọc thành một đợt màu quét từ trên xuống, và đó mới là thứ đáng tiền.
    ///
    /// Đứng ở Gameplay chứ không nằm trong HudView vì "màu này còn ô nào" là trạng thái
    /// luật chơi. HudView chỉ bấm nút và bật/tắt nút theo IFillColorService.
    public class FillColorController : MonoBehaviour, IFillColorService
    {
        [Tooltip("Mỗi frame tô bao nhiêu ô.\n\n" +
                 "Đây là núm chỉnh giữa NHANH và ĐẸP. Để thấp thì đợt màu quét chậm rãi " +
                 "nhưng màu nhiều ô phải chờ lâu; để cao thì xong nhanh mà gần như không " +
                 "kịp thấy gì.\n\n" +
                 "24 với màu 2000 ô là chừng 1.4 giây ở 60fps. Lưu ý viên ngọc bay có hạn " +
                 "mức riêng (Jewel Fly Effect → Max Concurrent, mặc định 24): vượt quá thì " +
                 "ô vẫn được tô, chỉ là ngọc hiện ngay không bay — nên đẩy số này lên rất " +
                 "cao cũng không làm ngọc bay nhiều hơn, chỉ làm mất đợt quét.")]
        [SerializeField] private int _cellsPerFrame = 24;

        private IPaintService _paintService;
        private PaintManager _paintManager;
        private FillColorCredits _credits;

        /// Danh sách ô của cú tô đang chạy. Dùng lại qua các lần bấm — cấp phát mới mỗi
        /// lần là vài nghìn phần tử rác đúng lúc bảng đang bận nhất.
        private readonly List<Vector2Int> _cells = new();

        private Coroutine _fill;

        /// Màu đã chốt lúc bấm nút. Giữ lại để cú tô hoàn tất đúng màu người chơi đã trả
        /// lượt cho, kể cả khi họ bấm sang màu khác giữa chừng.
        private int _fillPalette = -1;

        private int _filled;

        /// Giá trị đã báo ra lần gần nhất — chỉ bắn sự kiện khi thật sự đổi. Cùng lý do
        /// đã ghi ở HintFocusController.
        private bool _lastAvailability;

        public event Action<bool> OnFillingChanged;
        public event Action<int> OnCreditsChanged;
        public event Action OnCreditsExhausted;
        public event Action<bool> OnAvailabilityChanged;

        public bool IsFilling => _fill != null;

        public float FillProgress
        {
            get
            {
                if (!IsFilling || _cells.Count <= 0) return 1f;

                return Mathf.Clamp01(_filled / (float)_cells.Count);
            }
        }

        public int RemainingCredits => _credits?.Remaining ?? 0;

        public bool CanUse
        {
            get
            {
                if (_paintService == null) return false;

                // Đang tô dở thì thôi: bấm chồng lên nhau chỉ tốn thêm một lượt để làm
                // đúng việc đang làm.
                if (IsFilling) return false;

                // Hỏi CẢ BẢNG, không hỏi riêng màu đang chọn — và cũng không đòi phải chọn
                // màu. Chưa chọn màu vẫn cho BẤM: cú bấm đó mở lời nhắc chọn màu, xem
                // chú thích ở IFillColorService.CanUse.
                return !_paintService.IsComplete;
            }
        }

        public void Init(PaintManager paintManager, FillColorCredits credits)
        {
            _paintManager = paintManager;
            _paintService = paintManager;
            _credits = credits;

            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;

            _lastAvailability = CanUse;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

            if (_paintService == null) return;

            _paintService.OnBoardReady -= HandleBoardReady;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnCellPainted -= HandleCellPainted;
        }

        public bool Use()
        {
            if (_paintService == null) return false;

            // Chưa chọn màu thì đây không phải một lần dùng booster, chỉ là một cú bấm
            // nhầm — mở lời nhắc và KHÔNG trừ lượt. Trừ ở đây là ăn cắp của người chơi.
            var paletteIndex = _paintService.SelectedPaletteIndex;
            if (paletteIndex < 0)
            {
                _paintService.RequireColor();
                return false;
            }

            if (!CanUse) return false;

            // Gom ô TRƯỚC khi trừ lượt: màu đang chọn hết ô thì không có gì để tô, mà
            // lượt thì đã mất. Mọi đường thoát phải nằm hết trên cú trừ.
            if (_paintManager.CollectUnpainted(paletteIndex, _cells) <= 0) return false;

            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            _fillPalette = paletteIndex;
            _filled = 0;
            _fill = StartCoroutine(FillRoutine());

            OnFillingChanged?.Invoke(true);
            RefreshAvailability();

            return true;
        }

        /// Cắt cú tô đang chạy. Công khai để cheat hoặc luồng khác dừng được.
        public void StopFilling()
        {
            if (_fill == null) return;

            StopCoroutine(_fill);
            EndFill();
        }

        private IEnumerator FillRoutine()
        {
            var perFrame = Mathf.Max(1, _cellsPerFrame);

            while (_filled < _cells.Count)
            {
                var budget = perFrame;

                while (budget > 0 && _filled < _cells.Count)
                {
                    var cell = _cells[_filled++];

                    // TryPaintAs tự kiểm biên và tự bỏ qua ô đã tô, nên ô mà người chơi
                    // vừa tô tay trong lúc đợt màu đang chạy chỉ đơn giản là không được
                    // tô lần hai — không cần lọc lại danh sách.
                    if (_paintManager.TryPaintAs(cell.x, cell.y, _fillPalette)) budget--;
                }

                yield return null;
            }

            EndFill();
        }

        /// Màn đổi thì cắt ngay. Toạ độ ô của màn cũ không còn nghĩa gì trên lưới mới, mà
        /// coroutine thì không tự biết chuyện đó — nó vẫn chạy tiếp và tô bừa vào bảng mới.
        private void HandleBoardReady()
        {
            StopFilling();
            RefreshAvailability();
        }

        private void EndFill()
        {
            _fill = null;
            _fillPalette = -1;
            _filled = 0;
            _cells.Clear();

            OnFillingChanged?.Invoke(false);
            RefreshAvailability();
        }

        private void HandleCreditsChanged(int remaining) => OnCreditsChanged?.Invoke(remaining);

        private void HandleColorSelected(int paletteIndex) => RefreshAvailability();

        private void HandleCellPainted(Vector2Int cell, int paletteIndex) => RefreshAvailability();

        private void RefreshAvailability()
        {
            var available = CanUse;
            if (available == _lastAvailability) return;

            _lastAvailability = available;
            OnAvailabilityChanged?.Invoke(available);
        }
    }
}
