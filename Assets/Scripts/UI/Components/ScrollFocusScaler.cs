using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Phóng to ô nào đang đứng ở vị trí tiêu điểm của một danh sách cuộn dọc, và nhỏ
    /// dần theo khoảng cách tới đó.
    ///
    /// Cỡ là HÀM CỦA VỊ TRÍ CUỘN, không phải một tween chạy theo thời gian. Nhờ vậy nó
    /// tự mượt khi kéo tay, tự đúng khi thả cho quán tính trôi, và không bao giờ kẹt ở
    /// một cỡ dở dang vì bị ngắt giữa chừng.
    public class ScrollFocusScaler : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Vị trí tiêu điểm trong khung nhìn: 0 = mép trên, 0.5 = giữa, " +
                 "1 = mép dưới. Nên để TRÙNG với Focus Alignment của HomeScreenView, " +
                 "không thì lúc mở Home ô được cuộn tới lại không phải ô được phóng to.")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusAlignment = 1f;

        [Tooltip("Cỡ của ô đang ở đúng tiêu điểm.")]
        [SerializeField] private float _focusScale = 1.25f;

        [Tooltip("Cách tiêu điểm xa hơn ngần này pixel thì về cỡ 1. Nên đặt cỡ khoảng " +
                 "một ô cộng spacing — nhỏ quá thì ô đổi cỡ giật cục, lớn quá thì không " +
                 "ô nào ra dáng được chọn.")]
        [SerializeField] private float _falloffPixels = 500f;

        [Tooltip("Đường cong từ tiêu điểm (0) ra tới hết tầm ảnh hưởng (1). Mặc định " +
                 "EaseInOut cho cỡ đổi mềm ở hai đầu.")]
        [SerializeField] private AnimationCurve _falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<RectTransform> _targets = new();

        /// HomeScreenView gọi mỗi lần dựng lại danh sách.
        public void SetTargets(IReadOnlyList<RectTransform> targets)
        {
            _targets.Clear();

            if (targets == null) return;

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) _targets.Add(targets[i]);
            }

            Apply();
        }

        /// LateUpdate chứ không Update: ScrollRect cập nhật vị trí content trong LateUpdate
        /// của chính nó. Đọc ở Update là đọc vị trí của frame trước, và cỡ ô trễ đúng một
        /// frame so với thứ người chơi đang kéo.
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_scrollRect == null || _targets.Count == 0) return;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var viewportHeight = viewport.rect.height;
            if (viewportHeight <= 0f) return;

            var falloff = Mathf.Max(1f, _falloffPixels);
            var alignment = Mathf.Clamp01(_focusAlignment);

            foreach (var item in _targets)
            {
                if (item == null || !item.gameObject.activeInHierarchy) continue;

                // Khoảng cách từ mép trên khung nhìn xuống tâm ô. Đi qua toạ độ local của
                // viewport nên không phụ thuộc padding, spacing hay kiểu neo.
                var centerFromTop = viewportHeight * 0.5f - viewport.InverseTransformPoint(item.position).y;

                // Chỗ mà tâm ô sẽ đứng khi nó ở đúng tiêu điểm. Cùng công thức
                // HomeScreenView dùng để cuộn, nên hai bên luôn chỉ vào một chỗ.
                var focusFromTop = viewportHeight * alignment - item.rect.height * (alignment - 0.5f);

                var distance = Mathf.Abs(centerFromTop - focusFromTop);
                var t = _falloffCurve.Evaluate(Mathf.Clamp01(distance / falloff));

                item.localScale = Vector3.one * Mathf.LerpUnclamped(_focusScale, 1f, t);
            }
        }
    }
}
