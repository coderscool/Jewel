using System.Collections;
using DG.Tweening;
using JewelPainter.Core.Services;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Lời nhắc ngắn, tự tắt sau vài giây.
    public class NotificationPopupView : PopupView
    {
        [Tooltip("Tự tắt sau ngần này giây.")]
        [SerializeField] private float _autoHideSeconds = 1f;

        [Tooltip("Thời gian mờ dần lúc tắt.")]
        [SerializeField] private float _fadeOutSeconds = 0.35f;

        private bool _isFadingOut;

        public override bool BlocksBackground => false;

        protected override SoundKey CloseSound => SoundKey.None;

        public override void Show()
        {
            _isFadingOut = false;

            base.Show();

            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }

            RestartAutoHide();
        }

        /// Giữ popup mở cho tới khi có lệnh Hide.
        public void KeepOpenUntilHidden()
        {
            StopAllCoroutines();

            _isFadingOut = false;
        }

        /// Mờ dần rồi mới tắt.
        public override void Hide()
        {
            if (_fadeOutSeconds <= 0f || !isActiveAndEnabled || CanvasGroup == null)
            {
                base.Hide();
                return;
            }

            if (_isFadingOut) return;

            StopAllCoroutines();
            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            _isFadingOut = true;

            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            var from = CanvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < _fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(elapsed / _fadeOutSeconds), Ease.InQuad);
                CanvasGroup.alpha = Mathf.Lerp(from, 0f, t);

                yield return null;
            }

            _isFadingOut = false;

            base.Hide();
        }

        private void RestartAutoHide()
        {
            StopAllCoroutines();

            if (_autoHideSeconds <= 0f) return;
            if (!isActiveAndEnabled) return;

            StartCoroutine(AutoHideRoutine());
        }

        private IEnumerator AutoHideRoutine()
        {
            var elapsed = 0f;

            while (elapsed < _autoHideSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Hide();
        }
    }
}
