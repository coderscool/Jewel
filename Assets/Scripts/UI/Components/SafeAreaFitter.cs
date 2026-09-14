using UnityEngine;

namespace JewelPainter.UI.Components
{
    /// Co khung của object này về đúng **vùng an toàn** mà hệ điều hành báo về
    /// (`Screen.safeArea`) — phần màn hình không bị tai thỏ, camera đục lỗ, thanh
    /// trạng thái hay thanh gesture che.
    ///
    /// Gắn lên một object **con trực tiếp của Canvas**, kéo full khung, rồi để mọi UI
    /// nằm dưới nó. Phép quy đổi ở đây chia toạ độ safe area cho cỡ màn hình để ra
    /// anchor 0..1, mà anchor chỉ đúng khi cha của nó phủ trọn màn hình — tức là
    /// canvas gốc. Gắn xuống sâu hơn một tầng là ăn hai lần co, UI tụt vào trong gấp
    /// đôi.
    ///
    /// Không đụng tới canvas nền: nền cần **tràn hết** ra mép máy, co nó lại chỉ tạo
    /// hai dải đen ở tai thỏ.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Lùi khỏi mép TRÊN — nơi có tai thỏ, camera đục lỗ và thanh trạng thái. " +
                 "Gần như luôn cần bật cho canvas có nút ở trên.")]
        [SerializeField] private bool _applyTop = true;

        [Tooltip("Lùi khỏi mép DƯỚI — nơi có thanh gesture của iOS và Android cử chỉ. " +
                 "Cần bật cho canvas có nút hoặc thanh màu sát đáy.")]
        [SerializeField] private bool _applyBottom = true;

        [Tooltip("Lùi khỏi mép TRÁI. Màn dọc gần như không bao giờ bị cắt bên trái, nên " +
                 "mặc định tắt: bật lên chỉ làm layout hụt vô cớ trên máy không có notch. " +
                 "Chỉ bật nếu game có hỗ trợ xoay ngang.")]
        [SerializeField] private bool _applyLeft;

        [Tooltip("Lùi khỏi mép PHẢI. Cùng lý do với mép trái.")]
        [SerializeField] private bool _applyRight;

        private RectTransform _rect;

        // Cache để Update thoát sớm. Safe area chỉ đổi khi xoay máy, đổi độ phân giải
        // hoặc vào/ra chế độ chia đôi màn hình — tính lại mỗi frame là phí, mà bỏ hẳn
        // Update rồi chỉ tính ở Awake thì xoay máy xong UI nằm sai chỗ.
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;
        private bool _applied;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            var canvas = GetComponentInParent<Canvas>();

            if (canvas != null && transform.parent != canvas.transform)
            {
                Debug.LogWarning(
                    $"[SafeAreaFitter] '{name}' không phải con trực tiếp của canvas " +
                    $"'{canvas.name}'. Anchor tính theo cỡ màn hình nên chỉ đúng khi cha " +
                    "phủ trọn màn hình — đặt sâu hơn sẽ co hai lần.", this);
            }
        }

        private void OnEnable()
        {
            // Bật lại object sau khi màn hình đã đổi: cache còn giữ giá trị cũ nên
            // Apply sẽ nghĩ là không có gì đổi. Ép tính lại một lần.
            _applied = false;
            Apply();
        }

        private void Update() => Apply();

        /// Gọi khi tự tay đổi các ô tick lúc đang chạy.
        public void Refresh()
        {
            _applied = false;
            Apply();
        }

        private void Apply()
        {
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var orientation = Screen.orientation;

            if (_applied
                && safeArea == _lastSafeArea
                && screenSize == _lastScreenSize
                && orientation == _lastOrientation)
            {
                return;
            }

            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            if (_rect == null) _rect = (RectTransform)transform;

            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;

            min.x /= screenSize.x;
            min.y /= screenSize.y;
            max.x /= screenSize.x;
            max.y /= screenSize.y;

            if (!_applyLeft) min.x = 0f;
            if (!_applyBottom) min.y = 0f;
            if (!_applyRight) max.x = 1f;
            if (!_applyTop) max.y = 1f;

            // Vài máy (và Device Simulator lúc đang đổi thiết bị) trả về safe area rỗng
            // trong một frame. Gán vào sẽ làm anchorMin vượt anchorMax và toàn bộ UI
            // biến mất — thà giữ nguyên khung cũ thêm một frame.
            if (max.x - min.x <= 0f || max.y - min.y <= 0f) return;

            _rect.anchorMin = min;
            _rect.anchorMax = max;

            // offsetMin/offsetMax thay cho anchoredPosition + sizeDelta: cùng kết quả
            // nhưng không phụ thuộc pivot, nên object bị đổi pivot vẫn khít.
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _lastOrientation = orientation;
            _applied = true;
        }
    }
}
