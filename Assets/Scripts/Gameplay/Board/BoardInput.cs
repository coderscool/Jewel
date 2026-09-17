using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JewelPainter.Gameplay.Board
{
    /// Nhận chạm trên bảng và phân loại nét tô hay kéo.
    [DefaultExecutionOrder(-100)]
    public class BoardInput : MonoBehaviour
    {
        private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

        private const int MaxSnapReachCells = 8;

        public enum StrokeOwner
        {
            None,
            Paint,
            Camera,
        }

        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _boardView;

        [Tooltip("Thời gian giữ tay trên ô chưa tô để chọn màu của ô đó.")]
        [SerializeField] private float _holdToPickSeconds = 0.8f;

        [Tooltip("Khoảng xê dịch tối đa của ngón tay khi giữ để chọn màu, tính bằng pixel.")]
        [SerializeField] private float _holdMoveTolerancePixels = 24f;

        [Header("Hút vào ô tô được")]
        [Tooltip("Chạm hụt thì tìm ô tô được gần nhất.")]
        [SerializeField] private bool _snapToNearestPaintable;

        [Tooltip("Bán kính tìm ô khi đang tô, tính bằng pixel.")]
        [SerializeField] private float _snapRadiusPixels = 44f;

        [Tooltip("Bán kính tìm ô lúc đặt tay xuống, tính bằng pixel.")]
        [SerializeField] private float _snapBeginRadiusPixels = 40f;

        private IPaintService _paintService;

        private TutorialState _tutorialState;

        private Vector2Int _lastCell = NoCell;

        private Vector2Int _holdCell = NoCell;
        private Vector2 _holdStartScreen;
        private float _holdElapsed;

        private bool _holdConsumed;

        private bool _waitingForFullRelease;

        private bool _hasPendingNotify;
        private Vector2 _pendingNotifyScreen;

        public StrokeOwner CurrentStroke { get; private set; } = StrokeOwner.None;

        public void Init(BoardView boardView, IPaintService paintService, TutorialState tutorialState)
        {
            _boardView = boardView;
            _paintService = paintService;
            _tutorialState = tutorialState;
        }

        private void Update()
        {
            if (_camera == null) return;

            if (!TryReadPointer(out var screenPosition, out var canPaint))
            {
                FlushPendingNotify();

                CurrentStroke = StrokeOwner.None;
                _lastCell = NoCell;
                ResetHold();
                return;
            }

            if (!canPaint)
            {
                CurrentStroke = StrokeOwner.Camera;
                _lastCell = NoCell;
                ResetHold();
                return;
            }

            if (CurrentStroke == StrokeOwner.None)
            {
                CurrentStroke = DecideOwner(screenPosition);

                if (CurrentStroke == StrokeOwner.Camera) BeginHold(screenPosition);
            }

            if (CurrentStroke == StrokeOwner.Camera)
            {
                TickHold(screenPosition);
                TickPendingNotify(screenPosition);
            }

            if (CurrentStroke != StrokeOwner.Paint) return;

            PaintAt(screenPosition);
        }

        /// Ghi lại ô và điểm chạm lúc bắt đầu nét.
        private void BeginHold(Vector2 screenPosition)
        {
            ResetHold();

            if (_holdToPickSeconds <= 0f) return;
            if (_paintService == null || _boardView == null) return;
            if (!TryGetCell(screenPosition, out var cell)) return;
            if (!HasColorToPick(cell)) return;

            _holdCell = cell;
            _holdStartScreen = screenPosition;
        }

        private void TickHold(Vector2 screenPosition)
        {
            if (_holdConsumed || _holdCell == NoCell) return;

            var moved = screenPosition - _holdStartScreen;
            var tolerance = Mathf.Max(0f, _holdMoveTolerancePixels);

            if (moved.sqrMagnitude > tolerance * tolerance)
            {
                ResetHold();
                return;
            }

            _holdElapsed += Time.deltaTime;
            if (_holdElapsed < _holdToPickSeconds) return;

            _holdConsumed = true;

            var grid = _boardView.Grid;
            if (grid == null) return;
            if (_holdCell.x >= grid.Width || _holdCell.y >= grid.Height) return;

            _paintService.SelectColor(grid.GetCell(_holdCell.x, _holdCell.y));
        }

        /// Ô này có màu để chọn không.
        private bool HasColorToPick(Vector2Int cell)
        {
            var grid = _boardView.Grid;
            if (grid == null) return false;

            if (grid.GetCell(cell.x, cell.y) == PixelGrid.EmptyCell) return false;

            return !_paintService.IsPainted(cell.x, cell.y);
        }

        private void ResetHold()
        {
            _holdCell = NoCell;
            _holdElapsed = 0f;
            _holdConsumed = false;
        }

        private StrokeOwner DecideOwner(Vector2 screenPosition)
        {
            var stage = _tutorialState != null ? _tutorialState.Stage : TutorialStage.None;

            if (stage == TutorialStage.PickColor) return StrokeOwner.None;

            if (IsPointerOverUI()) return StrokeOwner.None;

            if (_paintService == null || _boardView == null || _boardView.Layout == null)
            {
                return StrokeOwner.Camera;
            }

            if (TryResolvePaintCell(screenPosition, CellUnder(screenPosition),
                    _snapBeginRadiusPixels, out _))
            {
                return StrokeOwner.Paint;
            }

            if (stage == TutorialStage.PaintCells) return StrokeOwner.None;

            if (_paintService.SelectedPaletteIndex < 0)
            {
                _hasPendingNotify = true;
                _pendingNotifyScreen = screenPosition;
            }

            return StrokeOwner.Camera;
        }

        /// Huỷ lời nhắc khi tay kéo đi quá xa.
        private void TickPendingNotify(Vector2 screenPosition)
        {
            if (!_hasPendingNotify) return;

            var moved = screenPosition - _pendingNotifyScreen;
            var tolerance = Mathf.Max(0f, _holdMoveTolerancePixels);

            if (moved.sqrMagnitude > tolerance * tolerance) _hasPendingNotify = false;
        }

        /// Chốt lời nhắc lúc nhấc hết tay.
        private void FlushPendingNotify()
        {
            if (!_hasPendingNotify) return;

            _hasPendingNotify = false;

            if (_paintService == null) return;

            if (_paintService.SelectedPaletteIndex >= 0) return;

            _paintService.RequireColor();
        }

        /// Trả false khi không còn gì chạm màn hình.
        private bool TryReadPointer(out Vector2 screenPosition, out bool canPaint)
        {
            screenPosition = default;
            canPaint = false;

            var screen = Touchscreen.current;
            if (screen != null)
            {
                var pressedCount = 0;
                Vector2 firstPosition = default;

                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;

                    if (pressedCount == 0) firstPosition = touch.position.ReadValue();

                    pressedCount++;
                }

                if (pressedCount > 1)
                {
                    _waitingForFullRelease = true;
                    screenPosition = firstPosition;
                    return true;
                }

                if (pressedCount == 1)
                {
                    screenPosition = firstPosition;
                    canPaint = !_waitingForFullRelease;
                    return true;
                }

                _waitingForFullRelease = false;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return false;

            screenPosition = mouse.position.ReadValue();
            canPaint = true;
            return true;
        }

        private void PaintAt(Vector2 screenPosition)
        {
            if (!TryGetCell(screenPosition, out var directCell))
            {
                _lastCell = NoCell;
                return;
            }

            if (directCell == _lastCell) return;

            _lastCell = directCell;

            if (!TryResolvePaintCell(screenPosition, directCell, _snapRadiusPixels, out var target))
            {
                return;
            }

            _paintService.TryPaint(target.x, target.y);
        }

        /// Ô sẽ được tô cho điểm chạm này.
        private bool TryResolvePaintCell(
            Vector2 screenPosition, Vector2Int directCell, float radiusPixels, out Vector2Int target)
        {
            target = directCell;

            if (_paintService.CanPaint(directCell.x, directCell.y)) return true;

            if (!_snapToNearestPaintable) return false;

            var grid = _boardView.Grid;
            var layout = _boardView.Layout;
            if (grid == null || layout == null) return false;

            var cellPixels = BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize);
            if (cellPixels <= 0f) return false;

            var radiusCells = Mathf.Max(0f, radiusPixels) / cellPixels;
            if (radiusCells <= 0f) return false;

            var reach = Mathf.Min(Mathf.CeilToInt(radiusCells), MaxSnapReachCells);

            var world = ScreenToWorld(screenPosition);

            var bestSqr = radiusCells * radiusCells;
            var found = false;

            var minX = Mathf.Max(0, directCell.x - reach);
            var maxX = Mathf.Min(grid.Width - 1, directCell.x + reach);
            var minY = Mathf.Max(0, directCell.y - reach);
            var maxY = Mathf.Min(grid.Height - 1, directCell.y + reach);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!_paintService.CanPaint(x, y)) continue;

                    var sqr = (layout.CellToWorldCenter(x, y) - world).sqrMagnitude;

                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    target = new Vector2Int(x, y);
                    found = true;
                }
            }

            return found;
        }

        /// Ô dưới ngón, kể cả khi nó nằm ngoài lưới.
        private Vector2Int CellUnder(Vector2 screenPosition)
        {
            _boardView.Layout.TryWorldToCell(ScreenToWorld(screenPosition), out var cell);

            return cell;
        }

        private bool TryGetCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = NoCell;

            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return false;

            return layout.TryWorldToCell(ScreenToWorld(screenPosition), out cell);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            var depth = Mathf.Abs(_camera.transform.position.z);

            return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
