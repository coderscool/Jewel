using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace JewelPainter.Gameplay.Board
{
    /// Zoom và kéo bảng.
    public class BoardCamera : MonoBehaviour
    {
        private const float DefaultMinSize = 9f;

        private const float ScrollZoomSpeed = 0.001f;
        private const float PinchZoomSpeed = 0.005f;

        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _boardView;

        [Tooltip("Phần màn hình được kéo ra ngoài mép bảng.")]
        [Range(0f, 1f)]
        [SerializeField] private float _panMarginScreenFraction = 0.5f;

        [Tooltip("Thời gian camera bay tới ô gợi ý, tính bằng giây.")]
        [SerializeField] private float _focusDuration = 0.4f;

        [Tooltip("Vừa bắt đầu bay thì bỏ qua input trong ngần này giây.")]
        [SerializeField] private float _focusInputGrace = 0.2f;

        [Header("Khung hình lúc chơi")]
        [Tooltip("Tỉ lệ chiều cao màn hình chừa ở trên cho HUD.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _playMarginTop = 0.24f;

        [Tooltip("Tỉ lệ chiều cao màn hình chừa ở dưới cho thanh màu.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _playMarginBottom = 0.24f;

        [Tooltip("Tỉ lệ bề ngang màn hình dành cho bức tranh lúc chơi.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _playBoardWidthFraction = 0.9f;

        [Header("Khung hình lúc thắng màn")]
        [Tooltip("Tỉ lệ chiều cao màn hình chừa ở trên cho băng chúc mừng lúc thắng.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _winMarginTop = 0.24f;

        [Tooltip("Tỉ lệ chiều cao màn hình chừa ở dưới cho phần thưởng và nút Continue lúc thắng.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _winMarginBottom = 0.24f;

        [Tooltip("Tỉ lệ bề ngang màn hình dành cho bức tranh lúc thắng.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _winBoardWidthFraction = 0.9f;

        [Tooltip("Khoá kéo và zoom trong lúc khung hình thắng màn đang giữ.")]
        [SerializeField] private bool _freezeAfterWin = true;

        private ILevelService _levelService;
        private BoardInput _boardInput;

        private float _minSize = 1f;
        private float _maxSize = 10f;

        private bool _isDragging;
        private Vector2 _dragOriginWorld;
        private float _lastPinchDistance;

        private int _lastTouchCount;

        private bool _gestureOverUI;
        private bool _hasGesture;

        private float _viewBandCenter = 0.5f;

        private bool _winFraming;

        private bool _isFocusing;
        private bool _focusCancelOnInput;
        private float _focusMoveDuration;
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

            _camera.orthographicSize = _maxSize;

            _isDragging = false;
            _lastPinchDistance = 0f;
            _lastTouchCount = 0;
            _isFocusing = false;

            _winFraming = false;

            _viewBandCenter = ResolveBandCenter(
                _playMarginTop, _playMarginBottom, BoardScreenFraction(layout, _maxSize));

            transform.position = new Vector3(0f, ViewCenterY(_maxSize), transform.position.z);
        }

        /// Đưa camera tới một ô và phóng sát nhất.
        public void FocusOn(Vector2Int cell)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return;

            var center = layout.CellToWorldCenter(cell.x, cell.y);

            BeginMove(new Vector2(center.x, center.y), _minSize, _focusDuration, true);
        }

        /// Bay về đúng khung hình lúc vào màn: tâm bảng, mức kéo xa nhất, lề của lúc chơi.
        public void ResetFraming(float duration)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return;

            _winFraming = false;

            _viewBandCenter = ResolveBandCenter(
                _playMarginTop, _playMarginBottom, BoardScreenFraction(layout, _maxSize));

            BeginMove(new Vector2(0f, ViewCenterY(_maxSize)), _maxSize, duration, true);
        }

        /// Đưa camera về toàn cảnh: tâm bảng, mức kéo xa nhất.
        public void FrameWholeBoard(float duration) => FrameWholeBoard(duration, 0f);

        public void FrameWholeBoard(float duration, float extraCells)
        {
            FrameWholeBoard(duration, extraCells, 0f);
        }

        /// Đưa camera về toàn cảnh, chừa thêm viền và nhấc tranh lên.
        public void FrameWholeBoard(float duration, float extraCells, float riseFraction)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return;

            var padding = Mathf.Max(0f, extraCells) * 2f;
            var width = layout.Width + padding;
            var height = layout.Height + padding;

            var size = FitSize(
                width, height, BandHeight(_winMarginTop, _winMarginBottom), _winBoardWidthFraction);

            _viewBandCenter = Mathf.Clamp01(
                ResolveBandCenter(_winMarginTop, _winMarginBottom, size > 0f ? height / (2f * size) : 1f)
                + riseFraction);

            _winFraming = true;

            BeginMove(new Vector2(0f, ViewCenterY(size)), size, duration, false);
        }

        /// Bắt đầu một cú di chuyển camera.
        private void BeginMove(Vector2 targetPosition, float targetSize, float duration, bool cancelOnInput)
        {
            var position = transform.position;

            _focusStartPosition = position;
            _focusStartSize = _camera.orthographicSize;
            _focusTargetPosition = new Vector3(targetPosition.x, targetPosition.y, position.z);
            _focusTargetSize = targetSize;
            _focusMoveDuration = duration;
            _focusCancelOnInput = cancelOnInput;

            _focusElapsed = 0f;
            _isFocusing = true;

            _isDragging = false;
            _lastPinchDistance = 0f;
        }

        /// Dải màn hình còn lại sau khi trừ hai lề, theo phần chiều cao màn.
        private static float BandHeight(float marginTop, float marginBottom)
        {
            return Mathf.Max(0.2f, 1f - Mathf.Clamp01(marginTop) - Mathf.Clamp01(marginBottom));
        }

        /// Tâm khung nhìn cho một bức tranh cao `boardFraction` phần chiều cao màn, theo toạ độ chuẩn hoá 0..1.
        private static float ResolveBandCenter(float marginTop, float marginBottom, float boardFraction)
        {
            var half = Mathf.Max(0f, boardFraction) * 0.5f;

            var lowest = Mathf.Clamp01(marginBottom) + half;
            var highest = 1f - Mathf.Clamp01(marginTop) - half;

            if (lowest > highest) return (lowest + highest) * 0.5f;

            return Mathf.Clamp(0.5f, lowest, highest);
        }

        /// Bức tranh chiếm bao nhiêu phần chiều cao màn hình ở mức zoom đã cho.
        private static float BoardScreenFraction(BoardLayout layout, float size)
        {
            return size > 0f ? layout.Height / (2f * size) : 1f;
        }

        /// Camera phải đứng ở toạ độ y nào để tâm bảng rơi đúng vào tâm dải.
        private float ViewCenterY(float size)
        {
            return -(_viewBandCenter - 0.5f) * 2f * size;
        }

        /// orthographicSize nhỏ nhất mà bảng vẫn nằm gọn trong phần màn hình cho phép.
        private float FitSize(BoardLayout layout, float heightFraction, float widthFraction)
        {
            return FitSize(layout.Width, layout.Height, heightFraction, widthFraction);
        }

        /// orthographicSize nhỏ nhất để một kích thước cho trước nằm gọn trong màn.
        private float FitSize(float width, float height, float heightFraction, float widthFraction)
        {
            var aspect = Mathf.Max(0.0001f, _camera.aspect);

            return Mathf.Max(
                height / (2f * Mathf.Clamp(heightFraction, 0.2f, 1f)),
                width / (2f * Mathf.Clamp(widthFraction, 0.2f, 1f) * aspect));
        }

        /// Lấy giới hạn zoom của màn đang chơi.
        private void ResolveZoomRange(BoardLayout layout)
        {
            var autoMax = FitSize(
                layout, BandHeight(_playMarginTop, _playMarginBottom), _playBoardWidthFraction);
            var autoMin = Mathf.Min(DefaultMinSize, autoMax);

            var config = _levelService != null ? _levelService.CurrentConfig : null;

            _maxSize = config != null && config.CameraMaxSize > 0f ? config.CameraMaxSize : autoMax;
            _minSize = config != null && config.CameraMinSize > 0f ? config.CameraMinSize : autoMin;

            if (_minSize <= _maxSize) return;

            Debug.LogWarning(
                $"Camera Min Size ({_minSize}) lớn hơn Max Size ({_maxSize}) trong " +
                $"'{config.name}' — đã đổi chỗ hai giá trị.");

            (_minSize, _maxSize) = (_maxSize, _minSize);
        }

        private void Update()
        {
            if (_boardView == null || _boardView.Layout == null) return;

            if (_isFocusing && !TryAdvanceFocus()) return;

            if (_winFraming && _freezeAfterWin)
            {
                ClampPosition();
                return;
            }

            UpdateGestureBlock();

            if (!HandleTouch()) HandleMouse();

            ClampPosition();
        }

        /// Trả false khi đã lo xong frame này (đang bay), true khi input được quyền điều khiển tiếp.
        private bool TryAdvanceFocus()
        {
            _focusElapsed += Time.deltaTime;

            if (_focusCancelOnInput && _focusElapsed > _focusInputGrace && IsUserTouchingScreen())
            {
                _isFocusing = false;
                return true;
            }

            var duration = Mathf.Max(0.0001f, _focusMoveDuration);
            var t = Mathf.Clamp01(_focusElapsed / duration);

            var eased = t * t * (3f - 2f * t);

            _camera.orthographicSize = Mathf.Lerp(_focusStartSize, _focusTargetSize, eased);
            transform.position = Vector3.Lerp(_focusStartPosition, _focusTargetPosition, eased);

            if (t >= 1f) _isFocusing = false;

            ClampPosition();
            return false;
        }

        private bool IsUserTouchingScreen()
        {
            if (IsUserPressing()) return true;

            var mouse = Mouse.current;

            return mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
        }

        /// Có ngón tay hoặc nút chuột nào đang được giữ không.
        private static bool IsUserPressing()
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

            return mouse.leftButton.isPressed || mouse.rightButton.isPressed;
        }

        /// Mở và đóng một cử chỉ, và chốt xem nó có bắt đầu trên UI không.
        private void UpdateGestureBlock()
        {
            if (!IsUserPressing())
            {
                _hasGesture = false;
                _gestureOverUI = false;
                return;
            }

            if (_hasGesture) return;

            _hasGesture = true;
            _gestureOverUI = IsPointerOverUI();
        }

        /// Con trỏ hoặc ngón tay có đang nằm trên UI không.
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// Trả true nếu cảm ứng đang được dùng — khi đó bỏ qua chuột.
        private bool HandleTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return false;

            TouchControl first = null;
            TouchControl second = null;
            var pressedCount = 0;

            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;

                pressedCount++;

                if (first == null) first = touch;
                else if (second == null) second = touch;
            }

            if (pressedCount != _lastTouchCount)
            {
                _isDragging = false;
                _lastPinchDistance = 0f;
            }

            _lastTouchCount = pressedCount;

            if (pressedCount == 0)
            {
                _isDragging = false;
                return false;
            }

            if (pressedCount == 1)
            {
                if (!CanDragStroke())
                {
                    _isDragging = false;
                    return true;
                }

                DragTo(first.position.ReadValue());
                return true;
            }

            if (_gestureOverUI)
            {
                _isDragging = false;
                _lastPinchDistance = 0f;
                return true;
            }

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
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI())
            {
                ApplyZoom(-scroll * ScrollZoomSpeed * _camera.orthographicSize);
            }

            if (mouse.rightButton.isPressed)
            {
                if (_gestureOverUI)
                {
                    _isDragging = false;
                    return;
                }

                DragTo(mouse.position.ReadValue());
                return;
            }

            if (!mouse.leftButton.isPressed || !CanDragStroke())
            {
                _isDragging = false;
                return;
            }

            DragTo(mouse.position.ReadValue());
        }

        /// Nét này có được dùng để kéo camera không.
        private bool CanDragStroke()
        {
            return _boardInput == null || _boardInput.CurrentStroke == BoardInput.StrokeOwner.Camera;
        }

        /// Kéo camera để điểm dưới ngón tay luôn giữ nguyên.
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
        private void ClampPosition()
        {
            var bounds = _boardView.Layout.WorldBounds;

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;

            var fraction = Mathf.Clamp01(_panMarginScreenFraction);

            var marginX = fraction * 2f * halfWidth;
            var marginY = fraction * 2f * halfHeight;

            var maxX = Mathf.Max(0f, bounds.extents.x - halfWidth + marginX);
            var maxY = Mathf.Max(0f, bounds.extents.y - halfHeight + marginY);

            var position = transform.position;

            var centerY = ViewCenterY(halfHeight);

            transform.position = new Vector3(
                Mathf.Clamp(position.x, -maxX, maxX),
                Mathf.Clamp(position.y, centerY - maxY, centerY + maxY),
                position.z);
        }
    }
}
