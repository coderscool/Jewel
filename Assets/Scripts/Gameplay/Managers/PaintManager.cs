using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// MonoBehaviour mỏng: giữ PaintState và phát sự kiện.
    /// Toàn bộ luật tô nằm ở PaintState (thuần C#).
    public class PaintManager : MonoBehaviour, IPaintService
    {
        private static readonly int[] NoIndices = Array.Empty<int>();

        private ILevelService _levelService;
        private PaintProgressStore _progressStore;
        private PaintState _state;

        /// Màn đang NẠP. Khác CurrentLevel của ILevelService — con số đó là mốc tiến
        /// trình, và hai bên tách nhau từ khi Home cho chơi lại màn cũ.
        private int _loadedLevel = -1;

        public int SelectedPaletteIndex { get; private set; } = -1;

        public IReadOnlyList<int> UsedPaletteIndices =>
            _state != null ? _state.UsedPaletteIndices : NoIndices;

        public event Action OnBoardReady;
        public event Action<int> OnColorSelected;
        public event Action<int> OnColorFocusRequested;
        public event Action<Vector2Int, int> OnCellPainted;
        public event Action OnColorRequired;
        public event Action<bool> OnFreePaintChanged;
        public event Action<bool> OnColorLockChanged;

        /// Đợt tô của booster tô hết màu đang chạy. Nguồn khoá thứ hai bên cạnh
        /// FreePaintActive — xem SetFillLock.
        private bool _fillLock;

        /// Giá trị ColorLocked đã báo ra lần gần nhất, để chỉ bắn sự kiện khi thật sự đổi.
        private bool _lastColorLock;

        public bool FreePaintActive { get; private set; }

        /// Bật/tắt luật tô tự do. KHÔNG nằm trên IPaintService — chỉ FreePaintController
        /// gọi, và nó cầm thẳng PaintManager. Để UI gọi được thì cái nút sẽ đi tắt qua
        /// mọi thứ đứng giữa: số lượt, đồng hồ, luật "màn xong rồi thì thôi".
        public void SetFreePaint(bool active)
        {
            if (FreePaintActive == active) return;

            FreePaintActive = active;
            OnFreePaintChanged?.Invoke(active);

            RefreshColorLock();
        }

        public bool ColorLocked => FreePaintActive || _fillLock;

        /// Booster tô hết màu giữ khoá trong lúc đợt tô chạy.
        ///
        /// KHÔNG nằm trên IPaintService — cùng lý do như SetFreePaint và TryPaintAs: đây
        /// là cửa sau cho booster. Mở nó cho UI thì bất cứ chỗ nào cũng khoá được việc
        /// chọn màu của người chơi, và cái khoá đó sẽ có ngày không ai mở.
        ///
        /// Hai nguồn khoá là hai cờ RIÊNG chứ không phải một bộ đếm. Đếm thì mỗi cú khoá
        /// phải có đúng một cú mở khớp với nó, mà lệch một cặp là người chơi không chọn
        /// được màu nữa cho tới hết phiên — kiểu hỏng gần như không lần ra được.
        public void SetFillLock(bool locked)
        {
            if (_fillLock == locked) return;

            _fillLock = locked;

            RefreshColorLock();
        }

        private void RefreshColorLock()
        {
            var locked = ColorLocked;
            if (locked == _lastColorLock) return;

            _lastColorLock = locked;
            OnColorLockChanged?.Invoke(locked);
        }

        public void RequireColor()
        {
            // Đang tô tự do thì không cần màu nào cả — nhắc là nhắc sai.
            if (FreePaintActive) return;

            if (SelectedPaletteIndex >= 0) return;

            // Bảng đã tô kín thì không còn màu nào để mà chọn.
            //
            // Cần chốt này từ khi lời nhắc bắt mọi cú chạm ngoài UI: popup thắng màn
            // KHÔNG làm tối nền, nên chạm ra ngoài popup là chạm thẳng xuống bảng, và
            // người chơi vừa hoàn thành bức tranh lại bị bảo đi chọn màu.
            if (_state == null || _state.IsComplete) return;

            OnColorRequired?.Invoke();
        }

        /// Bootstrap đưa phụ thuộc xuống — không tự đi tìm.
        public void Init(ILevelService levelService, PaintProgressStore progressStore)
        {
            _levelService = levelService;
            _progressStore = progressStore;

            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId)
        {
            _state = null;
            SelectedPaletteIndex = -1;
            _loadedLevel = levelId;

            // Booster không sống qua ranh giới màn. Tắt ở ĐÂY chứ không chỉ ở
            // FreePaintController: màn được nạp lại bằng nhiều đường (nút Tô lại, Home,
            // cheat), và đây là chỗ duy nhất mọi đường đó đều đi qua.
            SetFreePaint(false);

            // Mở khoá luôn. Đợt tô của màn cũ đã bị FillColorController cắt theo
            // OnBoardReady, nhưng cái khoá thì không tự biết chuyện đó — và một cái khoá
            // sót lại là màn mới mở ra với thanh màu bấm không ăn.
            SetFillLock(false);

            var data = _levelService.CurrentGrid;
            var grid = data != null ? data.ToGrid() : null;

            if (grid != null) _state = new PaintState(grid);

            // Nạp lại tiến độ TRƯỚC khi bắn OnBoardReady: thanh màu và bảng đều dựng
            // theo trạng thái đọc được lúc nhận sự kiện đó. Nạp sau thì chúng dựng theo
            // bảng trống rồi mới bị sửa, và người chơi thấy một nhịp nhấp nháy.
            if (_state != null)
            {
                // Restore chạy TRƯỚC và chạy cho MỌI màn, kể cả màn đã xong: nó còn là chỗ
                // kho tiến độ gắn mình vào lưới mới. Bỏ qua nó thì kho vẫn trỏ vào màn cũ,
                // và cú ghi kế tiếp sẽ lưu nhầm bảng.
                var restored = _progressStore != null && _progressStore.Restore(levelId, _state);

                // Màn đã ghi nhận hoàn thành mà KHÔNG có lượt chơi nào đang mở thì hiện lại
                // NGUYÊN bức tranh.
                //
                // Đọc từ tiến trình chứ không từ bản lưu, và đó là điểm mấu chốt: bản lưu
                // của màn đã xong bị xoá đi (nó là dữ liệu thừa — "xong" nghĩa là mọi ô đều
                // đã tô), nên chỉ dựa vào bản lưu thì tranh cũ mở ra trắng trơn.
                //
                // Còn `restored` là thứ chừa đường cho nút Tô lại: nó ghi một bản lưu rỗng,
                // và chính sự TỒN TẠI của bản lưu đó nói rằng người chơi đang tô lại màn
                // này — đừng tô kín hộ nữa.
                if (!restored && _levelService.IsCompleted(levelId)) _state.PaintAll();
            }

            OnBoardReady?.Invoke();
        }

        public void SelectColor(int paletteIndex)
        {
            if (_state == null) return;

            // Booster đang chạy dở thì màu đứng yên. Xem IPaintService.ColorLocked.
            //
            // Chặn ở ĐÂY chứ không ở từng chỗ bấm: cú chạm vào ô màu và cú giữ tay trên
            // tranh là hai đường vào khác nhau, mà sau này còn thêm nữa. Đặt ở mỗi cửa
            // một cái chốt thì cửa nào quên là lọt.
            if (ColorLocked) return;
            if (!_state.IsUsed(paletteIndex)) return;

            // Bắn lời "đưa màu này vào tầm mắt" TRƯỚC phép so sánh bên dưới, nên nó bắn
            // cả khi màu không hề đổi. Xem chú thích ở IPaintService.OnColorFocusRequested.
            //
            // Phép kiểm IsUsed phải đứng trên nó: màu không có trên thanh thì chẳng có ô
            // nào để mà đưa vào tầm mắt.
            OnColorFocusRequested?.Invoke(paletteIndex);

            if (paletteIndex == SelectedPaletteIndex) return;

            SelectedPaletteIndex = paletteIndex;
            OnColorSelected?.Invoke(paletteIndex);
        }

        public bool CanPaint(int x, int y)
        {
            if (_state == null) return false;

            if (FreePaintActive) return _state.CanPaintAny(x, y);

            if (SelectedPaletteIndex < 0) return false;

            return _state.CanPaint(x, y, SelectedPaletteIndex);
        }

        public bool TryPaint(int x, int y)
        {
            if (_state == null) return false;

            if (FreePaintActive)
            {
                // Bắn kèm màu THẬT của ô, không phải màu đang chọn: viên ngọc bay ra từ
                // đúng ô màu của nó, vòng tiến độ đúng màu nhích lên, và hiệu ứng "xong
                // một màu" nổ đúng lúc. Cả ba đều chỉ đọc con số trong sự kiện này.
                if (!_state.TryPaintAny(x, y, out var painted)) return false;

                _progressStore?.MarkDirty();

                OnCellPainted?.Invoke(new Vector2Int(x, y), painted);
                return true;
            }

            if (SelectedPaletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, SelectedPaletteIndex)) return false;

            _progressStore?.MarkDirty();

            OnCellPainted?.Invoke(new Vector2Int(x, y), SelectedPaletteIndex);
            return true;
        }

        /// Tô một ô bằng MỘT MÀU CHỈ ĐỊNH, không hỏi màu đang chọn.
        ///
        /// KHÔNG nằm trên IPaintService — cùng lý do như SetFreePaint: đây là cửa sau cho
        /// booster, và để UI với lớp nhận chạm gọi được thì luật "chỉ tô được màu đang
        /// chọn" mất hết ý nghĩa. FillColorController cầm thẳng PaintManager.
        ///
        /// Cố ý không tự đọc SelectedPaletteIndex ở trong: cú tô hàng loạt kéo dài qua
        /// nhiều frame, mà người chơi thì có thể bấm sang màu khác giữa chừng — chốt màu
        /// một lần lúc bấm nút rồi truyền xuống thì cú tô luôn hoàn tất đúng cái màu người
        /// chơi đã trả lượt cho.
        public bool TryPaintAs(int x, int y, int paletteIndex)
        {
            if (_state == null) return false;
            if (paletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, paletteIndex)) return false;

            _progressStore?.MarkDirty();

            OnCellPainted?.Invoke(new Vector2Int(x, y), paletteIndex);
            return true;
        }

        /// Gom mọi ô chưa tô của một màu vào danh sách cho sẵn. Xem PaintState.
        /// Cũng KHÔNG nằm trên interface: chỉ booster cần tới, và interface thì nên hẹp.
        public int CollectUnpainted(int paletteIndex, List<Vector2Int> buffer)
        {
            if (_state == null)
            {
                buffer?.Clear();
                return 0;
            }

            return _state.CollectUnpainted(paletteIndex, buffer);
        }

        public bool IsPainted(int x, int y)
        {
            return _state != null && _state.IsPainted(x, y);
        }

        public bool IsComplete => _state != null && _state.IsComplete;

        public bool IsUntouched => _state != null && _state.IsUntouched;

        public int RemainingFor(int paletteIndex)
        {
            return _state != null ? _state.RemainingFor(paletteIndex) : 0;
        }

        public bool TryGetUnpaintedCell(int paletteIndex, int ordinal, out Vector2Int cell)
        {
            cell = default;

            return _state != null && _state.TryGetUnpainted(paletteIndex, ordinal, out cell);
        }

        public float ProgressFor(int paletteIndex)
        {
            return _state != null ? _state.ProgressFor(paletteIndex) : 0f;
        }

        public bool CanReset => _state != null && !_state.IsUntouched;

        public void ResetCurrentLevel()
        {
            if (_state == null || _loadedLevel < 0) return;

            _progressStore?.ResetCurrent();

            // Nạp lại chính màn đang chơi. LoadLevel bắn OnLevelStarted, và mọi lớp hiển
            // thị đều dựng lại theo sự kiện đó — kể cả PaintManager này, nên _state mới
            // sinh ra ở ngay dòng dưới của lượt sự kiện.
            _levelService.LoadLevel(_loadedLevel);
        }
    }
}
