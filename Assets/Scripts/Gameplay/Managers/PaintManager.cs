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
        public event Action<Vector2Int, int> OnCellPainted;
        public event Action OnColorRequired;

        public void RequireColor()
        {
            if (SelectedPaletteIndex >= 0) return;

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
            if (paletteIndex == SelectedPaletteIndex) return;
            if (!_state.IsUsed(paletteIndex)) return;

            SelectedPaletteIndex = paletteIndex;
            OnColorSelected?.Invoke(paletteIndex);
        }

        public bool CanPaint(int x, int y)
        {
            if (_state == null) return false;
            if (SelectedPaletteIndex < 0) return false;

            return _state.CanPaint(x, y, SelectedPaletteIndex);
        }

        public bool TryPaint(int x, int y)
        {
            if (_state == null) return false;
            if (SelectedPaletteIndex < 0) return false;

            if (!_state.TryPaint(x, y, SelectedPaletteIndex)) return false;

            _progressStore?.MarkDirty();

            OnCellPainted?.Invoke(new Vector2Int(x, y), SelectedPaletteIndex);
            return true;
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
