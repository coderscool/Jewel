using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Phóng to ô đang ở vị trí tiêu điểm của danh sách cuộn.
    public class ScrollFocusScaler : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Vị trí tiêu điểm trong khung nhìn: 0 = mép trên, 0.5 = giữa, 1 = mép dưới.")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusAlignment = 1f;

        [Tooltip("Cỡ của ô đang ở đúng tiêu điểm.")]
        [SerializeField] private float _focusScale = 1.25f;

        [Tooltip("Cách tiêu điểm xa hơn ngần này pixel thì về cỡ 1.")]
        [SerializeField] private float _falloffPixels = 500f;

        [Tooltip("Đường cong từ tiêu điểm (0) ra tới hết tầm ảnh hưởng (1).")]
        [SerializeField] private AnimationCurve _falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<RectTransform> _targets = new();

        /// Đặt danh sách ô cần phóng to.
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

        /// Cập nhật tỉ lệ các ô theo vị trí cuộn.
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

                var centerFromTop = viewportHeight * 0.5f - viewport.InverseTransformPoint(item.position).y;

                var focusFromTop = viewportHeight * alignment - item.rect.height * (alignment - 0.5f);

                var distance = Mathf.Abs(centerFromTop - focusFromTop);
                var t = _falloffCurve.Evaluate(Mathf.Clamp01(distance / falloff));

                item.localScale = Vector3.one * Mathf.LerpUnclamped(_focusScale, 1f, t);
            }
        }
    }
}
