using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đặt viên ngọc lên những ô đã tô.
    ///
    /// Chỉ sinh ngọc cho ô đang lọt trong tầm nhìn camera, nên số GameObject sống
    /// cùng lúc phụ thuộc mức zoom chứ KHÔNG phụ thuộc số ô đã tô. Bảng tô kín 4000 ô
    /// vẫn chỉ có chừng trăm viên tồn tại.
    ///
    /// Cull được là nhờ BoardView đã ghi màu vào texture: viên ngọc bị gỡ thì ô đó
    /// vẫn còn nguyên màu bên dưới, người chơi không nhận ra có gì biến mất.
    public class JewelLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì không sinh ngọc — " +
                 "ở cỡ đó viên ngọc bé hơn hạt gạo mà vẫn tốn một GameObject. " +
                 "Màu của ô vẫn hiện vì nó nằm trong texture của bảng.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        [Tooltip("Số ngọc dựng sẵn lúc vào màn, nên đủ phủ một màn hình.")]
        [SerializeField] private int _prewarmCount = 200;

        [Tooltip("Số ngọc được sinh tối đa trong MỘT frame. Zoom liên tục đẩy nhiều ô vào " +
                 "tầm nhìn cùng lúc; chia ra nhiều frame thì không có cú khựng nào.")]
        [SerializeField] private int _maxSpawnPerFrame = 24;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn, để ô ở rìa " +
                 "không bị thu về rồi sinh lại liên tục khi camera nhích.")]
        [SerializeField] private int _visibleMarginCells = 2;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;
        private bool _needsRefresh;

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;

            // Nghe lúc viên bay ĐÁP XUỐNG, không phải lúc ô được tô — nhờ vậy ngọc chỉ
            // hiện khi hiệu ứng kết thúc. JewelFlyEffect luôn bắn sự kiện này kể cả khi
            // không bay được, nên không có ô nào bị bỏ quên.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            Prewarm();

            _lastOrthographicSize = -1f;   // ép tính lại ở LateUpdate kế tiếp
        }

        /// Viên bay vừa đáp xuống: hiện ngọc ngay nếu ô đó đang trong tầm nhìn,
        /// không đợi camera động.
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_boardView.Layout == null) return;
            if (CellScreenPixels() < _minCellScreenPixels) return;
            if (!_boardView.Layout.VisibleCells(CameraWorldRect()).Contains(cell)) return;

            Show(cell, paletteIndex);
        }

        private void LateUpdate()
        {
            if (_boardView == null || _boardView.Layout == null) return;

            if (HasCameraChanged())
            {
                _lastCameraPosition = _camera.transform.position;
                _lastOrthographicSize = _camera.orthographicSize;
                _needsRefresh = true;
            }

            // Còn việc dở từ frame trước thì làm tiếp, kể cả khi camera đã đứng yên.
            if (!_needsRefresh) return;

            _needsRefresh = !Refresh();
        }

        private bool HasCameraChanged()
        {
            if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

            return _lastCameraPosition != _camera.transform.position;
        }

        /// true khi đã phủ hết ô trong tầm nhìn; false khi hết hạn mức sinh của frame này.
        private bool Refresh()
        {
            if (CellScreenPixels() < _minCellScreenPixels)
            {
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            var budget = Mathf.Max(1, _maxSpawnPerFrame);

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;
                    if (!_paintService.IsPainted(x, y)) continue;

                    // Ô đang có viên bay tới thì để hiệu ứng lo, đừng hiện trước.
                    if (_flyEffect != null && _flyEffect.IsInFlight(cell)) continue;

                    Show(cell, grid.GetCell(x, y));

                    if (--budget <= 0) return false;
                }
            }

            return true;
        }

        /// Nới rộng tầm nhìn thêm vài ô để camera nhích một chút không làm ô ở rìa bị
        /// thu về rồi sinh lại liên tục.
        private Rect ExpandedCameraRect()
        {
            var rect = CameraWorldRect();
            var margin = Mathf.Max(0, _visibleMarginCells);

            rect.xMin -= margin;
            rect.xMax += margin;
            rect.yMin -= margin;
            rect.yMax += margin;

            return rect;
        }

        private void Show(Vector2Int cell, int paletteIndex)
        {
            if (_active.ContainsKey(cell)) return;

            var colors = _boardView.Colors;
            if (colors == null || paletteIndex < 0 || paletteIndex >= colors.Count) return;

            var jewel = Rent();
            if (jewel == null) return;

            jewel.transform.position = _boardView.Layout.CellToWorldCenter(cell.x, cell.y);
            jewel.color = colors[paletteIndex];

            _active[cell] = jewel;
        }

        private float CellScreenPixels()
        {
            return BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize);
        }

        private Rect CameraWorldRect()
        {
            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;
            var center = _camera.transform.position;

            return new Rect(
                center.x - halfWidth,
                center.y - halfHeight,
                halfWidth * 2f,
                halfHeight * 2f);
        }

        private void ReleaseOutside(RectInt visible)
        {
            _toRelease.Clear();

            foreach (var pair in _active)
            {
                if (!visible.Contains(pair.Key)) _toRelease.Add(pair.Key);
            }

            foreach (var cell in _toRelease) Release(cell);
        }

        private void ReleaseAll()
        {
            _toRelease.Clear();
            foreach (var pair in _active) _toRelease.Add(pair.Key);

            foreach (var cell in _toRelease) Release(cell);
        }

        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var jewel)) return;

            jewel.gameObject.SetActive(false);
            _pool.Push(jewel);
            _active.Remove(cell);
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_jewelPrefab != null) return Instantiate(_jewelPrefab, _root);

            Debug.LogWarning($"{nameof(JewelLayer)} chưa gán Jewel Prefab — ô tô xong chỉ có màu phẳng.");
            return null;
        }

        /// Dựng sẵn lúc vào màn, lúc màn hình đang chuyển cảnh nên không ai thấy.
        private void Prewarm()
        {
            if (_jewelPrefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var jewel = Instantiate(_jewelPrefab, _root);
                jewel.gameObject.SetActive(false);
                _pool.Push(jewel);
            }
        }
    }
}
