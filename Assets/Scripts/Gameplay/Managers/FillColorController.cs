using System;
using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Board;
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
        [Tooltip("Cả đợt tô nên kéo dài chừng bao nhiêu GIÂY. Đây là núm chính.\n\n" +
                 "Tốc độ được suy ra từ số ô: màu ít ô thì bắn thưa, màu nhiều ô thì bắn " +
                 "dày, và cả hai xong trong cùng khoảng thời gian. Đặt một tốc độ cố định " +
                 "thay vì thế thì màu 60 ô xong trong nháy mắt còn màu 2000 ô bắt người " +
                 "chơi ngồi nhìn cả chục giây.")]
        [SerializeField] private float _fillSeconds = 2.5f;

        [Tooltip("Sàn tốc độ, tính bằng ô mỗi giây. Màu ít ô đến mấy cũng không được bắn " +
                 "chậm hơn ngần này — không thì màu còn 5 ô cuối lại lê ra đủ Fill Seconds.")]
        [SerializeField] private float _minCellsPerSecond = 40f;

        [Tooltip("Trần tốc độ, tính bằng ô mỗi giây. Màu nhiều ô sẽ tô lâu hơn Fill Seconds " +
                 "chứ không được vượt trần này.\n\n" +
                 "Trần tồn tại vì hạn mức viên bay: số viên trên trời cùng lúc xấp xỉ " +
                 "(tốc độ) × (thời gian bay, chừng 0.5 giây). 700 ô/giây là chừng 350 viên, " +
                 "vừa khớp Burst Max Concurrent 420 của JewelFlyEffect. Đẩy trần lên cao hơn " +
                 "mà không nới hạn mức kia thì phần vượt sẽ hiện ngay, không có ngọc bay.")]
        [SerializeField] private float _maxCellsPerSecond = 700f;

        [Tooltip("Mọi ô trong đợt tô đều có viên ngọc bay ra từ ô màu trên thanh chọn, " +
                 "y hệt lúc tô tay — không phải chỉ vài chục ô đầu.\n\n" +
                 "Không đổi hiệu ứng bay, chỉ nới hạn mức viên bay cùng lúc của " +
                 "JewelFlyEffect trong lúc đợt tô chạy (Burst Max Concurrent). Bỏ tick là " +
                 "về đúng hành vi cũ: hai chục ô đầu có ngọc bay, phần còn lại hiện ngay.")]
        [SerializeField] private bool _flyEveryCell = true;

        private IPaintService _paintService;
        private PaintManager _paintManager;
        private FillColorCredits _credits;
        private JewelFlyEffect _flyEffect;

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

                // Booster khác đang chạy dở thì khoá. Hai booster chồng lên nhau là một
                // mớ: đợt tô bắn hàng trăm viên trong lúc người chơi đang chạy đua với
                // đồng hồ, mà số ô nó tô mất lại chính là số ô đáng lẽ họ tự tô được
                // trong ngần ấy giây vừa trả tiền.
                if (_paintService.ColorLocked) return false;

                // Hỏi CẢ BẢNG, không hỏi riêng màu đang chọn — và cũng không đòi phải chọn
                // màu. Chưa chọn màu vẫn cho BẤM: cú bấm đó mở lời nhắc chọn màu, xem
                // chú thích ở IFillColorService.CanUse.
                return !_paintService.IsComplete;
            }
        }

        /// flyEffect được phép null — để trống thì booster vẫn tô đúng như cũ, chỉ là
        /// không nới được hạn mức viên bay nên phần lớn ô sẽ hiện ngay không có ngọc bay.
        public void Init(PaintManager paintManager, FillColorCredits credits, JewelFlyEffect flyEffect)
        {
            _paintManager = paintManager;
            _paintService = paintManager;
            _credits = credits;
            _flyEffect = flyEffect;

            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;
            _paintService.OnColorLockChanged += HandleColorLockChanged;

            _lastAvailability = CanUse;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

            if (_paintService == null) return;

            _paintService.OnBoardReady -= HandleBoardReady;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnColorLockChanged -= HandleColorLockChanged;
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

            // Nới hạn mức TRƯỚC khi coroutine chạy ô đầu tiên, không thì mấy ô đầu đã
            // kịp tràn hạn mức cũ và hiện ngay không có ngọc bay.
            if (BurstActive) _flyEffect.SetBurstMode(true);

            // Khoá màu suốt đợt tô. Màu đã chốt từ lúc bấm nút, nên để người chơi bấm
            // sang màu khác giữa chừng chỉ dựng lên một lời hứa mà đợt tô không giữ:
            // họ thấy màu mới được chọn nhưng ngọc vẫn tiếp tục rơi ra màu cũ.
            _paintManager.SetFillLock(true);

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

        /// Có nới hạn mức thật không — có tick VÀ có JewelFlyEffect để mà nhờ.
        private bool BurstActive => _flyEveryCell && _flyEffect != null;

        private IEnumerator FillRoutine()
        {
            var rate = ResolveRate(_cells.Count);

            // Hạn ngạch CỘNG DỒN chứ không cấp lại mỗi frame.
            //
            // Tốc độ 40 ô/giây ở 60fps là 0.67 ô mỗi frame — cấp lại mỗi frame thì phần
            // lẻ ấy không bao giờ đủ một ô và đợt tô đứng hình. Cộng dồn thì cứ chừng
            // một frame rưỡi lại đủ để bắn một viên, đúng như ý.
            var budget = 0f;

            while (_filled < _cells.Count)
            {
                // Nhân với deltaTime, không đếm theo frame. Đếm theo frame nghĩa là máy
                // 120Hz tô nhanh gấp đôi máy 60Hz, và máy tụt xuống 30fps thì đợt tô dài
                // gấp đôi — cùng một booster mà mỗi máy một tốc độ.
                budget += rate * Time.deltaTime;

                while (budget >= 1f && _filled < _cells.Count)
                {
                    var cell = _cells[_filled++];

                    // TryPaintAs tự kiểm biên và tự bỏ qua ô đã tô, nên ô mà người chơi
                    // vừa tô tay trong lúc đợt màu đang chạy chỉ đơn giản là không được
                    // tô lần hai — không cần lọc lại danh sách. Ô bị bỏ qua cũng KHÔNG
                    // trừ hạn ngạch: nó không sinh viên ngọc nào nên không tốn gì cả.
                    if (_paintManager.TryPaintAs(cell.x, cell.y, _fillPalette)) budget -= 1f;
                }

                yield return null;
            }

            EndFill();
        }

        /// Bao nhiêu ô mỗi giây, suy từ số ô sao cho cả đợt gọn trong Fill Seconds — rồi
        /// kẹp lại giữa sàn và trần.
        private float ResolveRate(int count)
        {
            var seconds = Mathf.Max(0.05f, _fillSeconds);
            var min = Mathf.Max(1f, _minCellsPerSecond);
            var max = Mathf.Max(min, _maxCellsPerSecond);

            return Mathf.Clamp(count / seconds, min, max);
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
            // Tắt ngay ở đây chứ không đợi viên cuối đáp xuống: cờ này chỉ quyết định
            // viên MỚI có được cấp chỗ hay không, còn mấy trăm viên đang giữa trời vẫn
            // bay nốt bình thường.
            if (_flyEffect != null) _flyEffect.SetBurstMode(false);

            _paintManager.SetFillLock(false);

            _fill = null;
            _fillPalette = -1;
            _filled = 0;
            _cells.Clear();

            OnFillingChanged?.Invoke(false);
            RefreshAvailability();
        }

        private void HandleCreditsChanged(int remaining) => OnCreditsChanged?.Invoke(remaining);

        private void HandleColorSelected(int paletteIndex) => RefreshAvailability();

        private void HandleColorLockChanged(bool locked) => RefreshAvailability();

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
