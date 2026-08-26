using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đặt viên ngọc lên những ô đã tô.
    ///
    /// Chỉ sinh ngọc cho ô đang lọt trong tầm nhìn camera, nên số GameObject sống
    /// cùng lúc phụ thuộc mức zoom chứ KHÔNG phụ thuộc số ô đã tô. Bảng tô kín 4000 ô
    /// vẫn chỉ có chừng trăm viên tồn tại.
    ///
    /// Cull được là nhờ BoardView đã ghi màu vào texture: viên ngọc bị gỡ thì ô đó
    /// vẫn còn nguyên màu bên dưới, người chơi không nhận ra có gì biến mất.
    public class JewelLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì không sinh ngọc — " +
                 "ở cỡ đó viên ngọc bé hơn hạt gạo mà vẫn tốn một GameObject. " +
                 "Màu của ô vẫn hiện vì nó nằm trong texture của bảng.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        [Tooltip("Số ngọc dựng sẵn lúc vào màn. Chỉ là mức SÀN nếu ô dưới được tick.")]
        [SerializeField] private int _prewarmCount = 200;

        [Tooltip("Dựng sẵn đủ ngọc cho MỌI Ô CÓ MÀU của màn, thay vì một con số cố định.\n\n" +
                 "Đây đúng là trường hợp xấu nhất: tranh tô kín và kéo ra thấy trọn bảng " +
                 "thì mọi ô đều cần một viên. Dựng sẵn từng đó thì zoom nhanh cỡ nào kho " +
                 "cũng không phải Instantiate bù.\n\n" +
                 "Cái giá là bộ nhớ và thời gian vào màn: bảng 64x64 là 4096 SpriteRenderer, " +
                 "dựng mất cỡ 80ms. Việc đó chạy lúc màn hình chờ đang che.")]
        [SerializeField] private bool _prewarmFromBoardSize = true;

        [Tooltip("Số ngọc được sinh tối đa trong MỘT frame.\n\n" +
                 "**Để 0 là tất cả hiện cùng một lúc** — đây là mặc định. Zoom ra nhanh " +
                 "thì cả vùng mới hiện trọn ngay, không thấy ngọc lần lượt mọc lên.\n\n" +
                 "An toàn khi kho đã dựng sẵn đủ: lấy ra chỉ là bật object, đặt vị trí và " +
                 "gán màu.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn, để ô ở rìa " +
                 "không bị thu về rồi sinh lại liên tục khi camera nhích.")]
        [SerializeField] private int _visibleMarginCells = 2;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();

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

            // Nghe lúc viên bay ĐÁP XUỐNG, không phải lúc ô được tô — nhờ vậy ngọc chỉ
            // hiện khi hiệu ứng kết thúc. JewelFlyEffect luôn bắn sự kiện này kể cả khi
            // không bay được, nên không có ô nào bị bỏ quên.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            Prewarm();

            _lastOrthographicSize = -1f;   // ép tính lại ở LateUpdate kế tiếp
        }

        /// Viên bay vừa đáp xuống: hiện ngọc ngay nếu ô đó đang trong tầm nhìn,
        /// không đợi camera động.
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
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

            // Còn việc dở từ frame trước thì làm tiếp, kể cả khi camera đã đứng yên.
            if (!_needsRefresh) return;

            _needsRefresh = !Refresh();
        }

        private bool HasCameraChanged()
        {
            if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

            return _lastCameraPosition != _camera.transform.position;
        }

        /// Nhãn đo cho Profiler. Không có nhãn thì cả ba lớp đều nằm lẫn trong
        /// LateUpdate và không tách được lớp nào tốn bao nhiêu.
        ///
        /// static readonly để tên chỉ được cấp phát một lần cho cả chương trình.
        /// Bản build phát hành không bật ENABLE_PROFILER thì nhãn tự tiêu biến.
        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Jewels.Refresh");

        /// true khi đã phủ hết ô trong tầm nhìn; false khi hết hạn mức sinh của frame này.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            if (CellScreenPixels() < _minCellScreenPixels)
            {
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            // 0 nghĩa là không giới hạn — ngọc lấy từ kho dựng sẵn nên rẻ, không cần
            // chia frame. int.MaxValue thay vì rẽ nhánh riêng: lưới lớn nhất cũng chỉ
            // vài nghìn ô nên phép trừ không bao giờ chạm đáy.
            var budget = _maxSpawnPerFrame > 0 ? _maxSpawnPerFrame : int.MaxValue;

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;
                    if (!_paintService.IsPainted(x, y)) continue;

                    // Ô đang có viên bay tới thì để hiệu ứng lo, đừng hiện trước.
                    if (_flyEffect != null && _flyEffect.IsInFlight(cell)) continue;

                    Show(cell, grid.GetCell(x, y));

                    if (--budget <= 0) return false;
                }
            }

            return true;
        }

        /// Nới rộng tầm nhìn thêm vài ô để camera nhích một chút không làm ô ở rìa bị
        /// thu về rồi sinh lại liên tục.
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

        /// Tắt RENDERER chứ không tắt GameObject.
        ///
        /// SetActive phải duyệt cả cây con, gửi thông điệp vòng đời và cập nhật lại cấu
        /// trúc culling — đắt gấp nhiều lần so với gạt một cờ bool. Zoom qua lại liên
        /// tục là hàng trăm lần bật tắt mỗi frame, và đó chính là chỗ khung hình tụt.
        ///
        /// Object chỉ có đúng một SpriteRenderer nên tắt renderer với tắt object là một
        /// về mặt hình ảnh.
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

        /// Dựng sẵn lúc vào màn, lúc màn hình chờ đang che nên không ai thấy.
        private void Prewarm()
        {
            if (_jewelPrefab == null) return;

            var target = Mathf.Max(_prewarmCount, ColoredCellCount());

            while (_pool.Count < target)
            {
                var jewel = Instantiate(_jewelPrefab, _root);
                jewel.enabled = false;
                _pool.Push(jewel);
            }
        }

        /// Số ô CÓ MÀU của màn — cũng chính là số viên ngọc tối đa có thể cần cùng lúc,
        /// khi tranh đã tô kín và người chơi kéo ra thấy trọn bảng.
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
