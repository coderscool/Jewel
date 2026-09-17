using System;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Màn hình chờ che lúc dựng bàn chơi.
    public class LoadingScreenView : MonoBehaviour
    {
        [Tooltip("Object bị ẩn khi xong.")]
        [SerializeField] private GameObject _content;

        [Tooltip("Thời gian giữ màn chờ tính từ lúc bàn dựng xong.")]
        [SerializeField] private float _minimumSeconds = 0.8f;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Thanh tiến trình.")]
        [SerializeField] private Image _progressFill;

        private const float BuildProgressShare = 0.25f;

        private ILevelService _levelService;

        private bool _isShowing;
        private bool _isBoardBuilt;
        private float _builtAt;

        public bool IsShowing
        {
            get
            {
                var target = _content != null ? _content : gameObject;

                return target.activeSelf;
            }
        }

        public event Action<bool> OnVisibilityChanged;

        /// Nối phụ thuộc.
        public void Bind(ILevelService levelService)
        {
            if (_levelService != null) Unbind();

            _levelService = levelService;

            _levelService.OnLevelLoadStarted += HandleLoadStarted;
            _levelService.OnLevelStarted += HandleBoardBuilt;

            SetVisible(false);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_levelService == null) return;

            _levelService.OnLevelLoadStarted -= HandleLoadStarted;
            _levelService.OnLevelStarted -= HandleBoardBuilt;

            _levelService = null;
        }

        private void HandleLoadStarted(int levelId)
        {
            _isShowing = true;
            _isBoardBuilt = false;

            SetVisible(true);
            SetProgress(0f);
        }

        /// Bắt đầu đếm giờ giữ màn chờ khi bàn đã dựng xong.
        private void HandleBoardBuilt(int levelId)
        {
            _isBoardBuilt = true;
            _builtAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (!_isShowing) return;

            if (!_isBoardBuilt)
            {
                SetProgress(BuildProgressShare * 0.5f);
                return;
            }

            var minimum = Mathf.Max(0f, _minimumSeconds);
            var elapsed = Time.unscaledTime - _builtAt;
            var t = minimum > 0f ? Mathf.Clamp01(elapsed / minimum) : 1f;

            SetProgress(BuildProgressShare + (1f - BuildProgressShare) * t);

            if (elapsed < minimum) return;

            _isShowing = false;

            SetProgress(1f);
            SetVisible(false);
        }

        private void SetProgress(float value)
        {
            if (_progressFill == null) return;

            _progressFill.fillAmount = Mathf.Clamp01(value);
        }

        private void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf == visible) return;

            target.SetActive(visible);

            OnVisibilityChanged?.Invoke(visible);
        }
    }
}
