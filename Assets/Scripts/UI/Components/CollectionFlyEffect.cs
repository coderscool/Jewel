using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Đưa một bức tranh bay từ ô của nó trong danh sách tới icon bộ sưu tập.
    public class CollectionFlyEffect : MonoBehaviour
    {
        [Tooltip("Image dùng để bay.")]
        [SerializeField] private Image _flyer;

        [Header("Nhịp")]
        [Tooltip("Dừng lại ngắm bức tranh bao lâu trước khi nó bay đi.")]
        [SerializeField] private float _holdSeconds = 0.5f;

        [SerializeField] private float _duration = 1.15f;

        [Tooltip("Nghỉ thêm sau khi bay xong, trước khi trả quyền lại cho bên gọi.")]
        [SerializeField] private float _tailSeconds = 0.15f;

        [Header("Đường bay")]
        [Tooltip("Nhịp đi theo đường bay.")]
        [SerializeField] private Ease _moveEase = Ease.InOutCubic;

        [Tooltip("Cỡ lúc chạm icon, so với cỡ ban đầu.")]
        [SerializeField] private float _endScale = 0.22f;

        [SerializeField] private Ease _scaleEase = Ease.InCubic;

        [Header("Icon nảy khi nhận")]
        [Tooltip("Icon phình thêm bao nhiêu phần lúc tranh chạm vào.")]
        [SerializeField] private float _targetPunch = 0.3f;

        [SerializeField] private float _punchSeconds = 0.3f;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (_flyer != null) _flyer.gameObject.SetActive(false);
        }

        /// Bay từ `from` tới `to`, mang theo `sprite`.
        public void Play(RectTransform from, Sprite sprite, RectTransform to, Action onComplete)
        {
            if (_flyer == null || from == null || to == null || sprite == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(FlyRoutine(from, sprite, to, onComplete));
        }

        public void StopAll()
        {
            StopAllCoroutines();

            IsPlaying = false;

            if (_flyer != null) _flyer.gameObject.SetActive(false);
        }

        private IEnumerator FlyRoutine(RectTransform from, Sprite sprite, RectTransform to, Action onComplete)
        {
            IsPlaying = true;

            var rect = _flyer.rectTransform;

            _flyer.sprite = sprite;
            _flyer.preserveAspect = true;
            _flyer.gameObject.SetActive(true);

            rect.sizeDelta = from.rect.size;
            rect.localScale = Vector3.one;

            var start = from.position;
            rect.position = start;

            yield return WaitUnscaled(_holdSeconds);

            var end = to.position;

            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, _duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                var moved = DOVirtual.EasedValue(0f, 1f, t, _moveEase);
                rect.position = Vector3.LerpUnclamped(start, end, moved);

                var scaled = DOVirtual.EasedValue(0f, 1f, t, _scaleEase);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, _endScale, scaled);

                yield return null;
            }

            rect.position = end;
            _flyer.gameObject.SetActive(false);

            if (_targetPunch > 0f) yield return PunchRoutine(to);

            yield return WaitUnscaled(_tailSeconds);

            IsPlaying = false;
            onComplete?.Invoke();
        }

        /// Icon phình ra rồi co về.
        private IEnumerator PunchRoutine(RectTransform target)
        {
            var original = target.localScale;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, _punchSeconds);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                target.localScale = original * (1f + _targetPunch * Mathf.Sin(Mathf.PI * t));
                yield return null;
            }

            target.localScale = original;
        }

        /// Chờ theo thời gian không phụ thuộc timeScale.
        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f) yield break;

            var elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
