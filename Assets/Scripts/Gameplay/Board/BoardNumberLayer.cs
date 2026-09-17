using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Hiện chỉ số bảng màu lên từng ô.
    public class BoardNumberLayer : MonoBehaviour, IBoardNumbers
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private TextMeshPro _numberPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Vị trí trong dải zoom của màn mà số bắt đầu hiện (0 = lúc vào màn, 1 = lúc lớp màu tan hết).")]
        [Range(0f, 1f)]
        [SerializeField] private float _showAtZoomProgress = 0.2f;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì không hiện số khi LevelConfig không đặt Fade Switch Size.")]
        [SerializeField] private float _minCellScreenPixels = 32f;

        [Tooltip("Màu chữ số, dùng chung cho mọi ô.")]
        [SerializeField] private Color _numberColor = Color.black;

        [Tooltip("Số chữ lấy ra tối đa trong một frame.")]
        [SerializeField] private int _maxSpawnPerFrame = 400;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn.")]
        [SerializeField] private int _visibleMarginCells = 2;

        [Tooltip("Dải trễ quanh ngưỡng hiện số.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _showHysteresis = 0.08f;

        [Header("Thử nghiệm")]
        [Tooltip("Dựng tất cả số trong tầm nhìn trong một frame.")]
        [SerializeField] private bool _spawnAllInOneFrame;

        [Tooltip("Số chữ dựng sẵn cho mỗi số lúc vào màn.")]
        [SerializeField] private int _prewarmPerNumber = 64;

        [Tooltip("Dựng sẵn đúng số ô của mỗi số thay vì một con số cố định.")]
        [SerializeField] private bool _prewarmFromBoardSize = true;

        [Tooltip("Số chữ dựng sẵn tối đa trong một frame.")]
        [SerializeField] private int _prewarmPerFrame = 250;

        [Tooltip("Giới hạn lượng dựng sẵn theo sức chứa màn hình.")]
        [SerializeField] private bool _capPrewarmToScreen = true;

        [Tooltip("Hệ số an toàn nhân vào sức chứa màn hình khi cắt.")]
        [Range(1f, 3f)]
        [SerializeField] private float _prewarmScreenSafety = 1.3f;

        /// Một chữ số kèm renderer của nó.
        private struct Label
        {
            public TextMeshPro Text;
            public MeshRenderer Renderer;
            public int Number;
        }

        private readonly Dictionary<Vector2Int, Label> _active = new();

        private readonly Dictionary<int, Stack<Label>> _poolByNumber = new();

        private readonly List<Vector2Int> _toRelease = new();

        private readonly Dictionary<int, int> _cellCountByNumber = new();

        private readonly List<int> _prewarmNumbers = new();

        private Coroutine _prewarmRoutine;

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;
        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;

        private float _baseSize = -1f;

        private bool _needsBaseCapture = true;
        private bool _needsRefresh;

        private bool _hasHiddenLabels;

        private bool _prefabRejected;

        private bool _numbersShown;

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _boardView.OnCoverChanged += HandleCoverChanged;

            if (_flyEffect != null) _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            if (_boardView == null) return;

            _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            _boardView.OnCoverChanged -= HandleCoverChanged;
        }

        /// Thu chữ số của ô vừa có ngọc đáp xuống.
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex) => Release(cell);

        /// Ô đã tô và viên ngọc đã đáp.
        private bool IsDone(Vector2Int cell)
        {
            if (_paintService == null || !_paintService.IsPainted(cell.x, cell.y)) return false;

            return _flyEffect == null || !_flyEffect.IsInFlight(cell);
        }

        /// Xử lý khi màn hình che bảng mở hoặc đóng.
        private void HandleCoverChanged() => _needsRefresh = true;

        /// Kiểm tra prefab chữ số trước khi dùng.
        private bool ValidatePrefab()
        {
            if (_prefabRejected) return false;
            if (_numberPrefab == null) return false;
            if (_numberPrefab.GetComponent<MeshRenderer>() != null) return true;

            _prefabRejected = true;

            Debug.LogError($"{nameof(BoardNumberLayer)}: Number Prefab không có MeshRenderer nên " +
                           "lớp số bị tắt. Prefab phải là TextMeshPro (bản 3D đặt thẳng trong " +
                           "world), không phải TextMeshProUGUI (bản dành cho Canvas).", this);

            return false;
        }

        private void HandleBoardRebuilt()
        {
            if (!ValidatePrefab()) return;

            ReleaseAll();
            StartPrewarm();

            _numbersShown = false;

            _needsBaseCapture = true;
            _lastOrthographicSize = -1f;
        }

        /// Dựng sẵn chữ cho mọi số màn này dùng, lúc màn hình chờ đang che.
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
            if (_numberPrefab == null) yield break;

            var grid = _boardView.Grid;
            var colors = _boardView.Colors;

            if (grid == null || colors == null) yield break;

            CountCellsByNumber(grid, colors.Count);

            _prewarmNumbers.Clear();
            foreach (var pair in _cellCountByNumber) _prewarmNumbers.Add(pair.Key);

            yield return null;

            var ratio = ResolvePrewarmRatio(grid);

            var perFrame = _prewarmPerFrame > 0 ? _prewarmPerFrame : int.MaxValue;
            var budget = perFrame;

            foreach (var number in _prewarmNumbers)
            {
                var target = _prewarmFromBoardSize
                    ? Mathf.Max(_prewarmPerNumber,
                        Mathf.CeilToInt(_cellCountByNumber[number] * ratio))
                    : _prewarmPerNumber;

                var pool = PoolFor(number);

                while (pool.Count < target)
                {
                    pool.Push(CreateLabel(number));

                    if (--budget > 0) continue;

                    budget = perFrame;
                    yield return null;
                }
            }

            _prewarmRoutine = null;
        }

        /// Tỉ lệ cắt cho lượng chữ dựng sẵn.
        private float ResolvePrewarmRatio(PixelGrid grid)
        {
            if (!_capPrewarmToScreen || !_prewarmFromBoardSize) return 1f;

            var showSize = ResolveShowSize();
            if (showSize <= 0f) return 1f;

            var ceiling = Mathf.Max(showSize, _baseSize);
            var widest = Mathf.Lerp(showSize, ceiling, Mathf.Clamp01(_showHysteresis));

            var margin = 2f * Mathf.Max(0, _visibleMarginCells) + 1f;

            var viewCells =
                Mathf.Min(grid.Width, 2f * widest * _camera.aspect + margin) *
                Mathf.Min(grid.Height, 2f * widest + margin);

            var filled = 0;
            foreach (var pair in _cellCountByNumber) filled += pair.Value;

            if (filled <= 0) return 1f;

            return Mathf.Clamp01(viewCells * Mathf.Max(1f, _prewarmScreenSafety) / filled);
        }

        private void CountCellsByNumber(PixelGrid grid, int colorCount)
        {
            _cellCountByNumber.Clear();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index < 0 || index >= colorCount) continue;

                    var number = index + 1;

                    _cellCountByNumber.TryGetValue(number, out var count);
                    _cellCountByNumber[number] = count + 1;
                }
            }
        }

        private void LateUpdate()
        {
            if (_prefabRejected) return;
            if (_boardView == null || _boardView.Layout == null) return;

            if (_needsBaseCapture)
            {
                _baseSize = _camera.orthographicSize;
                _needsBaseCapture = false;
                _lastOrthographicSize = -1f;
            }

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

        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Numbers.Refresh");

        /// Sinh chữ cho các ô trong tầm nhìn; false khi còn việc dở.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            if (_boardView.IsCovered)
            {
                ReleaseAll();
                return true;
            }

            if (!ShouldShowNumbers())
            {
                ReleaseAll();
                return true;
            }

            if (_paintService != null && _paintService.IsComplete)
            {
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            var colors = _boardView.Colors;
            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            var budget = _spawnAllInOneFrame ? int.MaxValue : Mathf.Max(1, _maxSpawnPerFrame);

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;

                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;
                    if (index < 0 || index >= colors.Count) continue;
                    if (IsDone(cell)) continue;

                    var number = index + 1;
                    var label = Rent(number);

                    label.Text.transform.position = layout.CellToWorldCenter(x, y);

                    _active[cell] = label;
                    _hasHiddenLabels = true;

                    if (--budget <= 0) return false;
                }
            }

            RevealAll();
            return true;
        }

        /// Bật hết chữ đang ẩn lên cùng một lượt.
        private void RevealAll()
        {
            if (!_hasHiddenLabels) return;

            _hasHiddenLabels = false;

            foreach (var entry in _active.Values)
            {
                entry.Renderer.enabled = true;
            }
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

        /// Có hiện số ở mức zoom hiện tại không.
        private bool ShouldShowNumbers()
        {
            var threshold = ResolveShowSize();
            if (threshold <= 0f) return false;

            var ceiling = Mathf.Max(threshold, _baseSize);
            var limit = _numbersShown
                ? Mathf.Lerp(threshold, ceiling, Mathf.Clamp01(_showHysteresis))
                : threshold;

            _numbersShown = _camera.orthographicSize <= limit;

            return _numbersShown;
        }

        /// orthographicSize mà tại đó số bắt đầu hiện.
        private float ResolveShowSize()
        {
            var config = _boardView.Config;
            var fadeSwitchSize = config != null ? config.FadeSwitchSize : 0f;

            if (fadeSwitchSize > 0f && _baseSize > 0f)
            {
                return Mathf.Lerp(_baseSize, fadeSwitchSize, _showAtZoomProgress);
            }

            if (_minCellScreenPixels <= 0f) return 0f;

            return Screen.height / (2f * _minCellScreenPixels);
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

            _hasHiddenLabels = false;
        }

        /// Trả chữ về kho.
        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var entry)) return;

            entry.Renderer.enabled = false;
            PoolFor(entry.Number).Push(entry);

            _active.Remove(cell);
        }

        /// Lấy một chữ từ kho ở trạng thái ẩn.
        private Label Rent(int number)
        {
            var pool = PoolFor(number);

            return pool.Count > 0 ? pool.Pop() : CreateLabel(number);
        }

        /// Tạo một chữ mới.
        private Label CreateLabel(int number)
        {
            var text = Instantiate(_numberPrefab, _root);

            text.color = _numberColor;
            text.SetText("{0}", number);
            text.ForceMeshUpdate();

            var renderer = text.GetComponent<MeshRenderer>();
            renderer.enabled = false;

            return new Label { Text = text, Renderer = renderer, Number = number };
        }

        private Stack<Label> PoolFor(int number)
        {
            if (_poolByNumber.TryGetValue(number, out var pool)) return pool;

            pool = new Stack<Label>();
            _poolByNumber[number] = pool;

            return pool;
        }
    }
}
