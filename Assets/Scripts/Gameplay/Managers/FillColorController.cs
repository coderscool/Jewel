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
    public class FillColorController : MonoBehaviour, IFillColorService
    {
        [Tooltip("Thời lượng mong muốn của cả đợt tô, tính bằng giây.")]
        [SerializeField] private float _fillSeconds = 2.5f;

        [Tooltip("Sàn tốc độ, tính bằng ô mỗi giây.")]
        [SerializeField] private float _minCellsPerSecond = 40f;

        [Tooltip("Trần tốc độ, tính bằng ô mỗi giây.")]
        [SerializeField] private float _maxCellsPerSecond = 700f;

        [Tooltip("Mọi ô trong đợt tô đều có viên ngọc bay ra từ thanh màu.")]
        [SerializeField] private bool _flyEveryCell = true;

        private IPaintService _paintService;
        private PaintManager _paintManager;
        private FillColorCredits _credits;
        private JewelFlyEffect _flyEffect;

        private readonly List<Vector2Int> _cells = new();

        private Coroutine _fill;

        private int _fillPalette = -1;

        private int _filled;

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

                if (IsFilling) return false;

                return !_paintService.IsComplete;
            }
        }

        /// Khởi tạo phụ thuộc.
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

            var paletteIndex = _paintService.SelectedPaletteIndex;
            if (paletteIndex < 0)
            {
                _paintService.RequireColor();
                return false;
            }

            if (!CanUse) return false;

            if (_paintManager.CollectUnpainted(paletteIndex, _cells) <= 0) return false;

            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            _fillPalette = paletteIndex;
            _filled = 0;

            if (BurstActive) _flyEffect.SetBurstMode(true);

            _paintManager.SetFillLock(true);

            _fill = StartCoroutine(FillRoutine());

            OnFillingChanged?.Invoke(true);
            RefreshAvailability();

            return true;
        }

        /// Dừng đợt tô đang chạy.
        public void StopFilling()
        {
            if (_fill == null) return;

            StopCoroutine(_fill);
            EndFill();
        }

        private bool BurstActive => _flyEveryCell && _flyEffect != null;

        private IEnumerator FillRoutine()
        {
            var rate = ResolveRate(_cells.Count);

            var budget = 0f;

            while (_filled < _cells.Count)
            {
                budget += rate * Time.deltaTime;

                while (budget >= 1f && _filled < _cells.Count)
                {
                    var cell = _cells[_filled++];

                    if (_paintManager.TryPaintAs(cell.x, cell.y, _fillPalette)) budget -= 1f;
                }

                yield return null;
            }

            EndFill();
        }

        /// Số ô tô mỗi giây của đợt tô.
        private float ResolveRate(int count)
        {
            var seconds = Mathf.Max(0.05f, _fillSeconds);
            var min = Mathf.Max(1f, _minCellsPerSecond);
            var max = Mathf.Max(min, _maxCellsPerSecond);

            return Mathf.Clamp(count / seconds, min, max);
        }

        /// Dừng đợt tô khi màn đổi.
        private void HandleBoardReady()
        {
            StopFilling();
            RefreshAvailability();
        }

        private void EndFill()
        {
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
