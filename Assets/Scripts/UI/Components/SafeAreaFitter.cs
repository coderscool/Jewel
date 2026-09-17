using UnityEngine;

namespace JewelPainter.UI.Components
{
    /// Co khung object về vùng an toàn của màn hình.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Lùi khỏi mép trên.")]
        [SerializeField] private bool _applyTop = true;

        [Tooltip("Lùi khỏi mép dưới.")]
        [SerializeField] private bool _applyBottom = true;

        [Tooltip("Lùi khỏi mép trái.")]
        [SerializeField] private bool _applyLeft;

        [Tooltip("Lùi khỏi mép phải.")]
        [SerializeField] private bool _applyRight;

        private RectTransform _rect;

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
            _applied = false;
            Apply();
        }

        private void Update() => Apply();

        /// Áp lại vùng an toàn.
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

            if (max.x - min.x <= 0f || max.y - min.y <= 0f) return;

            _rect.anchorMin = min;
            _rect.anchorMax = max;

            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _lastOrientation = orientation;
            _applied = true;
        }
    }
}
