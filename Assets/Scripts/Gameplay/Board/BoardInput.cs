using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JewelPainter.Gameplay.Board
{
    /// Nhận chạm trên bảng và quyết định NÉT NÀY THUỘC VỀ AI.
    ///
    /// Luật: bấm xuống ô đang hiện dấu gợi ý thì kéo là tô; bấm xuống chỗ khác thì kéo
    /// là di chuyển camera. Phân theo VỊ TRÍ BẤM chứ không theo số ngón, nên người chơi
    /// không phải học thao tác riêng — chỗ nào tô được thì tô, chỗ nào không thì kéo.
    ///
    /// Chạy sớm hơn BoardCamera (DefaultExecutionOrder) vì camera phải đọc kết quả
    /// quyết định ở đây trước khi tự xử lý kéo. Unity không đảm bảo thứ tự Update mặc
    /// định, để nguyên thì một frame nào đó camera sẽ đọc trạng thái cũ.
    [DefaultExecutionOrder(-100)]
    public class BoardInput : MonoBehaviour
    {
        private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

        public enum StrokeOwner
        {
            /// Chưa có nét nào, hoặc nét bắt đầu trên UI nên không ai được nhận.
            None,
            Paint,
            Camera,
        }

        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _boardView;

        private IPaintService _paintService;
        private Vector2Int _lastCell = NoCell;

        /// BoardCamera đọc cái này để biết có được kéo hay không.
        public StrokeOwner CurrentStroke { get; private set; } = StrokeOwner.None;

        public void Init(BoardView boardView, IPaintService paintService)
        {
            _boardView = boardView;
            _paintService = paintService;
        }

        private void Update()
        {
            if (_camera == null) return;

            if (!TryGetStrokePosition(out var screenPosition))
            {
                CurrentStroke = StrokeOwner.None;
                _lastCell = NoCell;
                return;
            }

            // Quyết định MỘT LẦN lúc bắt đầu nét rồi giữ nguyên tới khi nhả tay.
            // Xét lại mỗi frame thì kéo qua một ô tô được là camera đang di bỗng nhảy
            // sang chế độ tô giữa chừng.
            if (CurrentStroke == StrokeOwner.None) CurrentStroke = DecideOwner(screenPosition);

            if (CurrentStroke != StrokeOwner.Paint) return;

            PaintAt(screenPosition);
        }

        private StrokeOwner DecideOwner(Vector2 screenPosition)
        {
            // Chạm trúng thanh màu thì khoá cả nét: không tô, mà cũng không kéo camera.
            if (IsPointerOverUI()) return StrokeOwner.None;

            if (_paintService == null || _boardView == null || _boardView.Layout == null)
            {
                return StrokeOwner.Camera;
            }

            if (!TryGetCell(screenPosition, out var cell)) return StrokeOwner.Camera;

            return _paintService.CanPaint(cell.x, cell.y) ? StrokeOwner.Paint : StrokeOwner.Camera;
        }

        /// false khi không có ngón nào, hoặc khi có từ hai ngón trở lên — lúc đó
        /// BoardCamera lo việc zoom và di chuyển, không ai tô cả.
        private bool TryGetStrokePosition(out Vector2 screenPosition)
        {
            screenPosition = default;

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
                    if (pressedCount > 1) return false;
                }

                if (pressedCount == 1)
                {
                    screenPosition = firstPosition;
                    return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return false;

            screenPosition = mouse.position.ReadValue();
            return true;
        }

        private void PaintAt(Vector2 screenPosition)
        {
            if (!TryGetCell(screenPosition, out var cell))
            {
                _lastCell = NoCell;
                return;
            }

            // Kéo chậm thì một ô nằm dưới ngón nhiều frame liền — chỉ xử lý lần đầu.
            if (cell == _lastCell) return;

            _lastCell = cell;
            _paintService.TryPaint(cell.x, cell.y);
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
