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
    /// Nhiều ngón thì nét luôn thuộc về camera, và **vẫn thuộc về camera sau khi nhấc
    /// bớt xuống còn một ngón** — người chơi zoom xong thường kéo tiếp bằng ngón còn
    /// lại. Chỉ khi nhấc hết tay ra thì nét sau mới được quyền tô.
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

        /// Chặn trên cho bán kính quét, tính bằng ô. Ở mọi mức zoom thật bán kính chỉ
        /// vài ô, nhưng một LevelConfig đặt Camera Max Size rất lớn sẽ làm ô bé xíu và
        /// vòng quét nở ra vô tội vạ.
        private const int MaxSnapReachCells = 8;

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

        [Header("Hút vào ô tô được")]
        [Tooltip("Chạm hụt thì tìm ô TÔ ĐƯỢC gần nhất quanh đó thay vì bỏ qua.\n\n" +
                 "BỎ TICK là chạy y hệt bản cũ: chỉ ô nằm đúng dưới ngón mới được tính. " +
                 "Toàn bộ phép hút nằm sau ô tick này, không tick thì không một dòng nào " +
                 "của nó chạy.\n\n" +
                 "Vì sao hút được mà không đoán mò: 'tô được' đã lọc theo màu ĐANG CHỌN và " +
                 "ô CHƯA TÔ, nên tập đích rất thưa — khác app vẽ tự do, nơi điểm nào cũng " +
                 "hợp lệ và hút thành ra đoán bừa.")]
        [SerializeField] private bool _snapToNearestPaintable;

        [Tooltip("Bán kính hút TRONG LÚC TÔ, tính bằng pixel màn hình.\n\n" +
                 "Khai bằng pixel nên đây là một khoảng cách VẬT LÝ cố định trên tay người " +
                 "chơi. Quy sang ô thì zoom càng xa bán kính theo ô càng lớn — đúng chiều " +
                 "với vấn đề, vì đó cũng là lúc ô nhỏ và khó chạm nhất. Ngược lại, phóng " +
                 "sát thì bán kính chỉ còn một phần tư ô và phép hút gần như tự tắt.\n\n" +
                 "32 là điểm khởi đầu. Màn hình càng nhiều điểm ảnh càng nên nới ra — sai " +
                 "số của đầu ngón là vài milimét, mà một milimét trên máy 400 dpi là ~16 px.")]
        [SerializeField] private float _snapRadiusPixels = 32f;

        [Tooltip("Bán kính hút LÚC ĐẶT TAY XUỐNG, tức lúc quyết nét này là tô hay kéo camera.\n\n" +
                 "Hẹp hơn bán kính khi tô, và đó là chủ ý: nét bắt đầu gần một ô tô được sẽ " +
                 "thành nét TÔ, nên để rộng quá thì ở vùng dày ô gợi ý người chơi không còn " +
                 "kéo bảng bằng một ngón được nữa. Hai ngón thì vẫn luôn là kéo và zoom.\n\n" +
                 "Để 0 là bắt chạm trúng mới bắt đầu tô được, nhưng khi đã tô thì vẫn hút.")]
        [SerializeField] private float _snapBeginRadiusPixels = 24f;

        private IPaintService _paintService;
        private Vector2Int _lastCell = NoCell;

        private Vector2Int _holdCell = NoCell;
        private Vector2 _holdStartScreen;
        private float _holdElapsed;

        /// Đã chọn màu cho nét này rồi. Giữ tiếp cũng không chọn lại — không thì cứ mỗi
        /// frame sau mốc 1.5 giây lại bắn thêm một lần SelectColor.
        private bool _holdConsumed;

        /// Nét này đã từng có từ hai ngón trở lên. Phải nhấc HẾT tay ra rồi mới nhận
        /// nét mới.
        private bool _waitingForFullRelease;

        /// Nét này đã chạm vào một ô đáng lẽ tô được nhưng chưa chọn màu. Lời nhắc chờ
        /// tới lúc NHẤC TAY mới bắn, và chỉ bắn nếu tay không hề kéo đi đâu.
        ///
        /// Bắn ngay lúc đặt tay xuống là sai: kéo bảng đi xem tranh cũng bắt đầu bằng một
        /// cú chạm vào ô nào đó, nên người chơi ăn lời nhắc mỗi lần muốn di chuyển. Chỉ
        /// một cú chạm rồi thả tại chỗ mới là "tôi định tô ô này".
        private bool _hasPendingNotify;
        private Vector2 _pendingNotifyScreen;

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

            if (!TryReadPointer(out var screenPosition, out var canPaint))
            {
                // Đã nhấc HẾT tay ra — đây mới là lúc chốt xem có nhắc hay không.
                FlushPendingNotify();

                CurrentStroke = StrokeOwner.None;
                _lastCell = NoCell;
                ResetHold();
                return;
            }

            if (!canPaint)
            {
                // Đang nhiều ngón, hoặc còn sót ngón sau một cử chỉ nhiều ngón.
                //
                // Trả về Camera chứ KHÔNG phải None: None khoá luôn cả việc kéo, và
                // người chơi vừa pinch xong nhấc bớt một ngón sẽ thấy bảng đứng đơ dưới
                // ngón còn lại. Nét này không bao giờ được tô, nhưng kéo thì vẫn được.
                CurrentStroke = StrokeOwner.Camera;
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

            if (CurrentStroke == StrokeOwner.Camera)
            {
                TickHold(screenPosition);
                TickPendingNotify(screenPosition);
            }

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

        /// Ô này sẽ tô được nếu người chơi đã chọn đúng màu của nó. Khác CanPaint ở chỗ
        /// KHÔNG xét màu đang chọn — đây đúng là câu hỏi cần đặt khi chưa chọn màu nào.
        private bool IsPaintableWhenColorChosen(Vector2Int cell)
        {
            return _paintService.SelectedPaletteIndex < 0 && HasColorToPick(cell);
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

            // Bán kính lúc BẮT ĐẦU nét hẹp hơn lúc đang tô — xem chú thích của hai ô đó
            // trong Inspector.
            if (TryResolvePaintCell(screenPosition, CellUnder(screenPosition),
                    _snapBeginRadiusPixels, out _))
            {
                return StrokeOwner.Paint;
            }

            if (!TryGetCell(screenPosition, out var cell)) return StrokeOwner.Camera;

            // Chạm trúng một ô ĐÁNG LẼ tô được mà chưa chọn màu nào: ghi nhận để nhắc,
            // nhưng chờ tới lúc nhấc tay. Im lặng hẳn thì người chơi mới vào màn cứ quẹt
            // mãi mà không hiểu vì sao không có gì xảy ra.
            if (IsPaintableWhenColorChosen(cell))
            {
                _hasPendingNotify = true;
                _pendingNotifyScreen = screenPosition;
            }

            return StrokeOwner.Camera;
        }

        /// Tay đã kéo đi quá xa thì bỏ lời nhắc: đó là ý định DI CHUYỂN bảng, không phải
        /// ý định tô. Dùng chung ngưỡng với hold-to-pick vì cùng một câu hỏi — "cú này có
        /// phải là chạm tại chỗ không".
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

            // Giữ lâu để chọn màu cũng là một nét không kéo đi đâu, nên nó cũng tới được
            // đây — nhưng lúc đó màu đã chọn xong rồi, nhắc nữa là nhắc sai.
            if (_paintService.SelectedPaletteIndex >= 0) return;

            _paintService.RequireColor();
        }

        /// Trả false khi không còn gì chạm màn hình.
        ///
        /// canPaint tách riêng khỏi việc "có con trỏ hay không" vì hai câu hỏi khác
        /// nhau. Sau một cử chỉ nhiều ngón, ngón còn sót lại vẫn là một con trỏ hợp lệ
        /// để KÉO, nhưng không được phép TÔ: người chơi nhấc bớt một ngón sau khi zoom
        /// hoàn toàn không có ý tô vào cái ô mà ngón kia tình cờ đang đứng.
        ///
        /// Chốt chỉ mở khi nhấc HẾT tay ra.
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

                // Không còn ngón nào trên màn: mở chốt cho nét sau.
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

            // Chốt theo ô DƯỚI NGÓN, không theo ô được tô.
            //
            // Kéo chậm thì một ô nằm dưới ngón nhiều frame liền — chỉ xử lý lần đầu. Từ
            // khi có phép hút còn một lý do nặng hơn: ô vừa tô xong không còn hợp lệ nữa,
            // nên nếu chốt theo ô ĐƯỢC TÔ thì frame sau phép hút đi tìm và vớ ngay ô kế
            // bên rồi tô luôn, frame sau nữa là ô kế tiếp — giữ tay yên một chỗ sẽ thấy
            // màu loang ra thành vệt.
            if (directCell == _lastCell) return;

            _lastCell = directCell;

            if (!TryResolvePaintCell(screenPosition, directCell, _snapRadiusPixels, out var target))
            {
                return;
            }

            _paintService.TryPaint(target.x, target.y);
        }

        /// Ô sẽ được tô cho điểm chạm này.
        ///
        /// KHÔNG bật hút: đúng ô dưới ngón, và chỉ khi ô đó tô được — hệt bản cũ.
        /// Bật hút: ô dưới ngón nếu tô được, không thì ô TÔ ĐƯỢC gần điểm chạm nhất
        /// trong bán kính.
        ///
        /// Chưa chọn màu nào thì CanPaint luôn trả false, nên hàm này cũng trả false và
        /// phép hút không đụng gì tới đường nhắc "hãy chọn màu" bên dưới.
        private bool TryResolvePaintCell(
            Vector2 screenPosition, Vector2Int directCell, float radiusPixels, out Vector2Int target)
        {
            target = directCell;

            // CanPaint tự kiểm biên nên ô nằm ngoài lưới cũng hỏi được.
            if (_paintService.CanPaint(directCell.x, directCell.y)) return true;

            if (!_snapToNearestPaintable) return false;

            var grid = _boardView.Grid;
            var layout = _boardView.Layout;
            if (grid == null || layout == null) return false;

            var cellPixels = BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize);
            if (cellPixels <= 0f) return false;

            // Một ô rộng đúng một world unit, nên số ô cũng chính là khoảng cách world.
            var radiusCells = Mathf.Max(0f, radiusPixels) / cellPixels;
            if (radiusCells <= 0f) return false;

            var reach = Mathf.Min(Mathf.CeilToInt(radiusCells), MaxSnapReachCells);

            var world = ScreenToWorld(screenPosition);

            // Khởi tạo bằng bình phương bán kính: vừa là mốc so sánh, vừa là hàng rào
            // TRÒN cho vùng quét — ô ở góc hình vuông duyệt nằm ngoài bán kính tự bị loại.
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

                    // Đo tới TÂM ô trong world chứ không đếm theo số ô: đếm ô thì bốn ô
                    // chéo góc cùng "cách 1 ô" với bốn ô kề cạnh, và phép hút chọn nhầm ô
                    // chéo chỉ vì nó được duyệt trước.
                    var sqr = (layout.CellToWorldCenter(x, y) - world).sqrMagnitude;

                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    target = new Vector2Int(x, y);
                    found = true;
                }
            }

            return found;
        }

        /// Ô dưới ngón, KỂ CẢ khi nó nằm ngoài lưới.
        ///
        /// Cần cho phép hút: chạm hụt ra ngoài mép bảng vẫn nên hút được vào ô sát mép,
        /// mà TryGetCell thì trả false ở đó nên bên gọi không có tâm để quét quanh.
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
