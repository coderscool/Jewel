using DG.Tweening;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Thả một icon kính lúp xuống ô mà nút gợi ý vừa chỉ tới.
    ///
    /// Chạy SAU khi camera bay tới nơi. Thả ngay lúc bấm thì icon rơi vào một ô đang
    /// trôi ngang qua màn hình, và người chơi mất dấu nó giữa đường.
    ///
    /// Chỉ có MỘT icon sống cùng lúc: bấm gợi ý liên tục thì lần sau ghi đè lần trước.
    /// Đây không phải hiệu ứng hàng loạt như ngọc bay nên không cần kho.
    public class HintMarkerEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _iconPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Chờ ngần này giây rồi mới thả, tính từ lúc bấm nút gợi ý. Nên đặt LỚN " +
                 "HƠN Focus Duration của BoardCamera một chút để camera kịp tới nơi.")]
        [SerializeField] private float _startDelay = 0.45f;

        [Tooltip("Icon bắt đầu cao hơn ô bao nhiêu world unit. Một ô rộng đúng một unit, " +
                 "nên 6 là rơi từ ngoài mép trên màn hình ở mức zoom sát.")]
        [SerializeField] private float _dropHeight = 6f;

        [Tooltip("Thời gian rơi.")]
        [SerializeField] private float _dropDuration = 0.32f;

        [Tooltip("Nằm lại bao lâu sau khi chạm ô.")]
        [SerializeField] private float _holdSeconds = 0.7f;

        [SerializeField] private float _fadeDuration = 0.25f;

        [Tooltip("Cỡ icon so với một ô.")]
        [SerializeField] private float _scale = 1.4f;

        [Tooltip("Order in Layer. Phải LỚN HƠN của viên ngọc (4) để icon nằm trên, và " +
                 "nhỏ hơn Flying Sorting Order của JewelFlyEffect (15).")]
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

        /// HintFocusController gọi sau khi đã chọn được ô và ra lệnh cho camera bay tới.
        public void Play(Vector2Int cell)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null || _iconPrefab == null) return;

            KillSequence();

            var icon = EnsureIcon();
            var target = (Vector3)layout.CellToWorldCenter(cell.x, cell.y);

            // z lấy theo root để icon nằm đúng mặt phẳng với các lớp khác của bảng.
            target.z = _root != null ? _root.position.z : 0f;

            icon.transform.position = target + Vector3.up * _dropHeight;
            icon.transform.localScale = Vector3.one * _scale;
            icon.sortingOrder = _sortingOrder;

            SetAlpha(icon, 0f);
            icon.gameObject.SetActive(true);

            _sequence = DOTween.Sequence().SetTarget(icon);

            if (_startDelay > 0f) _sequence.AppendInterval(_startDelay);

            // InQuad cho cú rơi nhanh dần — vật rơi tự do tăng tốc, easing nào chậm dần
            // ở cuối sẽ đọc ra là "được đặt xuống" chứ không phải "rơi xuống".
            _sequence.Append(icon.transform.DOMove(target, _dropDuration).SetEase(Ease.InQuad));
            _sequence.Join(CreateFade(icon, 1f, Mathf.Min(_dropDuration, 0.15f)));

            // Nhún một cái lúc chạm: bẹt xuống rồi bật về cỡ cũ.
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

        /// DOTween.To trên alpha thay vì SpriteRenderer.DOFade: DOFade cho SpriteRenderer
        /// nằm trong module Sprite của DOTween, mà project cố ý chỉ dùng phần core.
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
