using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Làm mờ lớp màu theo mức zoom, để lộ số nằm dưới.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardColorFade : MonoBehaviour
    {
        private const float AlphaVisibilityThreshold = 0.002f;

        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private BoardView _boardView;

        [Tooltip("orthographicSize mà tại đó lớp màu đục hoàn toàn.")]
        [SerializeField] private float _opaqueSize;

        [Tooltip("orthographicSize mà tại đó lớp màu trong suốt hoàn toàn.")]
        [SerializeField] private float _transparentSize = 12f;

        [Tooltip("Mốc sẽ bị ô Fade Switch Size trong LevelConfig ghi đè.")]
        [SerializeField] private bool _levelSizeIsOpaque;

        private float _baseSize = -1f;

        private bool _needsBaseCapture = true;
        private float _lastOrthographicSize = -1f;

        private void OnEnable()
        {
            _needsBaseCapture = true;
            _lastOrthographicSize = -1f;

            if (_boardView != null) _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDisable()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;

            if (_renderer != null) _renderer.enabled = true;
        }

        private void HandleBoardRebuilt() => _needsBaseCapture = true;

        private void LateUpdate()
        {
            if (_camera == null || _renderer == null) return;

            if (_needsBaseCapture)
            {
                _baseSize = _camera.orthographicSize;
                _needsBaseCapture = false;
                _lastOrthographicSize = -1f;
            }

            if (Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return;

            _lastOrthographicSize = _camera.orthographicSize;

            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            var color = _renderer.color;
            color.a = AlphaFor(_camera.orthographicSize);
            _renderer.color = color;

            var visible = color.a > AlphaVisibilityThreshold;
            if (_renderer.enabled != visible) _renderer.enabled = visible;
        }

        private float AlphaFor(float currentSize)
        {
            var opaque = _opaqueSize;
            var transparent = _transparentSize;

            var levelSize = LevelFadeSwitchSize();
            if (levelSize > 0f)
            {
                if (_levelSizeIsOpaque) opaque = levelSize;
                else transparent = levelSize;
            }

            if (opaque <= 0f) opaque = _baseSize;
            if (transparent <= 0f) transparent = _baseSize;

            if (opaque <= 0f || transparent <= 0f) return 1f;

            if (Mathf.Approximately(opaque, transparent)) return 1f;

            return 1f - Mathf.InverseLerp(opaque, transparent, currentSize);
        }

        /// Mức zoom chuyển mờ màu của màn đang chơi.
        private float LevelFadeSwitchSize()
        {
            if (_boardView == null) return 0f;

            var config = _boardView.Config;

            return config != null ? config.FadeSwitchSize : 0f;
        }
    }
}
