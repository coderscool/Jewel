using DG.Tweening;
using JewelPainter.Core.Services;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Base cho mọi popup, mờ dần khi mở và đóng.
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Tiếng phát khi popup đóng.")]
        [SerializeField] private SoundKey _closeSound = SoundKey.Cancel;

        [Tooltip("Thời gian mờ dần vào, tính bằng giây.")]
        [SerializeField] private float _fadeInDuration = 0.15f;

        [Tooltip("Thời gian mờ dần ra, tính bằng giây.")]
        [SerializeField] private float _fadeOutDuration = 0.15f;

        private Tween _fade;

        private bool _isShown;

        public bool IsVisible => _isShown;

        [Tooltip("Chặn chạm xuống màn chơi phía sau khi popup này đang mở.")]
        [SerializeField] private bool _blocksBackground = true;

        public virtual bool BlocksBackground => _blocksBackground;

        protected virtual SoundKey CloseSound => _closeSound;

        protected CanvasGroup CanvasGroup => _canvasGroup;

        protected ISoundService Sound { get; private set; }

        /// Gán sound service cho popup.
        public void SetSoundService(ISoundService sound) => Sound = sound;

        public virtual void Show()
        {
            KillFade();

            _isShown = true;

            gameObject.SetActive(true);

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            var duration = Mathf.Max(0f, _fadeInDuration);

            if (duration <= 0f)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            _canvasGroup.alpha = 0f;
            _fade = FadeTo(1f, duration);
        }

        public virtual void Hide() => Close(playCloseSound: true);

        /// Đóng mà không kêu tiếng đóng.
        public void HideSilently() => Close(playCloseSound: false);

        private void Close(bool playCloseSound)
        {
            KillFade();

            var closeSound = CloseSound;

            if (playCloseSound && _isShown && closeSound != SoundKey.None && Sound != null)
            {
                Sound.Play(closeSound);
            }

            if (!_isShown)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            _isShown = false;

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var duration = Mathf.Max(0f, _fadeOutDuration);

            if (duration <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            _fade = FadeTo(0f, duration).OnComplete(() =>
            {
                _fade = null;

                if (_canvasGroup == null) return;

                gameObject.SetActive(false);
            });
        }

        /// Tween alpha của CanvasGroup.
        private Tween FadeTo(float target, float duration)
        {
            var from = _canvasGroup.alpha;

            return DOVirtual.Float(from, target, duration, value =>
                {
                    if (_canvasGroup != null) _canvasGroup.alpha = value;
                })
                .SetUpdate(true);
        }

        /// Huỷ tween mờ đang chạy.
        private void KillFade()
        {
            if (_fade == null) return;

            if (_fade.IsActive()) _fade.Kill();

            _fade = null;
        }
    }
}
