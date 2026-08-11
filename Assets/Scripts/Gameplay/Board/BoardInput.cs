using JewelPainter.Gameplay.Domain;
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
    /// Ngoài ra: giữ tay yên trên một ô CHƯA TÔ đủ lâu thì chọn luôn màu của ô đó.
    /// Chỉ áp dụng cho nét thuộc về camera — nét thuộc về Paint nghĩa là ô đó đã đúng
    /// màu đang chọn, chạm vào là tô, không có gì để chọn nữa.
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

        [Tooltip("Giữ tay yên trên một ô chưa tô lâu ngần này giây thì chọn màu của ô đó. " +
                 "Để 0 hoặc âm là tắt hẳn.")]
        [SerializeField] private float _holdToPickSeconds = 0.8f;

        [Tooltip("Ngón tay xê dịch quá ngần này PIXEL MÀN HÌNH thì coi như đang kéo camera " +
                 "và huỷ đếm giờ. Màn hình càng nhiều điểm ảnh thì càng nên nới ra.")]
        [SerializeField] private float _holdMoveTolerancePixels = 24f;

        private IPaintService _paintService;
        private Vector2Int _lastCell = NoCell;

        private Vector2Int _holdCell = NoCell;
        private Vector2 _holdStartScreen;
        private float _holdElapsed;

        /// Đã chọn màu cho nét này rồi. Giữ tiếp cũng không chọn lại — không thì cứ mỗi
        /// frame sau mốc 1.5 giây lại bắn thêm một lần SelectColor.
        private bool _holdConsumed;

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
                ResetHold();
                return;
            }

            // Quyết định MỘT LẦN lúc bắt đầu nét rồi giữ nguyên tới khi nhả tay.
            // Xét lại mỗi frame thì kéo qua một ô tô được là camera đang di bỗng nhảy
            // sang chế độ tô giữa chừng.
            if (CurrentStroke == StrokeOwner.None)
            {
                CurrentStroke = DecideOwner(screenPosition);

                if (CurrentStroke == StrokeOwner.Camera) BeginHold(screenPosition);
            }

            if (CurrentStroke == StrokeOwner.Camera) TickHold(screenPosition);

            if (CurrentStroke != StrokeOwner.Paint) return;

            PaintAt(screenPosition);
        }

        /// Ghi lại ô và điểm chạm lúc bắt đầu nét. Ô được chốt ở đây chứ không đọc lại
        /// mỗi frame: camera có nhích một chút thì ô dưới ngón vẫn là ô người chơi
        /// nhắm tới lúc đặt tay xuống.
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

            // So bình phương để khỏi phải khai căn — cùng kết quả, rẻ hơn.
            if (moved.sqrMagnitude > tolerance * tolerance)
            {
                ResetHold();
                return;
            }

            _holdElapsed += Time.deltaTime;
            if (_holdElapsed < _holdToPickSeconds) return;

            _holdConsumed = true;

            // Đọc lại lưới ở đây chứ không giữ tham chiếu từ BeginHold: màn có thể đã
            // chuyển trong lúc tay còn đang giữ, và bảng mới có thể nhỏ hơn — GetCell
            // không tự kiểm biên nên phải kiểm ở đây.
            var grid = _boardView.Grid;
            if (grid == null) return;
            if (_holdCell.x >= grid.Width || _holdCell.y >= grid.Height) return;

            _paintService.SelectColor(grid.GetCell(_holdCell.x, _holdCell.y));
        }

        /// Ô rỗng không có màu, ô đã tô thì màu của nó đã nằm sẵn trên bảng rồi.
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
