using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đánh dấu những ô tô được bằng màu đang chọn, bằng sprite đặt lên từng ô.
    ///
    /// Chỉ sinh marker cho ô đang lọt trong tầm nhìn camera, nên số GameObject sống
    /// cùng lúc phụ thuộc mức zoom chứ không phụ thuộc kích thước bảng.
    ///
    /// Khác JewelLayer ở một chỗ quan trọng: ngọc bị cull thì ô vẫn còn màu trong
    /// texture nên không ai nhận ra, còn marker bị cull là mất hẳn dấu hiệu. Vì vậy
    /// ngưỡng ẩn ở đây để thấp hơn hẳn — xem tooltip.
    public class HintLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _hintPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì ngừng hiện marker. " +
                 "Để THẤP (4-6) vì gợi ý chủ yếu dùng lúc zoom xa để dò xem còn ô nào; " +
                 "đặt cao như lớp số sẽ làm nó biến mất đúng lúc cần nhất. " +
                 "Càng thấp thì càng nhiều object cùng lúc.")]
        [SerializeField] private float _minCellScreenPixels = 5f;

        [Tooltip("Số marker dựng sẵn lúc vào màn. Chọn màu là lúc duy nhất sinh hàng " +
                 "loạt object cùng lúc — dựng sẵn thì cú đó chỉ là lấy đồ khỏi kho.")]
        [SerializeField] private int _prewarmCount = 400;

        [Tooltip("Số marker được sinh tối đa trong MỘT frame. Đây là lớp nặng nhất vì " +
                 "chọn màu là sinh hàng loạt cùng lúc; chia ra nhiều frame thì không khựng.")]
        [SerializeField] private int _maxSpawnPerFrame = 32;

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
            _paintService.OnColorSelected += HandleColorSelected;

            // Gỡ marker lúc viên ngọc ĐÁP XUỐNG, không phải lúc bấm tô — gỡ sớm thì
            // ô trống trơn suốt quãng viên đang bay.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            if (_paintService == null) return;

            _paintService.OnColorSelected -= HandleColorSelected;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            Prewarm();

            _lastOrthographicSize = -1f;
        }

        private void HandleColorSelected(int paletteIndex)
        {
            ReleaseAll();
            _needsRefresh = true;
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex) => Release(cell);

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
            var selected = _paintService.SelectedPaletteIndex;
            if (selected < 0)
            {
                ReleaseAll();
                return true;
            }

            if (CellScreenPixels() < _minCellScreenPixels)
            {
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            if (layout == null || grid == null) return true;

            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            var budget = Mathf.Max(1, _maxSpawnPerFrame);

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;

                    if (grid.GetCell(x, y) != selected) continue;
                    if (IsDone(cell)) continue;

                    Show(cell);

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

        /// Ô đã tô nhưng viên ngọc còn đang bay thì vẫn coi là CHƯA xong — giữ marker
        /// tới lúc viên đáp xuống. Không có chỗ này thì chỉ cần kéo camera giữa lúc bay
        /// là marker biến mất sớm, đúng cái lỗi vừa sửa nhưng đi đường khác.
        private bool IsDone(Vector2Int cell)
        {
            if (!_paintService.IsPainted(cell.x, cell.y)) return false;

            return _flyEffect == null || !_flyEffect.IsInFlight(cell);
        }

        private void Show(Vector2Int cell)
        {
            var marker = Rent();
            if (marker == null) return;

            marker.transform.position = _boardView.Layout.CellToWorldCenter(cell.x, cell.y);

            _active[cell] = marker;
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
            if (!_active.TryGetValue(cell, out var marker)) return;

            marker.gameObject.SetActive(false);
            _pool.Push(marker);
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

            if (_hintPrefab != null) return Instantiate(_hintPrefab, _root);

            Debug.LogWarning($"{nameof(HintLayer)} chưa gán Hint Prefab — không có dấu hiệu nào hiện ra.");
            return null;
        }

        private void Prewarm()
        {
            if (_hintPrefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var marker = Instantiate(_hintPrefab, _root);
                marker.gameObject.SetActive(false);
                _pool.Push(marker);
            }
        }
    }
}
