using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace JewelPainter.Gameplay.Board
{
    /// Zoom và kéo bảng. Toàn bộ phần đọc input gói trong file này —
    /// đổi sang API input khác chỉ phải sửa ở đây.
    ///
    /// Phân vai với BoardInput theo VỊ TRÍ BẤM, không theo số ngón: bấm xuống ô đang
    /// hiện dấu gợi ý thì kéo là tô, bấm chỗ khác thì kéo là di chuyển camera.
    /// BoardInput quyết định, camera đọc lại qua CurrentStroke.
    ///
    /// Hai ngón thì luôn là zoom và di chuyển, không phụ thuộc bấm vào đâu.
    /// Chuột phải cũng luôn kéo được, làm đường thoát khi bảng dày ô gợi ý.
    public class BoardCamera : MonoBehaviour
    {
        /// Zoom gần nhất khi LevelConfig không đặt, tính bằng orthographicSize.
        private const float DefaultMinSize = 9f;

        /// Zoom xa nhất khi LevelConfig không đặt: trọn bảng cộng thêm lề.
        private const float AutoFitMargin = 1.1f;

        private const float ScrollZoomSpeed = 0.001f;
        private const float PinchZoomSpeed = 0.005f;

        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _boardView;

        [Tooltip("Kéo được ra ngoài mép bảng thêm bao nhiêu PHẦN MÀN HÌNH. 0.5 nghĩa là " +
                 "nửa chiều rộng màn theo trục ngang và nửa chiều cao theo trục dọc — kéo " +
                 "hết cỡ thì mép bảng nằm đúng giữa màn. 0 là khoá sát mép.")]
        [Range(0f, 1f)]
        [SerializeField] private float _panMarginScreenFraction = 0.5f;

        [Tooltip("Thời gian camera bay tới ô gợi ý, tính bằng giây. 0 là nhảy tức thì.")]
        [SerializeField] private float _focusDuration = 0.4f;

        [Tooltip("Vừa bắt đầu bay thì bỏ qua input trong ngần này giây. Cần, vì bấm nút " +
                 "gợi ý cũng là một cú chạm — không có khoảng chờ thì chính cú chạm đó " +
                 "huỷ luôn chuyến bay nó vừa gọi.")]
        [SerializeField] private float _focusInputGrace = 0.2f;

        private ILevelService _levelService;
        private BoardInput _boardInput;

        private float _minSize = 1f;
        private float _maxSize = 10f;

        private bool _isDragging;
        private Vector2 _dragOriginWorld;
        private float _lastPinchDistance;

        private bool _isFocusing;
        private float _focusElapsed;
        private Vector3 _focusStartPosition;
        private Vector3 _focusTargetPosition;
        private float _focusStartSize;
        private float _focusTargetSize;

        public void Init(BoardView boardView, ILevelService levelService, BoardInput boardInput)
        {
            _boardView = boardView;
            _levelService = levelService;
            _boardInput = boardInput;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt()
        {
            var layout = _boardView.Layout;
            if (layout == null) return;

            ResolveZoomRange(layout);

            // Vào màn là ở mức xa nhất.
            _camera.orthographicSize = _maxSize;
            transform.position = new Vector3(0f, 0f, transform.position.z);

            _isDragging = false;
            _lastPinchDistance = 0f;
            _isFocusing = false;
        }

        /// Đưa camera tới một ô và phóng sát nhất. Nút gợi ý gọi hàm này.
        ///
        /// Đích có thể nằm ngoài vùng kéo cho phép (ô ở sát mép bảng), nhưng không cần
        /// tự kẹp: ClampPosition chạy sau mỗi bước nên camera tự dừng đúng ở biên.
        public void FocusOn(Vector2Int cell)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return;

            var center = layout.CellToWorldCenter(cell.x, cell.y);
            var position = transform.position;

            _focusStartPosition = position;
            _focusStartSize = _camera.orthographicSize;
            _focusTargetPosition = new Vector3(center.x, center.y, position.z);
            _focusTargetSize = _minSize;

            _focusElapsed = 0f;
            _isFocusing = true;

            // Bỏ nét kéo đang dở, không thì frame sau nó tính lệch từ điểm ghim cũ.
            _isDragging = false;
            _lastPinchDistance = 0f;
        }

        /// LevelConfig đặt được giới hạn zoom cho từng màn. Để 0 hoặc âm thì tự tính
        /// theo kích thước bảng như trước.
        private void ResolveZoomRange(BoardLayout layout)
        {
            var fitByHeight = layout.Height / 2f;
            var fitByWidth = layout.Width / 2f / Mathf.Max(0.0001f, _camera.aspect);

            var autoMax = Mathf.Max(fitByHeight, fitByWidth) * AutoFitMargin;
            var autoMin = Mathf.Min(DefaultMinSize, autoMax);

            var config = _levelService != null ? _levelService.CurrentConfig : null;

            _maxSize = config != null && config.CameraMaxSize > 0f ? config.CameraMaxSize : autoMax;
            _minSize = config != null && config.CameraMinSize > 0f ? config.CameraMinSize : autoMin;

            if (_minSize <= _maxSize) return;

            // Mathf.Clamp với min > max trả về max, tức camera kẹt cứng một mức mà
            // không báo gì. Đổi chỗ và nói rõ còn hơn để người ta ngồi đoán.
            Debug.LogWarning(
                $"Camera Min Size ({_minSize}) lớn hơn Max Size ({_maxSize}) trong " +
                $"'{config.name}' — đã đổi chỗ hai giá trị.");

            (_minSize, _maxSize) = (_maxSize, _minSize);
        }

        private void Update()
        {
            if (_boardView == null || _boardView.Layout == null) return;

            if (_isFocusing && !TryAdvanceFocus()) return;

            if (!HandleTouch()) HandleMouse();

            ClampPosition();
        }

        /// Trả false khi đã lo xong frame này (đang bay), true khi input được quyền
        /// điều khiển tiếp.
        ///
        /// Người chơi chạm vào là huỷ chuyến bay ngay — không có gì bực bằng camera
        /// giằng lại tay mình. Nhưng phải chờ hết _focusInputGrace mới nghe input, vì
        /// cú chạm gọi chuyến bay này có thể còn chưa nhấc ra khỏi màn hình.
        private bool TryAdvanceFocus()
        {
            _focusElapsed += Time.deltaTime;

            if (_focusElapsed > _focusInputGrace && IsUserTouchingScreen())
            {
                _isFocusing = false;
                return true;
            }

            var duration = Mathf.Max(0.0001f, _focusDuration);
            var t = Mathf.Clamp01(_focusElapsed / duration);

            // Smoothstep: rời đi và dừng lại đều êm, không cần thư viện tween nào.
            var eased = t * t * (3f - 2f * t);

            _camera.orthographicSize = Mathf.Lerp(_focusStartSize, _focusTargetSize, eased);
            transform.position = Vector3.Lerp(_focusStartPosition, _focusTargetPosition, eased);

            if (t >= 1f) _isFocusing = false;

            ClampPosition();
            return false;
        }

        private bool IsUserTouchingScreen()
        {
            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.press.isPressed) return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return false;

            return mouse.leftButton.isPressed
                   || mouse.rightButton.isPressed
                   || Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
        }

        /// Trả true nếu cảm ứng đang được dùng — khi đó bỏ qua chuột.
        private bool HandleTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return false;

            TouchControl first = null;
            TouchControl second = null;

            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;

                if (first == null) first = touch;
                else
                {
                    second = touch;
                    break;
                }
            }

            if (first == null)
            {
                _isDragging = false;
                _lastPinchDistance = 0f;
                return false;
            }

            // MỘT ngón: chỉ kéo khi BoardInput không nhận nét này để tô.
            if (second == null)
            {
                _lastPinchDistance = 0f;

                if (!CanDragStroke())
                {
                    _isDragging = false;
                    return true;
                }

                DragTo(first.position.ReadValue());
                return true;
            }

            // HAI ngón: khoảng cách đổi thì zoom, trung điểm dịch thì di chuyển.
            var firstPosition = first.position.ReadValue();
            var secondPosition = second.position.ReadValue();
            var distance = Vector2.Distance(firstPosition, secondPosition);

            if (_lastPinchDistance > 0f)
            {
                ApplyZoom(-(distance - _lastPinchDistance) * PinchZoomSpeed * _camera.orthographicSize);
            }

            _lastPinchDistance = distance;

            DragTo((firstPosition + secondPosition) * 0.5f);
            return true;
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ApplyZoom(-scroll * ScrollZoomSpeed * _camera.orthographicSize);
            }

            // Chuột phải luôn kéo được, kể cả khi đang đứng trên ô tô được — đường thoát
            // khi bảng kín ô gợi ý mà vẫn muốn di chuyển.
            if (mouse.rightButton.isPressed)
            {
                DragTo(mouse.position.ReadValue());
                return;
            }

            // Chuột trái chỉ kéo khi BoardInput không nhận nét này để tô.
            if (!mouse.leftButton.isPressed || !CanDragStroke())
            {
                _isDragging = false;
                return;
            }

            DragTo(mouse.position.ReadValue());
        }

        /// BoardInput quyết định mỗi nét thuộc về ai ngay lúc bấm xuống. Camera chỉ kéo
        /// khi nét đó không phải nét tô — và cũng không kéo khi nét bắt đầu trên UI.
        private bool CanDragStroke()
        {
            return _boardInput == null || _boardInput.CurrentStroke == BoardInput.StrokeOwner.Camera;
        }

        /// Ghim điểm world dưới ngón tay, rồi mỗi frame dịch camera sao cho điểm đó
        /// quay lại đúng dưới ngón. Tự sửa sai nên không tích luỹ trôi.
        private void DragTo(Vector2 screenPosition)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                _dragOriginWorld = ScreenToWorld(screenPosition);
                return;
            }

            var current = ScreenToWorld(screenPosition);
            var move = _dragOriginWorld - current;

            transform.position += new Vector3(move.x, move.y, 0f);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            var depth = Mathf.Abs(transform.position.z);

            return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        private void ApplyZoom(float delta)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, _minSize, _maxSize);
        }

        /// Không cho kéo bảng đi mất.
        ///
        /// Lề đo theo MÀN HÌNH chứ không theo ô, nên zoom mức nào cũng kéo thừa ra được
        /// đúng bấy nhiêu phần màn — lề tính bằng ô thì lúc phóng sát nó chiếm gần hết
        /// màn, còn lúc kéo xa thì gần như không thấy.
        ///
        /// Với fraction = 0.5 công thức rút gọn thành maxX = extents.x: kéo hết cỡ thì
        /// mép bảng nằm đúng giữa màn hình.
        private void ClampPosition()
        {
            var bounds = _boardView.Layout.WorldBounds;

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;

            var fraction = Mathf.Clamp01(_panMarginScreenFraction);

            // Lề = fraction * CẢ chiều rộng màn = fraction * 2 * nửa chiều rộng.
            var marginX = fraction * 2f * halfWidth;
            var marginY = fraction * 2f * halfHeight;

            var maxX = Mathf.Max(0f, bounds.extents.x - halfWidth + marginX);
            var maxY = Mathf.Max(0f, bounds.extents.y - halfHeight + marginY);

            var position = transform.position;

            transform.position = new Vector3(
                Mathf.Clamp(position.x, -maxX, maxX),
                Mathf.Clamp(position.y, -maxY, maxY),
                position.z);
        }
    }
}
