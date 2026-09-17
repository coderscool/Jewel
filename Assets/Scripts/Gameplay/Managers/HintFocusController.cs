using System;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Nút gợi ý: bốc một ô chưa tô của màu đang chọn rồi đưa camera tới đó.
    public class HintFocusController : MonoBehaviour, IHintService
    {
        private IPaintService _paintService;
        private BoardCamera _boardCamera;
        private HintMarkerEffect _markerEffect;
        private HintCredits _credits;

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

            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += RefreshAvailability;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;
            _paintService.OnColorLockChanged += HandleColorLockChanged;

            _lastAvailability = CanUseHint;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

            if (_paintService == null) return;

            _paintService.OnBoardReady -= RefreshAvailability;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnColorLockChanged -= HandleColorLockChanged;
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

            var paletteIndex = ResolveHintColor();
            if (paletteIndex < 0) return false;

            if (paletteIndex != _paintService.SelectedPaletteIndex && _paintService.ColorLocked)
            {
                return false;
            }

            if (_boardCamera == null)
            {
                Debug.LogWarning($"{nameof(HintFocusController)} chưa có BoardCamera — " +
                                 "nút gợi ý không đưa camera đi đâu được.");
                return false;
            }

            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            if (paletteIndex != _paintService.SelectedPaletteIndex) _paintService.SelectColor(paletteIndex);

            var remaining = _paintService.RemainingFor(paletteIndex);

            var ordinal = UnityEngine.Random.Range(0, remaining);

            return FocusOnCellOf(paletteIndex, ordinal, playMarker: true, out _);
        }

        public bool FocusHintWithoutSpending(out Vector2Int cell)
        {
            cell = default;

            if (_paintService == null || _boardCamera == null) return false;

            var paletteIndex = _paintService.SelectedPaletteIndex;

            if (paletteIndex < 0) return false;

            if (_paintService.RemainingFor(paletteIndex) <= 0) return false;

            return FocusOnCellOf(paletteIndex, ordinal: 0, playMarker: false, out cell);
        }

        /// Đưa camera tới ô chưa tô thứ `ordinal` của màu này, và thả dấu gợi ý nếu được yêu cầu.
        private bool FocusOnCellOf(int paletteIndex, int ordinal, bool playMarker, out Vector2Int cell)
        {
            cell = default;

            if (_paintService.RemainingFor(paletteIndex) <= 0) return false;

            if (!_paintService.TryGetUnpaintedCell(paletteIndex, ordinal, out cell)) return false;

            _boardCamera.FocusOn(cell);

            if (playMarker && _markerEffect != null) _markerEffect.Play(cell);

            return true;
        }

        /// Màu để gợi ý: màu đang chọn nếu nó còn ô, không thì màu đầu tiên còn ô.
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

        private void HandleColorLockChanged(bool locked) => RefreshAvailability();

        private void RefreshAvailability()
        {
            var available = CanUseHint;
            if (available == _lastAvailability) return;

            _lastAvailability = available;
            OnHintAvailabilityChanged?.Invoke(available);
        }
    }
}
