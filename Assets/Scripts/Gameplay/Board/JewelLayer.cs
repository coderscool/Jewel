using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đặt viên ngọc lên những ô đã tô.
    public class JewelLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì không sinh ngọc.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        [Tooltip("Số ngọc dựng sẵn lúc vào màn.")]
        [SerializeField] private int _prewarmCount = 200;

        [Tooltip("Dựng sẵn đủ ngọc cho mọi ô có màu.")]
        [SerializeField] private bool _prewarmFromBoardSize = true;

        [Tooltip("Số viên dựng sẵn tối đa trong một frame.")]
        [SerializeField] private int _prewarmPerFrame = 500;

        [Tooltip("Giới hạn lượng dựng sẵn theo sức chứa màn hình.")]
        [SerializeField] private bool _capPrewarmToScreen = true;

        [Tooltip("Hệ số an toàn nhân vào sức chứa màn hình khi cắt.")]
        [Range(1f, 3f)]
        [SerializeField] private float _prewarmScreenSafety = 1.3f;

        [Tooltip("Số ngọc sinh tối đa trong một frame.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Tooltip("Số ô nới rộng quanh tầm nhìn khi tính toán.")]
        [SerializeField] private int _visibleMarginCells = 2;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();

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
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            StartPrewarm();

            _lastOrthographicSize = -1f;
        }

        /// Màn hình che bảng vừa mở hoặc vừa đóng.
        private void HandleCoverChanged() => _needsRefresh = true;

        /// Hiện ngọc ngay khi viên bay đáp xuống.
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_boardView.IsCovered) return;
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

            if (!_needsRefresh) return;

            _needsRefresh = !Refresh();
        }

        private bool HasCameraChanged()
        {
            if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

            return _lastCameraPosition != _camera.transform.position;
        }

        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Jewels.Refresh");

        /// Sinh ngọc cho các ô trong tầm nhìn; false khi còn việc dở.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            if (_boardView.IsCovered)
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
            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            var budget = _maxSpawnPerFrame > 0 ? _maxSpawnPerFrame : int.MaxValue;

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;
                    if (!_paintService.IsPainted(x, y)) continue;

                    if (_flyEffect != null && _flyEffect.IsInFlight(cell)) continue;

                    Show(cell, grid.GetCell(x, y));

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

        private void Show(Vector2Int cell, int paletteIndex)
        {
            if (_active.ContainsKey(cell)) return;

            var colors = _boardView.JewelColors;
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

        /// Trả viên ngọc về kho.
        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var jewel)) return;

            jewel.enabled = false;
            _pool.Push(jewel);
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

            if (_jewelPrefab != null) return Instantiate(_jewelPrefab, _root);

            Debug.LogWarning($"{nameof(JewelLayer)} chưa gán Jewel Prefab — ô tô xong chỉ có màu phẳng.");
            return null;
        }

        /// Dựng sẵn ngọc lúc vào màn.
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
            if (_jewelPrefab == null) yield break;

            yield return null;

            var target = Mathf.Max(_prewarmCount, ResolvePrewarmTarget());
            var perFrame = _prewarmPerFrame > 0 ? _prewarmPerFrame : int.MaxValue;
            var budget = perFrame;

            while (_pool.Count < target)
            {
                var jewel = Instantiate(_jewelPrefab, _root);
                jewel.enabled = false;
                _pool.Push(jewel);

                if (--budget > 0) continue;

                budget = perFrame;
                yield return null;
            }

            _prewarmRoutine = null;
        }

        /// Số viên cần dựng sẵn: số ô có màu, đã cắt theo sức chứa màn hình.
        private int ResolvePrewarmTarget()
        {
            var colored = ColoredCellCount();

            if (!_capPrewarmToScreen || colored <= 0) return colored;

            var capacity = VisibleCellCapacity();
            if (capacity <= 0f) return colored;

            return Mathf.Min(colored, Mathf.CeilToInt(capacity * Mathf.Max(1f, _prewarmScreenSafety)));
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

        /// Số ô có màu của màn.
        private int ColoredCellCount()
        {
            if (!_prewarmFromBoardSize) return 0;

            var grid = _boardView != null ? _boardView.Grid : null;
            if (grid == null) return 0;

            var count = 0;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.GetCell(x, y) != PixelGrid.EmptyCell) count++;
                }
            }

            return count;
        }
    }
}
