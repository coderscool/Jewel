using System;
using JewelPainter.Core.Persistence;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Lưu và nạp lại tiến độ tô của từng màn.
    public class PaintProgressStore : MonoBehaviour
    {
        [Tooltip("Bao lâu ghi xuống đĩa một lần khi có thay đổi, tính bằng giây.")]
        [SerializeField] private float _autoSaveSeconds = 5f;

        private ISaveService _save;
        private ILevelService _levelService;

        private PaintState _state;
        private int _levelId = -1;
        private bool _isDirty;
        private float _sinceLastSave;

        public void Init(ISaveService save, ILevelService levelService)
        {
            _save = save;
            _levelService = levelService;

            _levelService.OnLevelCompleted += HandleLevelCompleted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelCompleted -= HandleLevelCompleted;

            Flush();
        }

        /// Nạp tiến độ tô đã lưu vào PaintState.
        public bool Restore(int levelId, PaintState state)
        {
            Flush();

            _levelId = levelId;
            _state = state;
            _isDirty = false;
            _sinceLastSave = 0f;

            if (_save == null || state == null) return false;

            var encoded = _save.GetString(KeyFor(levelId));
            if (string.IsNullOrEmpty(encoded)) return false;

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"Bản lưu tiến độ tô của màn {levelId} bị hỏng — bỏ qua.");
                _save.DeleteKey(KeyFor(levelId));
                return false;
            }

            if (state.RestorePaintedBits(bytes)) return true;

            Debug.LogWarning($"Bản lưu tiến độ tô của màn {levelId} không khớp cỡ lưới " +
                             "(lưới đã được sinh lại?) — bỏ qua và xoá.");
            _save.DeleteKey(KeyFor(levelId));
            return false;
        }

        /// Đánh dấu có thay đổi cần lưu.
        public void MarkDirty() => _isDirty = true;

        /// Đưa tiến độ tô của màn đang chơi về 0 ô.
        public void ResetCurrent()
        {
            if (_save == null || _state == null || _levelId < 0) return;

            _isDirty = false;
            _sinceLastSave = 0f;

            _save.SetString(KeyFor(_levelId), Convert.ToBase64String(new byte[_state.PaintedBitsLength]));
            _save.Save();
        }

        /// Đọc trạng thái tô của một màn bất kỳ, kể cả màn chưa bao giờ được nạp.
        public byte[] LoadBits(int levelId)
        {
            if (levelId == _levelId && _state != null) return _state.ToPaintedBits();
            if (_save == null) return null;

            var encoded = _save.GetString(KeyFor(levelId));
            if (string.IsNullOrEmpty(encoded)) return null;

            try
            {
                return Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// Ghi ngay lập tức nếu đang có thay đổi chưa lưu.
        public void Flush()
        {
            if (!_isDirty || _save == null || _state == null || _levelId < 0) return;

            _isDirty = false;
            _sinceLastSave = 0f;

            _save.SetString(KeyFor(_levelId), Convert.ToBase64String(_state.ToPaintedBits()));
            _save.Save();
        }

        private void Update()
        {
            if (!_isDirty || _autoSaveSeconds <= 0f) return;

            _sinceLastSave += Time.unscaledDeltaTime;
            if (_sinceLastSave < _autoSaveSeconds) return;

            Flush();
        }

        /// Lưu tiến độ khi app xuống nền.
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) Flush();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void HandleLevelCompleted(int levelId)
        {
            _isDirty = false;

            if (_save == null) return;

            _save.DeleteKey(KeyFor(levelId));
            _save.Save();
        }

        private static string KeyFor(int levelId) => PreferenceKeys.PaintedPrefix + levelId;
    }
}
