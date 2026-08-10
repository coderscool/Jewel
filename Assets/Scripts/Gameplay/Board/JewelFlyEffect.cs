using System;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Viên ngọc bay từ ô màu trên thanh chọn tới ô vừa tô.
    ///
    /// Là NGUỒN SỰ THẬT cho việc "ô này đã có ngọc chưa": JewelLayer chờ sự kiện
    /// OnJewelLanded chứ không nghe thẳng OnCellPainted. Nhờ vậy ngọc chỉ hiện khi
    /// viên bay đáp xuống, đúng cảm giác lấy ngọc từ khay gắn vào tranh.
    ///
    /// Mọi đường thoát đều phải bắn OnJewelLanded — không sinh được viên bay, hết chỗ
    /// trong hạn mức, hay thiếu điểm xuất phát đều bắn NGAY. Thiếu một đường thoát là
    /// ô đó kẹt vĩnh viễn không bao giờ có ngọc.
    public class JewelFlyEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Thời gian bay, tính bằng giây.")]
        [SerializeField] private float _duration = 0.35f;

        [Tooltip("Cỡ viên lúc mới rời thanh màu, so với cỡ ô. Nhỏ hơn 1 rồi to dần " +
                 "khi tới nơi cho cảm giác bay từ xa lại.")]
        [SerializeField] private float _startScale = 5f;

        [Tooltip("Số viên bay cùng lúc tối đa. Vượt quá thì ô vẫn được tô, chỉ là " +
                 "ngọc hiện ngay không có hiệu ứng.")]
        [SerializeField] private int _maxConcurrent = 24;

        [SerializeField] private int _prewarmCount = 24;

        [Tooltip("Order in Layer của viên NGỌC ĐANG BAY, để nó nổi trên mọi lớp của bảng. " +
                 "Đáp xuống thì trả về giá trị gốc của prefab.")]
        [SerializeField] private int _flyingSortingOrder = 15;

        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly Dictionary<SpriteRenderer, Tween> _tweens = new();
        private readonly HashSet<Vector2Int> _inFlight = new();
        private readonly HashSet<string> _warnings = new();

        private BoardView _boardView;
        private IPaintService _paintService;
        private IPaintOriginProvider _originProvider;

        /// Order in Layer gốc của prefab, đọc một lần để trả về đúng giá trị đó.
        private int _baseSortingOrder;
        private bool _hasBaseSortingOrder;

        /// Bắn khi ô đã thật sự có ngọc — JewelLayer nghe cái này.
        public event Action<Vector2Int, int> OnJewelLanded;

        public void Init(BoardView boardView, IPaintService paintService, IPaintOriginProvider originProvider)
        {
            _boardView = boardView;
            _paintService = paintService;
            _originProvider = originProvider;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _paintService.OnCellPainted += HandleCellPainted;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_paintService != null) _paintService.OnCellPainted -= HandleCellPainted;

            KillAllTweens();
        }

        /// Ô đang có viên bay tới thì JewelLayer chưa được hiện ngọc ở đó.
        public bool IsInFlight(Vector2Int cell) => _inFlight.Contains(cell);

        private void HandleBoardRebuilt()
        {
            KillAllTweens();
            _inFlight.Clear();
            Prewarm();
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            if (!TryStartFlight(cell, paletteIndex)) Land(cell, paletteIndex);
        }

        private bool TryStartFlight(Vector2Int cell, int paletteIndex)
        {
            var layout = _boardView.Layout;
            var colors = _boardView.Colors;

            if (layout == null || colors == null) return false;
            if (paletteIndex < 0 || paletteIndex >= colors.Count) return false;
            if (_tweens.Count >= _maxConcurrent) return false;

            if (_originProvider == null || !_originProvider.TryGetOriginWorldPosition(paletteIndex, out var origin))
            {
                // Không có điểm xuất phát thì ngọc hiện ngay, không bay. Im lặng ở đây
                // là kiểu hỏng khó chịu nhất: game vẫn chạy, chỉ mất hiệu ứng mà không
                // biết vì sao. Báo một lần rồi thôi.
                WarnOnce("Không lấy được vị trí ô màu trên thanh chọn — ngọc sẽ hiện ngay " +
                         "không có hiệu ứng bay. Kiểm tra ô World Camera của ColorPaletteBar.");
                return false;
            }

            var flyer = Rent();
            if (flyer == null)
            {
                WarnOnce($"{nameof(JewelFlyEffect)} chưa gán Jewel Prefab — không có hiệu ứng bay.");
                return false;
            }

            var target = (Vector3)layout.CellToWorldCenter(cell.x, cell.y);
            var depth = _root != null ? _root.position.z : 0f;
            origin.z = depth;
            target.z = depth;

            flyer.color = colors[paletteIndex];
            flyer.sortingOrder = _flyingSortingOrder;
            flyer.transform.position = origin;
            flyer.transform.localScale = Vector3.one * _startScale;

            _inFlight.Add(cell);

            // DOMove chứ không DOJump: DOJump tách trục Y ra tween riêng với easing khác
            // trục X, nên kể cả đặt jumpPower = 0 thì hai trục vẫn chạy lệch nhịp và
            // đường bay vẫn cong. DOMove nội suy thẳng giữa hai điểm.
            var sequence = DOTween.Sequence()
                .Append(flyer.transform.DOMove(target, _duration).SetEase(Ease.InOutQuad))
                .Join(flyer.transform.DOScale(1f, _duration).SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    Release(flyer);
                    _inFlight.Remove(cell);
                    Land(cell, paletteIndex);
                });

            _tweens[flyer] = sequence;
            return true;
        }

        /// Ô chỉ đổi từ xám sang màu thật ở đây, không phải lúc người chơi bấm.
        private void Land(Vector2Int cell, int paletteIndex)
        {
            _boardView.RevealCell(cell, paletteIndex);

            OnJewelLanded?.Invoke(cell, paletteIndex);
        }

        /// Tô là hành động lặp liên tục — cảnh báo mỗi lần sẽ ngập Console.
        private void WarnOnce(string message)
        {
            if (!_warnings.Add(message)) return;

            Debug.LogWarning(message);
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_jewelPrefab == null) return null;

            CacheBaseSortingOrder();

            return Instantiate(_jewelPrefab, _root);
        }

        /// Tween phải chết TRƯỚC khi viên quay về kho. Object tái sử dụng mang theo
        /// tween cũ sẽ bị kéo về vị trí của lần bay trước.
        ///
        /// Trả lại đủ những gì đã đổi lúc thuê: tween, scale, sortingOrder. Sót một cái
        /// là lần bay sau thừa hưởng trạng thái cũ.
        private void Release(SpriteRenderer flyer)
        {
            if (_tweens.TryGetValue(flyer, out var tween))
            {
                tween.Kill();
                _tweens.Remove(flyer);
            }

            flyer.transform.localScale = Vector3.one;
            flyer.sortingOrder = _baseSortingOrder;
            flyer.gameObject.SetActive(false);
            _pool.Push(flyer);
        }

        private void KillAllTweens()
        {
            foreach (var pair in _tweens)
            {
                pair.Value?.Kill();

                if (pair.Key == null) continue;

                // Đường thứ hai trả viên về pool (đổi màn). Phải reset đủ như Release,
                // không thì viên tái dùng ở màn sau mang theo sortingOrder lúc bay.
                pair.Key.transform.localScale = Vector3.one;
                pair.Key.sortingOrder = _baseSortingOrder;
                pair.Key.gameObject.SetActive(false);
                _pool.Push(pair.Key);
            }

            _tweens.Clear();
        }

        /// Đọc từ prefab, không đọc từ viên đang dùng — viên đó đã bị đổi sang
        /// _flyingSortingOrder rồi, lấy về là lưu nhầm giá trị bay làm giá trị gốc.
        private void CacheBaseSortingOrder()
        {
            if (_hasBaseSortingOrder || _jewelPrefab == null) return;

            _baseSortingOrder = _jewelPrefab.sortingOrder;
            _hasBaseSortingOrder = true;
        }

        private void Prewarm()
        {
            if (_jewelPrefab == null) return;

            CacheBaseSortingOrder();

            while (_pool.Count < _prewarmCount)
            {
                var flyer = Instantiate(_jewelPrefab, _root);
                flyer.gameObject.SetActive(false);
                _pool.Push(flyer);
            }
        }
    }
}
