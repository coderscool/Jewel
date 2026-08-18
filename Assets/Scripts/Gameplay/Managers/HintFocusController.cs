using System;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Nút gợi ý: bốc một ô chưa tô của màu đang chọn rồi đưa camera tới đó.
    ///
    /// Đứng ở Gameplay chứ không nằm trong HudView, vì "ô nào còn chưa tô" là trạng
    /// thái luật chơi. HudView chỉ bấm nút và bật/tắt nút theo IHintService.
    public class HintFocusController : MonoBehaviour, IHintService
    {
        private IPaintService _paintService;
        private BoardCamera _boardCamera;
        private HintMarkerEffect _markerEffect;

        /// Giá trị đã báo ra lần gần nhất. Giữ lại để chỉ bắn sự kiện khi thật sự đổi:
        /// OnCellPainted nổ liên tục suốt lúc kéo tay tô, mà nút thì chỉ đổi trạng thái
        /// đúng hai lần trong cả một màu.
        private bool _lastAvailability;

        public event Action<bool> OnHintAvailabilityChanged;

        public bool CanUseHint
        {
            get
            {
                if (_paintService == null) return false;

                var selected = _paintService.SelectedPaletteIndex;

                // Chưa chọn màu vẫn cho BẤM: bấm vào sẽ hiện lời nhắc chọn màu. Nút xám
                // ngắt không nói được gì, mà đó lại đúng lúc người chơi cần biết nhất.
                return selected < 0 || _paintService.RemainingFor(selected) > 0;
            }
        }

        public void Init(IPaintService paintService, BoardCamera boardCamera, HintMarkerEffect markerEffect)
        {
            _paintService = paintService;
            _boardCamera = boardCamera;
            _markerEffect = markerEffect;

            _paintService.OnBoardReady += RefreshAvailability;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;

            _lastAvailability = CanUseHint;
        }

        private void OnDestroy()
        {
            if (_paintService == null) return;

            _paintService.OnBoardReady -= RefreshAvailability;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnCellPainted -= HandleCellPainted;
        }

        public bool UseHint()
        {
            if (_paintService == null) return false;

            if (_paintService.SelectedPaletteIndex < 0)
            {
                _paintService.RequireColor();
                return false;
            }

            if (!CanUseHint) return false;
            if (_boardCamera == null)
            {
                Debug.LogWarning($"{nameof(HintFocusController)} chưa có BoardCamera — " +
                                 "nút gợi ý không đưa camera đi đâu được.");
                return false;
            }

            var paletteIndex = _paintService.SelectedPaletteIndex;
            var remaining = _paintService.RemainingFor(paletteIndex);

            // RemainingFor chính là số ô chưa tô của màu này, nên bốc số trong khoảng đó
            // là chắc chắn trúng — không phải quét lưới hai lần để đếm trước.
            var ordinal = UnityEngine.Random.Range(0, remaining);

            if (!_paintService.TryGetUnpaintedCell(paletteIndex, ordinal, out var cell)) return false;

            _boardCamera.FocusOn(cell);

            // Hiệu ứng tự chờ camera bay tới nơi rồi mới thả icon — nó có ô Start Delay
            // riêng, không đợi tín hiệu từ camera.
            if (_markerEffect != null) _markerEffect.Play(cell);

            return true;
        }

        private void HandleColorSelected(int paletteIndex) => RefreshAvailability();

        private void HandleCellPainted(Vector2Int cell, int paletteIndex) => RefreshAvailability();

        private void RefreshAvailability()
        {
            var available = CanUseHint;
            if (available == _lastAvailability) return;

            _lastAvailability = available;
            OnHintAvailabilityChanged?.Invoke(available);
        }
    }
}
