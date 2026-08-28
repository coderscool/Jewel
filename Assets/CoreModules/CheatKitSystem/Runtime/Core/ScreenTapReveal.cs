using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Mở panel bằng N lần chạm LIÊN TIẾP trong một VÙNG màn hình (mặc định TOP-CENTER) trong
    /// khoảng thời gian cho phép. Cross-platform: touch (mobile) hoặc chuột (Editor/WebGL); 0-alloc;
    /// dùng <see cref="Time.unscaledTime"/> nên hoạt động cả khi pause/timeScale=0.
    ///
    /// Vùng khai báo theo toạ độ màn hình CHUẨN HOÁ [0..1] (x: trái→phải, y: dưới→trên).
    /// Tap ngoài vùng → reset chuỗi (tránh kích hoạt ngẫu nhiên khi chơi).
    /// </summary>
    public sealed class ScreenTapReveal : IPanelRevealGesture
    {
        private readonly int _required;
        private readonly float _window;
        private readonly Rect _region;

        private int _count;
        private float _lastTapTime;

        public ScreenTapReveal(int requiredTaps = 5, float windowSeconds = 1.5f, Rect? normalizedRegion = null)
        {
            _required = Mathf.Max(2, requiredTaps);
            _window = Mathf.Max(0.3f, windowSeconds);
            _region = normalizedRegion ?? new Rect(0.30f, 0.82f, 0.40f, 0.18f); // dải top-center
        }

        public void Reset() => _count = 0;

        public bool Poll()
        {
            if (!TryGetTapBegan(out Vector2 pos)) return false;

            float now = Time.unscaledTime;
            if (_count > 0 && now - _lastTapTime > _window) _count = 0; // quá hạn → bắt đầu lại

            Vector2 norm = new Vector2(
                Screen.width > 0 ? pos.x / Screen.width : 0f,
                Screen.height > 0 ? pos.y / Screen.height : 0f);

            if (!_region.Contains(norm)) { _count = 0; return false; }

            _count++;
            _lastTapTime = now;
            if (_count >= _required) { _count = 0; return true; }
            return false;
        }

        // Một "tap bắt đầu" trong frame này (touch ưu tiên, fallback chuột cho Editor/WebGL).
        private static bool TryGetTapBegan(out Vector2 pos)
        {
            if (Input.touchSupported && Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { pos = t.position; return true; }
                pos = default;
                return false;
            }

            if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
            pos = default;
            return false;
        }
    }
}
