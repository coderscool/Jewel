using DG.Tweening;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Thả một icon kính lúp xuống ô mà nút gợi ý vừa chỉ tới.
    public class HintMarkerEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _iconPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Chờ ngần này giây rồi mới thả, tính từ lúc bấm nút gợi ý.")]
        [SerializeField] private float _startDelay = 0.45f;

        [Tooltip("Icon bắt đầu cao hơn ô bao nhiêu world unit.")]
        [SerializeField] private float _dropHeight = 6f;

        [Tooltip("Thời gian rơi.")]
        [SerializeField] private float _dropDuration = 0.32f;

        [Tooltip("Nằm lại bao lâu sau khi chạm ô.")]
        [SerializeField] private float _holdSeconds = 0.7f;

        [SerializeField] private float _fadeDuration = 0.25f;

        [Tooltip("Cỡ icon so với một ô.")]
        [SerializeField] private float _scale = 1.4f;

        [Tooltip("Order in Layer của icon.")]
        [SerializeField] private int _sortingOrder = 13;

        private BoardView _boardView;
        private SpriteRenderer _icon;
        private Sequence _sequence;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;

            KillSequence();
        }

        private void HandleBoardRebuilt() => Stop();

        /// Thả icon kính lúp xuống ô gợi ý.
        public void Play(Vector2Int cell)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null || _iconPrefab == null) return;

            KillSequence();

            var icon = EnsureIcon();
            var target = (Vector3)layout.CellToWorldCenter(cell.x, cell.y);

            target.z = _root != null ? _root.position.z : 0f;

            icon.transform.position = target + Vector3.up * _dropHeight;
            icon.transform.localScale = Vector3.one * _scale;
            icon.sortingOrder = _sortingOrder;

            SetAlpha(icon, 0f);
            icon.gameObject.SetActive(true);

            _sequence = DOTween.Sequence().SetTarget(icon);

            if (_startDelay > 0f) _sequence.AppendInterval(_startDelay);

            _sequence.Append(icon.transform.DOMove(target, _dropDuration).SetEase(Ease.InQuad));
            _sequence.Join(CreateFade(icon, 1f, Mathf.Min(_dropDuration, 0.15f)));

            _sequence.Append(icon.transform.DOScale(_scale * 0.82f, 0.08f).SetEase(Ease.OutQuad));
            _sequence.Append(icon.transform.DOScale(_scale, 0.16f).SetEase(Ease.OutBack));

            if (_holdSeconds > 0f) _sequence.AppendInterval(_holdSeconds);

            _sequence.Append(CreateFade(icon, 0f, _fadeDuration));
            _sequence.OnComplete(Stop);
        }

        public void Stop()
        {
            KillSequence();

            if (_icon != null) _icon.gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureIcon()
        {
            if (_icon == null) _icon = Instantiate(_iconPrefab, _root);

            return _icon;
        }

        /// Tạo tween mờ alpha cho sprite.
        private static Tween CreateFade(SpriteRenderer icon, float targetAlpha, float duration)
        {
            return DOTween.To(
                () => icon.color.a,
                alpha => SetAlpha(icon, alpha),
                targetAlpha,
                duration);
        }

        private static void SetAlpha(SpriteRenderer icon, float alpha)
        {
            var color = icon.color;
            color.a = alpha;
            icon.color = color;
        }

        private void KillSequence()
        {
            _sequence?.Kill();
            _sequence = null;
        }
    }
}
