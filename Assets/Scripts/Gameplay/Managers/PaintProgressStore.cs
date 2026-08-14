using System;
using JewelPainter.Core.Persistence;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Lưu và nạp lại những ô đã tô của từng màn, để đóng game giữa chừng rồi mở lại
    /// vẫn thấy bức tranh đang dở.
    ///
    /// Trạng thái tô gói theo BIT rồi mã hoá Base64: bảng 64x64 là 4096 ô nhưng chỉ
    /// tốn 512 byte, ra khoảng 700 ký tự — vừa sức PlayerPrefs. Lưu từng ô một thành
    /// số nguyên riêng thì cùng bảng đó là 4096 key.
    ///
    /// KHÔNG ghi đĩa mỗi lần tô một ô. PlayerPrefs.Save ghi cả file ra đĩa; gọi nó theo
    /// nhịp kéo tay tô là cách chắc chắn để game giật. Ở đây chỉ bật cờ bẩn, rồi ghi
    /// theo chu kỳ và ở những mốc mà mất dữ liệu là mất thật: app chuyển nền, mất tiêu
    /// điểm, hoặc thoát hẳn.
    public class PaintProgressStore : MonoBehaviour
    {
        [Tooltip("Bao lâu ghi xuống đĩa một lần khi có thay đổi, tính bằng giây. " +
                 "Để 0 thì chỉ ghi lúc app chuyển nền hoặc thoát.")]
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

            // Màn xong rồi thì bản lưu thành rác: không ai quay lại nữa mà vẫn chiếm
            // chỗ, và nếu sau này mở tính năng chơi lại thì màn cũ sẽ mở ra ở trạng
            // thái đã tô kín.
            _levelService.OnLevelCompleted += HandleLevelCompleted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelCompleted -= HandleLevelCompleted;

            Flush();
        }

        /// PaintManager gọi ngay sau khi dựng PaintState mới, TRƯỚC khi bắn OnBoardReady.
        /// Nhờ vậy thanh màu và bảng đều đọc được con số đã khôi phục ngay từ đầu.
        public void Restore(int levelId, PaintState state)
        {
            Flush();

            _levelId = levelId;
            _state = state;
            _isDirty = false;
            _sinceLastSave = 0f;

            if (_save == null || state == null) return;

            var encoded = _save.GetString(KeyFor(levelId));
            if (string.IsNullOrEmpty(encoded)) return;

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                // Chuỗi hỏng thì bỏ, đừng để một ký tự lỗi làm game không vào được màn.
                Debug.LogWarning($"Bản lưu tiến độ tô của màn {levelId} bị hỏng — bỏ qua.");
                _save.DeleteKey(KeyFor(levelId));
                return;
            }

            if (state.RestorePaintedBits(bytes)) return;

            Debug.LogWarning($"Bản lưu tiến độ tô của màn {levelId} không khớp cỡ lưới " +
                             "(lưới đã được sinh lại?) — bỏ qua và xoá.");
            _save.DeleteKey(KeyFor(levelId));
        }

        /// PaintManager gọi mỗi lần một ô được tô.
        public void MarkDirty() => _isDirty = true;

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

        /// Trên mobile, thoát app thường KHÔNG gọi OnApplicationQuit — hệ điều hành chỉ
        /// đưa app xuống nền rồi có thể giết bất cứ lúc nào. Đây mới là mốc lưu đáng
        /// tin nhất.
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
