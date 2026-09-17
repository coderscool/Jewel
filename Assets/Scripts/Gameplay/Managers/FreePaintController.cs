using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Booster "tô tự do": bật luật tô tự do trong ít giây rồi tự tắt.
    public class FreePaintController : MonoBehaviour, IFreePaintService
    {
        [Tooltip("Một lượt dùng kéo dài bao nhiêu giây.")]
        [SerializeField] private float _durationSeconds = 20f;

        [Tooltip("Đếm thời gian theo Time.timeScale.")]
        [SerializeField] private bool _useScaledTime = true;

        private IPaintService _paintService;
        private PaintManager _paintManager;
        private FreePaintCredits _credits;

        private float _remainingSeconds;

        private bool _paused;

        private bool _lastAvailability;

        public event Action<bool> OnActiveChanged;
        public event Action<int> OnCreditsChanged;
        public event Action OnCreditsExhausted;
        public event Action<bool> OnAvailabilityChanged;

        public bool IsActive => _paintService != null && _paintService.FreePaintActive;

        public float RemainingSeconds => IsActive ? _remainingSeconds : 0f;

        public bool IsPaused => _paused;

        public void SetPaused(bool paused) => _paused = paused;

        public float DurationSeconds => Mathf.Max(0.01f, _durationSeconds);

        public int RemainingCredits => _credits?.Remaining ?? 0;

        public bool CanUse
        {
            get
            {
                if (_paintService == null) return false;

                return !_paintService.IsComplete;
            }
        }

        public void Init(PaintManager paintManager, FreePaintCredits credits)
        {
            _paintManager = paintManager;
            _paintService = paintManager;
            _credits = credits;

            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += RefreshAvailability;
            _paintService.OnCellPainted += HandleCellPainted;

            _paintService.OnFreePaintChanged += HandleFreePaintChanged;
            _paintService.OnColorLockChanged += HandleColorLockChanged;

            _lastAvailability = CanUse;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

            if (_paintService == null) return;

            _paintService.OnBoardReady -= RefreshAvailability;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnFreePaintChanged -= HandleFreePaintChanged;
            _paintService.OnColorLockChanged -= HandleColorLockChanged;
        }

        public bool Use()
        {
            if (!CanUse) return false;

            if (IsActive) return false;

            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            _remainingSeconds = DurationSeconds;

            _paused = false;

            _paintManager.SetFreePaint(true);

            return true;
        }

        /// Tắt booster sớm.
        public void Cancel()
        {
            _paintManager.SetFreePaint(false);
        }

        private void Update()
        {
            if (!IsActive) return;

            if (_paused) return;

            _remainingSeconds -= _useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;

            if (_remainingSeconds > 0f) return;

            Cancel();
        }

        private void HandleFreePaintChanged(bool active)
        {
            if (!active)
            {
                _remainingSeconds = 0f;
                _paused = false;
            }

            OnActiveChanged?.Invoke(active);
            RefreshAvailability();
        }

        private void HandleColorLockChanged(bool locked) => RefreshAvailability();

        private void HandleCreditsChanged(int remaining) => OnCreditsChanged?.Invoke(remaining);

        /// Tắt booster khi ô cuối cùng của màn được tô.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            RefreshAvailability();

            if (IsActive && _paintService.IsComplete) Cancel();
        }

        private void RefreshAvailability()
        {
            var available = CanUse;
            if (available == _lastAvailability) return;

            _lastAvailability = available;
            OnAvailabilityChanged?.Invoke(available);
        }
    }
}
