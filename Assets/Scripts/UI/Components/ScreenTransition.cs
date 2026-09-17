using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Màn che chuyển cảnh: quét vào che kín màn hình, đổi màn hình ở phía sau, rồi quét ra.
    [RequireComponent(typeof(Image))]
    public class ScreenTransition : MonoBehaviour
    {
        [Tooltip("Image phủ trọn màn hình dùng shader chuyển cảnh.")]
        [SerializeField] private Image _overlay;

        [Tooltip("Thời gian quét vào (che kín).")]
        [SerializeField] private float _coverDuration = 0.35f;

        [Tooltip("Thời gian quét ra (lộ màn hình mới).")]
        [SerializeField] private float _revealDuration = 0.4f;

        [Tooltip("Giữ màn hình che kín thêm ngần này giây trước khi quét ra.")]
        [SerializeField] private float _holdSeconds = 0.08f;

        [Tooltip("Đường cong của cú quét vào.")]
        [SerializeField] private Ease _coverEase = Ease.InQuad;

        [Tooltip("Đường cong của cú quét ra.")]
        [SerializeField] private Ease _revealEase = Ease.OutQuad;

        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        private Material _material;

        private Coroutine _routine;

        public bool IsPlaying => _routine != null;

        private void Awake()
        {
            if (_overlay == null) _overlay = GetComponent<Image>();

            if (_overlay == null) return;

            _material = new Material(_overlay.material);
            _overlay.material = _material;

            SetCutoff(0f);

            _overlay.raycastTarget = false;
            _overlay.enabled = false;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        /// Quét vào cho tới khi che kín, chạy `onCovered`, giữ một nhịp rồi quét ra.
        public void Play(Action onCovered)
        {
            if (_overlay == null || _material == null)
            {
                onCovered?.Invoke();
                return;
            }

            if (_routine != null) StopCoroutine(_routine);

            if (!_overlay.gameObject.activeSelf) _overlay.gameObject.SetActive(true);

            _routine = StartCoroutine(PlayRoutine(onCovered));
        }

        private IEnumerator PlayRoutine(Action onCovered)
        {
            UpdateAspect();

            _overlay.enabled = true;

            _overlay.raycastTarget = true;

            yield return Sweep(0f, 1f, _coverDuration, _coverEase);

            onCovered?.Invoke();

            yield return null;

            if (_holdSeconds > 0f) yield return new WaitForSecondsRealtime(_holdSeconds);

            yield return Sweep(1f, 0f, _revealDuration, _revealEase);

            _overlay.raycastTarget = false;
            _overlay.enabled = false;

            _routine = null;
        }

        /// Chạy một cú quét của màn che.
        private IEnumerator Sweep(float from, float to, float duration, Ease ease)
        {
            if (duration <= 0f)
            {
                SetCutoff(to);
                yield break;
            }

            var tween = DOVirtual.Float(from, to, duration, SetCutoff)
                .SetEase(ease)
                .SetUpdate(true);

            yield return tween.WaitForCompletion();
        }

        /// Tỉ lệ rộng/cao của tấm che, để shader giữ mask tròn cho ra hình tròn.
        private void UpdateAspect()
        {
            if (_material == null || _overlay == null) return;

            var rect = _overlay.rectTransform.rect;
            if (rect.height <= 0f) return;

            _material.SetFloat(AspectId, rect.width / rect.height);
        }

        private void SetCutoff(float value)
        {
            if (_material == null) return;

            _material.SetFloat(CutoffId, value);
        }
    }
}
