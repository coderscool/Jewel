using System;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
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
        private HintCredits _credits;

        /// Giá trị đã báo ra lần gần nhất. Giữ lại để chỉ bắn sự kiện khi thật sự đổi:
        /// OnCellPainted nổ liên tục suốt lúc kéo tay tô, mà nút thì chỉ đổi trạng thái
        /// đúng hai lần trong cả một màu.
        private bool _lastAvailability;

        public event Action<bool> OnHintAvailabilityChanged;
        public event Action<int> OnCreditsChanged;
        public event Action OnCreditsExhausted;

        public int RemainingCredits => _credits?.Remaining ?? 0;

        public bool CanUseHint
        {
            get
            {
                if (_paintService == null) return false;

                // Hỏi CẢ BẢNG, không hỏi riêng màu đang chọn.
                //
                // Bản trước hỏi "màu đang chọn còn ô không", nên tô xong MỘT màu là nút
                // xám ngay — trong khi cả chục màu khác còn nguyên và đó đúng là lúc gợi ý
                // hữu ích nhất. Màu đang chọn hết ô là chuyện của UseHint: nó tự chuyển
                // sang màu còn ô.
                //
                // Chưa chọn màu vẫn cho BẤM: bấm vào sẽ hiện lời nhắc chọn màu. Nút xám
                // ngắt không nói được gì, mà đó lại đúng lúc người chơi cần biết nhất.
                return !_paintService.IsComplete;
            }
        }

        public void Init(
            IPaintService paintService,
            BoardCamera boardCamera,
            HintMarkerEffect markerEffect,
            HintCredits credits)
        {
            _paintService = paintService;
            _boardCamera = boardCamera;
            _markerEffect = markerEffect;
            _credits = credits;

            // Chuyển tiếp sự kiện của kho lượt ra ngoài, để HudView chỉ phải biết một
            // interface duy nhất là IHintService.
            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += RefreshAvailability;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;

            _lastAvailability = CanUseHint;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

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

            // Chốt màu và kiểm camera TRƯỚC khi trừ lượt. Mọi đường thoát phải nằm hết ở
            // trên cú trừ, không thì có đường nào đó lấy mất một lượt rồi trả về false —
            // người chơi mất lượt mà không thấy gì xảy ra.
            var paletteIndex = ResolveHintColor();
            if (paletteIndex < 0) return false;

            if (_boardCamera == null)
            {
                Debug.LogWarning($"{nameof(HintFocusController)} chưa có BoardCamera — " +
                                 "nút gợi ý không đưa camera đi đâu được.");
                return false;
            }

            // Trừ lượt rồi mới đi tiếp, và thoát ngay nếu hết.
            //
            // Đặt sau phép kiểm màu ở đầu hàm là có chủ ý: chưa chọn màu thì cú bấm đó
            // không phải một lần dùng gợi ý, nó chỉ là một cú bấm nhầm — trừ lượt ở đó là
            // ăn cắp của người chơi.
            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            // Màu đang chọn đã tô hết thì chuyển hẳn sang màu được gợi ý. Ô màu cũ đã biến
            // khỏi thanh chọn rồi, giữ nguyên nó chỉ để người chơi bay tới nơi và không tô
            // được gì.
            if (paletteIndex != _paintService.SelectedPaletteIndex) _paintService.SelectColor(paletteIndex);

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

        /// Màu để gợi ý: màu đang chọn nếu nó còn ô, không thì màu ĐẦU TIÊN còn ô.
        /// -1 khi bảng đã tô kín — CanUseHint đã chặn từ trước, đây chỉ là chốt cuối.
        private int ResolveHintColor()
        {
            var selected = _paintService.SelectedPaletteIndex;
            if (selected >= 0 && _paintService.RemainingFor(selected) > 0) return selected;

            var used = _paintService.UsedPaletteIndices;
            if (used == null) return -1;

            for (var i = 0; i < used.Count; i++)
            {
                if (_paintService.RemainingFor(used[i]) > 0) return used[i];
            }

            return -1;
        }

        private void HandleCreditsChanged(int remaining) => OnCreditsChanged?.Invoke(remaining);

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
