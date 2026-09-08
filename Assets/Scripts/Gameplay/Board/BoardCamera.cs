using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Header("Khung hình lúc chơi")]
        [Tooltip("Chừa bao nhiêu PHẦN CHIỀU CAO màn hình ở TRÊN cho HUD.\n\n" +
                 "HUD thật chỉ với xuống ~0.08 (hàng Coin / Booster / HomeButton, 167 trên " +
                 "canvas cao 2160). Để 0.24 là cố ý chừa DƯ, cho bằng lề của khung hình " +
                 "thắng màn — xem ô Play Board Width Fraction.\n\n" +
                 "Cái giá: bức tranh nào bị chiều DỌC chặn sẽ nhỏ hơn mức cần thiết lúc " +
                 "chơi. Trong 15 màn hiện tại chỉ Level 1 (39x52, cao và hẹp) dính, và chỉ " +
                 "trên màn vuông. Màn đó muốn to hơn thì điền Camera Max Size riêng cho nó — " +
                 "ô ghi đè vẫn còn nguyên tác dụng.\n\n" +
                 "CHỈ có tác dụng với những màn để Camera Max Size = 0. Màn nào điền số " +
                 "cứng thì số đó vẫn thắng.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _playMarginTop = 0.24f;

        [Tooltip("Chừa bao nhiêu PHẦN CHIỀU CAO màn hình ở DƯỚI cho thanh màu.\n\n" +
                 "Thanh màu đo được là 430 trên canvas cao 2160, tức đúng 0.20. Để 0.24 là " +
                 "cộng thêm một khoảng thở, vì tranh chạm sát thanh màu nhìn rất bí.\n\n" +
                 "Đây là ô quyết định trên MÁY RỘNG. Màn hình càng rộng thì vế bề ngang " +
                 "càng cho cỡ zoom nhỏ, tranh càng cao trên màn, và nó tiến sát thanh màu — " +
                 "trên điện thoại dựng đứng thì không thấy vì tranh nhỏ hơn nhiều.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _playMarginBottom = 0.24f;

        [Tooltip("Tương tự theo chiều ngang.\n\n" +
                 "Trên điện thoại dựng đứng thì ĐÂY mới là ô quyết định: bảng gần vuông mà " +
                 "màn hình thì cao, nên bề ngang luôn chật trước. Muốn chỉnh mức zoom lúc " +
                 "vào màn thì chỉnh ô này, không phải ô trên.\n\n" +
                 "Càng THẤP thì camera càng được kéo ra xa, tranh càng nhỏ trên màn.\n\n" +
                 "ĐANG ĐẶT BẰNG bộ số của khung hình thắng màn, và đó là chủ ý: chỉ khi cả " +
                 "hai bộ giống hệt nhau thì tranh mới giữ nguyên cỡ lúc chuyển từ chơi sang " +
                 "thắng, TRÊN MỌI TỈ LỆ MÀN. Chỉnh lệch một ô là hai bên lại tách nhau ra ở " +
                 "một tỉ lệ màn nào đó — vì vế quyết định (dọc hay ngang) đổi theo tỉ lệ.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _playBoardWidthFraction = 0.9f;

        [Header("Khung hình lúc thắng màn")]
        [Tooltip("Chừa bao nhiêu PHẦN CHIỀU CAO màn hình ở TRÊN cho băng chúc mừng.\n\n" +
                 "Khai theo BỐ CỤC POPUP, không theo bảng. Sửa popup thì phải sửa lại đây — " +
                 "Gameplay không được phép nhìn thấy UI nên camera không tự đo được.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _winMarginTop = 0.24f;

        [Tooltip("Chừa bao nhiêu PHẦN CHIỀU CAO màn hình ở DƯỚI cho cụm phần thưởng và nút " +
                 "Continue.\n\n" +
                 "Cụm phần thưởng bắt đầu ở khoảng 0.22 tính từ dưới; để 0.24 cho chắc.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _winMarginBottom = 0.24f;

        [Tooltip("Tương tự theo chiều ngang. Popup thường phủ hết bề rộng nên ô này chủ yếu " +
                 "để chừa lề hai bên cho đẹp, không phải để tránh đè.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _winBoardWidthFraction = 0.9f;

        [Tooltip("Khoá kéo và zoom trong lúc khung hình thắng màn đang giữ.\n\n" +
                 "Bỏ tick thì người chơi kéo được bảng lúc popup đang mở — và cú kéo đầu " +
                 "tiên sẽ phá luôn khung hình vừa canh, vì phép kẹp zoom đưa camera về " +
                 "Camera Max Size. Lúc đó cũng chẳng còn gì để tô.")]
        [SerializeField] private bool _freezeAfterWin = true;

        private ILevelService _levelService;
        private BoardInput _boardInput;

        private float _minSize = 1f;
        private float _maxSize = 10f;

        private bool _isDragging;
        private Vector2 _dragOriginWorld;
        private float _lastPinchDistance;

        /// Số ngón đang chạm ở frame trước. Đổi số ngón là đổi luôn ý nghĩa của điểm
        /// ghim, nên phải ghim lại — xem HandleTouch.
        private int _lastTouchCount;

        /// Cử chỉ đang diễn ra bắt đầu TRÊN UI, nên camera không nhận nó.
        ///
        /// Chốt MỘT LẦN lúc bấm xuống rồi giữ tới khi nhả hết tay — cùng khuôn với
        /// BoardInput.DecideOwner, và vì cùng một lý do: hỏi lại mỗi frame thì kéo bảng
        /// ngang qua nút gợi ý là camera đứng khựng giữa chừng.
        private bool _gestureOverUI;
        private bool _hasGesture;

        /// Tâm của dải màn hình đang dùng, theo toạ độ chuẩn hoá 0..1 (0 đáy, 1 đỉnh).
        /// 0.5 là giữa màn.
        ///
        /// Giữ dạng CHUẨN HOÁ chứ không giữ khoảng lệch tính bằng world, và đó là điểm
        /// mấu chốt: khoảng lệch world tỉ lệ với mức zoom. Chốt cứng một con số world thì
        /// lúc người chơi phóng sát, cái lệch ấy vẫn còn nguyên và nó ăn mất một dải bảng
        /// ở rìa mà người chơi không tài nào kéo tới được.
        private float _viewBandCenter = 0.5f;

        /// Đang giữ khung hình thắng màn.
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

            // Vào màn là ở mức xa nhất.
            _camera.orthographicSize = _maxSize;

            _isDragging = false;
            _lastPinchDistance = 0f;
            _lastTouchCount = 0;
            _isFocusing = false;

            _winFraming = false;

            // Tính theo _maxSize THẬT, không theo mức auto: màn nào còn điền Camera Max
            // Size cứng thì phần đẩy lên cũng phải bám theo con số đó.
            _viewBandCenter = ResolveBandCenter(
                _playMarginTop, _playMarginBottom, BoardScreenFraction(layout, _maxSize));

            // Đặt camera vào chỗ ngay từ frame đầu, không đợi ClampPosition kéo.
            transform.position = new Vector3(0f, ViewCenterY(_maxSize), transform.position.z);
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

            BeginMove(new Vector2(center.x, center.y), _minSize, _focusDuration, true);
        }

        /// Đưa camera về toàn cảnh: tâm bảng, mức kéo xa nhất. Đoạn ăn mừng lúc thắng
        /// màn gọi hàm này.
        ///
        /// KHÔNG huỷ được bằng chạm, khác nút gợi ý: người chơi giằng camera giữa chừng
        /// chỉ làm hỏng nhịp của màn ăn mừng, mà lúc này cũng chẳng còn gì để tô.
        public void FrameWholeBoard(float duration)
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null) return;

            // Cỡ zoom tính TỪ CHỖ TRỐNG popup chừa lại, không lấy _maxSize.
            //
            // _maxSize là con số ngắm cho lúc CHƠI, khi màn hình chỉ có HUD ở trên và
            // thanh màu ở dưới. Dùng lại nó ở đây là bắt một con số phục vụ hai bố cục
            // khác hẳn nhau, và bảng nào cao so với bề ngang sẽ thò xuống dưới cụm thưởng.
            var size = FitSize(
                layout, BandHeight(_winMarginTop, _winMarginBottom), _winBoardWidthFraction);

            _viewBandCenter = ResolveBandCenter(
                _winMarginTop, _winMarginBottom, BoardScreenFraction(layout, size));

            _winFraming = true;

            BeginMove(new Vector2(0f, ViewCenterY(size)), size, duration, false);
        }

        /// Đích có thể nằm ngoài vùng kéo cho phép (ô ở sát mép bảng), nhưng không cần
        /// tự kẹp: ClampPosition chạy sau mỗi bước nên camera tự dừng đúng ở biên.
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

            // Bỏ nét kéo đang dở, không thì frame sau nó tính lệch từ điểm ghim cũ.
            _isDragging = false;
            _lastPinchDistance = 0f;
        }

        /// Dải màn hình còn lại sau khi trừ hai lề, theo phần chiều cao màn.
        /// Kẹp sàn 0.2 để hai lề khai quá tay không cho ra một dải rỗng.
        private static float BandHeight(float marginTop, float marginBottom)
        {
            return Mathf.Max(0.2f, 1f - Mathf.Clamp01(marginTop) - Mathf.Clamp01(marginBottom));
        }

        /// Tâm khung nhìn cho một bức tranh cao `boardFraction` phần chiều cao màn,
        /// theo toạ độ chuẩn hoá 0..1.
        ///
        /// Mặc định là GIỮA MÀN, và chỉ đẩy đi đúng bằng phần cần thiết để không lấn vào
        /// lề. Đây là điểm khác với việc canh vào giữa dải: canh giữa dải thì đẩy cả
        /// những màn hình vốn đã dư chỗ — điện thoại dựng đứng có tranh chỉ cao chừng 40%
        /// màn, hở rộng cả hai đầu, mà vẫn bị nhấc lên và trông chông chênh.
        ///
        /// Nói cách khác: hai cái lề là RÀNG BUỘC, không phải là bố cục. Máy nào đủ rộng
        /// để không chạm ràng buộc thì tranh cứ nằm giữa màn.
        private static float ResolveBandCenter(float marginTop, float marginBottom, float boardFraction)
        {
            var half = Mathf.Max(0f, boardFraction) * 0.5f;

            var lowest = Mathf.Clamp01(marginBottom) + half;
            var highest = 1f - Mathf.Clamp01(marginTop) - half;

            // Tranh cao hơn cả dải — thường vì màn đó điền Camera Max Size cứng và số đó
            // nhỏ hơn mức auto. Không có chỗ đứng nào không lấn, nên chia đều cho hai đầu.
            if (lowest > highest) return (lowest + highest) * 0.5f;

            return Mathf.Clamp(0.5f, lowest, highest);
        }

        /// Bức tranh chiếm bao nhiêu phần chiều cao màn hình ở mức zoom đã cho.
        private static float BoardScreenFraction(BoardLayout layout, float size)
        {
            return size > 0f ? layout.Height / (2f * size) : 1f;
        }

        /// Camera phải đứng ở toạ độ y nào để tâm bảng rơi đúng vào tâm dải.
        ///
        /// Khung nhìn cao đúng 2 * size, nên lệch 1 đơn vị chuẩn hoá là lệch 2 * size
        /// world. Dấu âm vì đẩy CAMERA xuống thì TRANH đi lên.
        ///
        /// Nhận size làm tham số chứ không đọc camera: nó được hỏi cả cho mức zoom ĐÍCH
        /// của một chuyến bay chưa bắt đầu, lẫn cho mức zoom HIỆN TẠI lúc kẹp vị trí.
        private float ViewCenterY(float size)
        {
            return -(_viewBandCenter - 0.5f) * 2f * size;
        }

        /// orthographicSize nhỏ nhất mà bảng vẫn nằm gọn trong phần màn hình cho phép.
        ///
        /// Nhận TỈ LỆ MÀN HÌNH chứ không nhận lề tuyệt đối, và đó là toàn bộ điểm của nó:
        /// một con số orthographicSize chỉ đúng với đúng một tỉ lệ màn; một tỉ lệ thì đúng
        /// với mọi máy, vì nó tự nhân lại với aspect ở đây.
        ///
        /// Trên điện thoại dựng đứng, vế BỀ NGANG gần như luôn là vế thắng: bảng gần vuông
        /// mà khung nhìn thì cao gấp rưỡi bề ngang.
        private float FitSize(BoardLayout layout, float heightFraction, float widthFraction)
        {
            var aspect = Mathf.Max(0.0001f, _camera.aspect);

            return Mathf.Max(
                layout.Height / (2f * Mathf.Clamp(heightFraction, 0.2f, 1f)),
                layout.Width / (2f * Mathf.Clamp(widthFraction, 0.2f, 1f) * aspect));
        }

        /// LevelConfig đặt được giới hạn zoom cho từng màn. Để 0 hoặc âm thì tự tính
        /// theo kích thước bảng như trước.
        private void ResolveZoomRange(BoardLayout layout)
        {
            // Cùng phép tính với khung hình thắng màn, khác mỗi cặp tỉ lệ. Một con số
            // cứng mỗi màn thì chết với đúng một tỉ lệ màn hình; tỉ lệ thì tự co.
            var autoMax = FitSize(
                layout, BandHeight(_playMarginTop, _playMarginBottom), _playBoardWidthFraction);
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

            // Khung hình thắng màn đã canh xong thì khoá lại — xem tooltip Freeze After Win.
            if (_winFraming && _freezeAfterWin)
            {
                ClampPosition();
                return;
            }

            UpdateGestureBlock();

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

            if (_focusCancelOnInput && _focusElapsed > _focusInputGrace && IsUserTouchingScreen())
            {
                _isFocusing = false;
                return true;
            }

            var duration = Mathf.Max(0.0001f, _focusMoveDuration);
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
            if (IsUserPressing()) return true;

            var mouse = Mouse.current;

            return mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
        }

        /// Có ngón tay hoặc nút chuột nào đang được GIỮ không. Không tính lăn chuột:
        /// lăn không có lúc bấm xuống và lúc nhả ra, nên nó không mở ra một cử chỉ nào
        /// để mà chốt.
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

        /// BoardInput đã có phép kiểm này cho nét TÔ, nhưng camera thì chưa — và ba đường
        /// vào của camera không đi qua BoardInput: lăn chuột, chuột phải, và hai ngón.
        ///
        /// Hở ba đường đó nghĩa là cuộn danh sách ở màn hình Home cũng zoom cái bảng nằm
        /// dưới. Bảng đổi mức zoom thì JewelLayer, HintLayer và BoardNumberLayer đều thấy
        /// camera đã đổi và dựng lại toàn bộ ô trong tầm nhìn — MỖI FRAME, để phục vụ một
        /// cái bảng không ai đang nhìn. Đó là cảm giác vướng khi vuốt Home.
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

            // Đổi số ngón là bỏ điểm ghim cũ, ghim lại từ đầu ở frame sau.
            //
            // Điểm ghim của hai ngón là TRUNG ĐIỂM giữa chúng, của một ngón là chính
            // ngón đó. Nhấc bớt một ngón mà giữ nguyên điểm ghim thì DragTo thấy toạ độ
            // nhảy từ trung điểm sang ngón còn lại, và kéo camera đi đúng nửa khoảng
            // cách giữa hai ngón — ngay trong một frame. Đó là cú lệch bạn thấy.
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

            // MỘT ngón: chỉ kéo khi BoardInput không nhận nét này để tô.
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

            // HAI ngón: khoảng cách đổi thì zoom, trung điểm dịch thì di chuyển.
            //
            // Nhánh này KHÔNG đi qua CanDragStroke, nên phải tự chặn UI. Thiếu chỗ này là
            // pinch trên một popup hay trên danh sách Home cũng zoom bảng ở dưới.
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

            // Lăn chuột hỏi UI ngay tại chỗ chứ không dùng cờ chốt: lăn không có lúc bấm
            // xuống nên không mở ra cử chỉ nào, mỗi nấc lăn là một sự kiện độc lập.
            //
            // Đây chính là đường làm cuộn danh sách Home vướng: cùng một cú lăn vừa cuộn
            // danh sách vừa zoom bảng ở dưới.
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI())
            {
                ApplyZoom(-scroll * ScrollZoomSpeed * _camera.orthographicSize);
            }

            // Chuột phải luôn kéo được, kể cả khi đang đứng trên ô tô được — đường thoát
            // khi bảng kín ô gợi ý mà vẫn muốn di chuyển.
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

            // Tâm dải quy ra world THEO MỨC ZOOM HIỆN TẠI, không phải mức lúc vào màn:
            // phóng sát thì khoảng lệch co lại theo, nên người chơi vẫn kéo tới được mọi
            // rìa bảng.
            var centerY = ViewCenterY(halfHeight);

            transform.position = new Vector3(
                Mathf.Clamp(position.x, -maxX, maxX),
                Mathf.Clamp(position.y, centerY - maxY, centerY + maxY),
                position.z);
        }
    }
}
