using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đánh dấu những ô tô được bằng màu đang chọn, bằng sprite đặt lên từng ô.
    public class HintLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _hintPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì ngừng hiện marker.")]
        [SerializeField] private float _minCellScreenPixels = 2f;

        [Tooltip("Số marker dựng sẵn lúc vào màn.")]
        [SerializeField] private int _prewarmCount = 400;

        [Tooltip("Dựng sẵn đủ marker cho màu nhiều ô nhất.")]
        [SerializeField] private bool _prewarmFromLargestColor = true;

        [Tooltip("Số marker dựng sẵn tối đa trong một frame.")]
        [SerializeField] private int _prewarmPerFrame = 200;

        [Tooltip("Giới hạn lượng dựng sẵn theo sức chứa màn hình.")]
        [SerializeField] private bool _capPrewarmToScreen = true;

        [Tooltip("Hệ số an toàn nhân vào sức chứa màn hình khi cắt.")]
        [Range(1f, 3f)]
        [SerializeField] private float _prewarmScreenSafety = 1.3f;

        [Tooltip("Số marker sinh tối đa trong một frame.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Tooltip("Số marker sinh tối đa mỗi frame khi tô tự do đang chạy.")]
        [SerializeField] private int _freePaintMaxSpawnPerFrame = 250;

        [Tooltip("Số ô nới rộng quanh tầm nhìn khi tính toán.")]
        [SerializeField] private int _visibleMarginCells = 2;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();
        private readonly Dictionary<int, int> _colorCounts = new();

        private Coroutine _prewarmRoutine;

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
            _boardView.OnCoverChanged += HandleCoverChanged;
            _paintService.OnColorSelected += HandleColorSelected;

            _paintService.OnFreePaintChanged += HandleFreePaintChanged;

            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null)
            {
                _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
                _boardView.OnCoverChanged -= HandleCoverChanged;
            }

            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            if (_paintService == null) return;

            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnFreePaintChanged -= HandleFreePaintChanged;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            StartPrewarm();

            _lastOrthographicSize = -1f;
        }

        private void HandleColorSelected(int paletteIndex)
        {
            ReleaseAll();
            _needsRefresh = true;
        }

        /// Dựng lại marker khi tô tự do bật hoặc tắt.
        private void HandleFreePaintChanged(bool active)
        {
            ReleaseAll();
            _needsRefresh = true;
        }

        /// Xử lý khi màn hình che bảng mở hoặc đóng.
        private void HandleCoverChanged() => _needsRefresh = true;

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

            if (!_needsRefresh) return;

            _needsRefresh = !Refresh();
        }

        private bool HasCameraChanged()
        {
            if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

            return _lastCameraPosition != _camera.transform.position;
        }

        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Hints.Refresh");

        /// Sinh marker cho các ô trong tầm nhìn; false khi còn việc dở.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            if (_boardView.IsCovered)
            {
                ReleaseAll();
                return true;
            }

            if (_paintService.IsComplete)
            {
                ReleaseAll();
                return true;
            }

            var freePaint = _paintService.FreePaintActive;

            var selected = _paintService.SelectedPaletteIndex;
            if (!freePaint && selected < 0)
            {
                ReleaseAll();
                return true;
            }

            var cellPixels = CellScreenPixels();

            if (cellPixels < _minCellScreenPixels)
            {
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            if (layout == null || grid == null) return true;

            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            var perFrame = freePaint ? _freePaintMaxSpawnPerFrame : _maxSpawnPerFrame;
            var budget = perFrame > 0 ? perFrame : int.MaxValue;

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;

                    var index = grid.GetCell(x, y);

                    if (freePaint)
                    {
                        if (index == PixelGrid.EmptyCell) continue;
                    }
                    else if (index != selected)
                    {
                        continue;
                    }

                    if (IsDone(cell)) continue;

                    Show(cell);

                    if (--budget <= 0) return false;
                }
            }

            return true;
        }

        /// Tầm nhìn camera nới rộng thêm vài ô.
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

        /// Ô đã tô và viên ngọc đã đáp.
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

        /// Trả marker về kho.
        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var marker)) return;

            marker.enabled = false;
            _pool.Push(marker);
            _active.Remove(cell);
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.enabled = true;
                return pooled;
            }

            if (_hintPrefab != null) return Instantiate(_hintPrefab, _root);

            Debug.LogWarning($"{nameof(HintLayer)} chưa gán Hint Prefab — không có dấu hiệu nào hiện ra.");
            return null;
        }

        /// Dựng sẵn marker trải ra nhiều frame.
        private void StartPrewarm()
        {
            if (_prewarmRoutine != null) StopCoroutine(_prewarmRoutine);
            _prewarmRoutine = null;

            var routine = PrewarmRoutine();

            if (!isActiveAndEnabled)
            {
                while (routine.MoveNext()) { }
                return;
            }

            _prewarmRoutine = StartCoroutine(routine);
        }

        private IEnumerator PrewarmRoutine()
        {
            if (_hintPrefab == null) yield break;

            yield return null;

            var target = Mathf.Max(_prewarmCount, ResolvePrewarmTarget());
            var perFrame = _prewarmPerFrame > 0 ? _prewarmPerFrame : int.MaxValue;
            var budget = perFrame;

            while (_pool.Count < target)
            {
                var marker = Instantiate(_hintPrefab, _root);
                marker.enabled = false;
                _pool.Push(marker);

                if (--budget > 0) continue;

                budget = perFrame;
                yield return null;
            }

            _prewarmRoutine = null;
        }

        /// Số marker cần dựng sẵn: số ô của màu đông nhất, đã cắt theo sức chứa màn hình.
        private int ResolvePrewarmTarget()
        {
            var largest = LargestColorCellCount();

            if (!_capPrewarmToScreen || largest <= 0) return largest;

            var colored = 0;
            foreach (var pair in _colorCounts) colored += pair.Value;

            if (colored <= 0) return largest;

            var capacity = VisibleCellCapacity();
            if (capacity <= 0f) return largest;

            var ratio = Mathf.Clamp01(capacity * Mathf.Max(1f, _prewarmScreenSafety) / colored);

            return Mathf.CeilToInt(largest * ratio);
        }

        /// Số ô nhiều nhất lọt vào khung nhìn, ở mức zoom rộng nhất mà lớp này còn sống.
        private float VisibleCellCapacity()
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null || _camera == null) return 0f;

            var threshold = Mathf.Max(0.01f, _minCellScreenPixels);
            var widest = Mathf.Min(_camera.orthographicSize, Screen.height / (2f * threshold));

            var margin = 2f * Mathf.Max(0, _visibleMarginCells) + 1f;

            return Mathf.Min(layout.Width, 2f * widest * _camera.aspect + margin) *
                   Mathf.Min(layout.Height, 2f * widest + margin);
        }

        /// Số ô của màu có nhiều ô nhất.
        private int LargestColorCellCount()
        {
            if (!_prewarmFromLargestColor) return 0;

            var grid = _boardView != null ? _boardView.Grid : null;
            if (grid == null) return 0;

            _colorCounts.Clear();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;

                    _colorCounts.TryGetValue(index, out var count);
                    _colorCounts[index] = count + 1;
                }
            }

            var largest = 0;

            foreach (var pair in _colorCounts)
            {
                if (pair.Value > largest) largest = pair.Value;
            }

            return largest;
        }
    }
}
