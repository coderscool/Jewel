using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Core.Services
{
    /// Lưu cờ bật tắt rung và gọi rung.
    public class VibrationService : IVibrationService
    {
        private readonly ISaveService _save;

        private bool _isEnabled;

        public VibrationService(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));

            _isEnabled = _save.GetBool(PreferenceKeys.VibrationEnabled, true);
        }

        public bool IsEnabled => _isEnabled;

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;

            _save.SetBool(PreferenceKeys.VibrationEnabled, enabled);
            _save.Save();
        }

        /// Rung máy (chưa làm).
        public void Vibrate()
        {
            if (!_isEnabled) return;
        }
    }
}
