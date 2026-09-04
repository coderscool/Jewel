using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đánh dấu những ô tô được bằng màu đang chọn, bằng sprite đặt lên từng ô.
    ///
    /// Chỉ sinh marker cho ô đang lọt trong tầm nhìn camera, nên số GameObject sống
    /// cùng lúc phụ thuộc mức zoom chứ không phụ thuộc kích thước bảng.
    ///
    /// Khác JewelLayer ở một chỗ quan trọng: ngọc bị cull thì ô vẫn còn màu trong
    /// texture nên không ai nhận ra, còn marker bị cull là mất hẳn dấu hiệu. Vì vậy
    /// ngưỡng ẩn ở đây để thấp hơn hẳn — xem tooltip.
    public class HintLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _hintPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì ngừng hiện marker.\n\n" +
                 "Để THẤP vì gợi ý chủ yếu dùng lúc zoom xa để dò xem còn ô nào; đặt cao " +
                 "như lớp số sẽ làm nó biến mất đúng lúc cần nhất.\n\n" +
                 "PHẢI ngắm theo BẢNG LỚN NHẤT của game, không theo bảng đang mở. Mức zoom " +
                 "lúc vào màn là mức vừa khít cả bảng, nên bảng càng lớn thì ô chiếu xuống " +
                 "màn hình càng bé: một ô chỉ còn Screen.height / (2 * orthographicSize) " +
                 "pixel. Bảng 72x72 ở orthographicSize 70 là Screen.height / 140 — màn 1080 " +
                 "cho 7.7 pixel, màn ngắn hơn 700 tụt xuống dưới 5 và marker TẮT SẠCH ngay " +
                 "ở mức zoom mặc định. Chính ngưỡng này là thứ khiến gợi ý chỉ hiện sau khi " +
                 "phóng to.\n\n" +
                 "Càng thấp thì càng nhiều object cùng lúc — nhưng ô Prewarm From Largest " +
                 "Color bên dưới đã dựng sẵn đúng bằng trường hợp xấu nhất, nên hạ ngưỡng " +
                 "không làm phát sinh Instantiate lúc chơi.")]
        [SerializeField] private float _minCellScreenPixels = 2f;

        [Tooltip("Số marker dựng sẵn lúc vào màn. Chọn màu là lúc duy nhất sinh hàng " +
                 "loạt object cùng lúc — dựng sẵn thì cú đó chỉ là lấy đồ khỏi kho.\n\n" +
                 "Chỉ là mức SÀN nếu ô dưới được tick.")]
        [SerializeField] private int _prewarmCount = 400;

        [Tooltip("Dựng sẵn đủ marker cho MÀU NHIỀU Ô NHẤT của màn, thay vì một con số " +
                 "cố định.\n\n" +
                 "Đây đúng là số marker tối đa cần tới: chọn màu nào thì chỉ ô của màu đó " +
                 "mới có marker, nên màu đông ô nhất chính là trường hợp xấu nhất. Nhờ vậy " +
                 "bảng lớn tới đâu kho cũng không bao giờ thiếu, khỏi chỉnh tay từng màn.")]
        [SerializeField] private bool _prewarmFromLargestColor = true;

        [Tooltip("Dựng sẵn tối đa bao nhiêu marker trong MỘT frame. Để 0 là dựng hết " +
                 "trong một frame như trước.")]
        [SerializeField] private int _prewarmPerFrame = 200;

        [Tooltip("Cắt bớt lượng dựng sẵn theo SỨC CHỨA CỦA MÀN HÌNH.\n\n" +
                 "Số marker cần cùng lúc bị chặn bởi HAI thứ, không phải một: số ô của " +
                 "lưới, VÀ vùng mà khung nhìn phủ được ở mức zoom rộng nhất mà lớp này còn " +
                 "sống (xem Min Cell Screen Pixels). Sức chứa của màn là hằng số, còn số ô " +
                 "thì tăng theo BÌNH PHƯƠNG cạnh — nên bảng càng lớn, phần dựng thừa càng " +
                 "lớn. Bảng 72 lên 108 là số ô gấp 2.25 lần mà màn hình vẫn thế.\n\n" +
                 "Trên màn hình rất cao thì cận thứ hai có thể vẫn phủ trọn bảng và không " +
                 "cắt được gì. Điều đó ĐÚNG: ở máy đó cả bảng hiện thật, không phải chỗ " +
                 "này tính hụt.\n\n" +
                 "Bỏ tick là quay lại dựng đủ cho mọi ô.")]
        [SerializeField] private bool _capPrewarmToScreen = true;

        [Tooltip("Hệ số an toàn nhân vào sức chứa màn hình khi cắt. Thiếu thì kho phải " +
                 "Instantiate bù ngay giữa lúc zoom — đúng cú khựng mà việc dựng sẵn sinh " +
                 "ra để tránh.")]
        [Range(1f, 3f)]
        [SerializeField] private float _prewarmScreenSafety = 1.3f;

        [Tooltip("Số marker được sinh tối đa trong MỘT frame.\n\n" +
                 "**Để 0 là tất cả hiện cùng một lúc** — đây là mặc định, và nó an toàn vì " +
                 "kho đã dựng sẵn đủ hàng: lấy ra chỉ là bật object và đặt vị trí.\n\n" +
                 "Đặt số dương chỉ cần khi kho có thể thiếu và phải Instantiate bù.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn, để ô ở rìa " +
                 "không bị thu về rồi sinh lại liên tục khi camera nhích.")]
        [SerializeField] private int _visibleMarginCells = 2;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();
        private readonly Dictionary<int, int> _colorCounts = new();

        /// Lượt dựng sẵn đang chạy. Vào màn mới giữa chừng thì huỷ lượt cũ.
        private Coroutine _prewarmRoutine;

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;
        private bool _needsRefresh;

        /// Đã báo một lần cho màn này rằng mức zoom đang chặn marker.
        private bool _reportedZoomGate;

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _paintService.OnColorSelected += HandleColorSelected;

            // Gỡ marker lúc viên ngọc ĐÁP XUỐNG, không phải lúc bấm tô — gỡ sớm thì
            // ô trống trơn suốt quãng viên đang bay.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            if (_paintService == null) return;

            _paintService.OnColorSelected -= HandleColorSelected;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            StartPrewarm();

            _lastOrthographicSize = -1f;
            _reportedZoomGate = false;
        }

        private void HandleColorSelected(int paletteIndex)
        {
            ReleaseAll();
            _needsRefresh = true;
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex) => Release(cell);

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
        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Hints.Refresh");

        /// true khi đã phủ hết ô trong tầm nhìn; false khi hết hạn mức sinh của frame này.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            var selected = _paintService.SelectedPaletteIndex;
            if (selected < 0)
            {
                ReleaseAll();
                return true;
            }

            var cellPixels = CellScreenPixels();

            if (cellPixels < _minCellScreenPixels)
            {
                ReportZoomGate(cellPixels);
                ReleaseAll();
                return true;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            if (layout == null || grid == null) return true;

            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            // 0 nghĩa là không giới hạn — marker lấy từ kho dựng sẵn nên rẻ, không cần
            // chia frame. int.MaxValue thay vì rẽ nhánh riêng: lưới lớn nhất cũng chỉ
            // vài nghìn ô nên phép trừ không bao giờ chạm đáy.
            var budget = _maxSpawnPerFrame > 0 ? _maxSpawnPerFrame : int.MaxValue;

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;

                    if (grid.GetCell(x, y) != selected) continue;
                    if (IsDone(cell)) continue;

                    Show(cell);

                    if (--budget <= 0) return false;
                }
            }

            return true;
        }

        /// Marker biến mất mà không nói gì là kiểu hỏng khó lần nhất: người ta đi kiểm
        /// prefab, kiểm sorting order, kiểm cả luật chọn màu, trong khi thủ phạm chỉ là một
        /// con số trong Inspector.
        ///
        /// Bảng càng LỚN càng dễ dính, và đó là lý do lỗi chỉ hiện ra ở màn to nhất: mức
        /// zoom lúc vào màn phải kéo đủ xa để thấy trọn bảng, nên ô chiếu xuống màn hình
        /// càng bé. Bảng 32x32 ở mức vừa khít cho chừng 19 pixel mỗi ô, bảng 72x72 chỉ còn
        /// chừng 8 — cùng một ngưỡng, một bên qua một bên không.
        ///
        /// Báo đúng một lần mỗi màn: kéo zoom qua lại quanh ngưỡng sẽ in liên tục.
        private void ReportZoomGate(float cellPixels)
        {
            if (_reportedZoomGate) return;

            _reportedZoomGate = true;

            Debug.Log(
                $"[HintLayer] Mức zoom hiện tại cho {cellPixels:0.0} pixel mỗi ô, dưới ngưỡng " +
                $"Min Cell Screen Pixels = {_minCellScreenPixels} nên marker gợi ý bị ẩn — " +
                "phải phóng to mới thấy. " +
                $"(Screen.height = {Screen.height}, orthographicSize = {_camera.orthographicSize:0.##}, " +
                $"bảng {_boardView.Layout.Width}x{_boardView.Layout.Height}). " +
                "Hạ ngưỡng xuống nếu muốn thấy gợi ý ngay ở mức zoom này.", this);
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

        /// Ô đã tô nhưng viên ngọc còn đang bay thì vẫn coi là CHƯA xong — giữ marker
        /// tới lúc viên đáp xuống. Không có chỗ này thì chỉ cần kéo camera giữa lúc bay
        /// là marker biến mất sớm, đúng cái lỗi vừa sửa nhưng đi đường khác.
        private bool IsDone(Vector2Int cell)
        {
            if (!_paintService.IsPainted(cell.x, cell.y)) return false;

            return _flyEffect == null || !_flyEffect.IsInFlight(cell);
        }

        private void Show(Vector2Int cell)
        {
            var marker = Rent();
            if (marker == null) return;

            marker.transform.position = _boardView.Layout.CellToWorldCenter(cell.x, cell.y);

            _active[cell] = marker;
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

        /// Tắt RENDERER chứ không tắt GameObject — xem chú thích cùng chỗ ở JewelLayer.
        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var marker)) return;

            marker.enabled = false;
            _pool.Push(marker);
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

            if (_hintPrefab != null) return Instantiate(_hintPrefab, _root);

            Debug.LogWarning($"{nameof(HintLayer)} chưa gán Hint Prefab — không có dấu hiệu nào hiện ra.");
            return null;
        }

        /// Dựng sẵn TRẢI RA nhiều frame thay vì dồn hết vào frame vào màn.
        ///
        /// Vắt cạn ngay tại chỗ nếu object đang tắt: coroutine không chạy được lúc đó,
        /// mà thà khựng một nhịp còn hơn vào màn thiếu đồ dựng sẵn.
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
            if (_hintPrefab == null) yield break;

            // Nhường một frame TRƯỚC khi tính trần — xem chú thích cùng chỗ ở JewelLayer.
            yield return null;

            var target = Mathf.Max(_prewarmCount, ResolvePrewarmTarget());
            var perFrame = _prewarmPerFrame > 0 ? _prewarmPerFrame : int.MaxValue;
            var budget = perFrame;

            while (_pool.Count < target)
            {
                var marker = Instantiate(_hintPrefab, _root);
                marker.enabled = false;
                _pool.Push(marker);

                if (--budget > 0) continue;

                budget = perFrame;
                yield return null;
            }

            _prewarmRoutine = null;
        }

        /// Số marker cần dựng sẵn: số ô của màu đông nhất, đã cắt theo sức chứa màn hình.
        ///
        /// Cắt theo TỈ LỆ chứ không cắt phẳng: ở mức zoom mà marker còn sống, vùng nhìn
        /// thấy là một mẫu khá đều của cả bức tranh, nên màu chiếm nửa tranh thì trong
        /// khung hình cũng chiếm chừng nửa số ô.
        private int ResolvePrewarmTarget()
        {
            var largest = LargestColorCellCount();

            if (!_capPrewarmToScreen || largest <= 0) return largest;

            // _colorCounts vừa được LargestColorCellCount điền lại, dùng luôn khỏi quét lần hai.
            var colored = 0;
            foreach (var pair in _colorCounts) colored += pair.Value;

            if (colored <= 0) return largest;

            var capacity = VisibleCellCapacity();
            if (capacity <= 0f) return largest;

            var ratio = Mathf.Clamp01(capacity * Mathf.Max(1f, _prewarmScreenSafety) / colored);

            return Mathf.CeilToInt(largest * ratio);
        }

        /// Số ô nhiều nhất lọt vào khung nhìn, ở mức zoom RỘNG NHẤT mà lớp này còn sống.
        ///
        /// Hai cận, lấy cái chặt hơn: kéo ra quá Min Cell Screen Pixels là cả lớp bị thu
        /// về hết, mà BoardCamera cũng không cho kéo xa hơn mức lúc vào màn.
        ///
        /// 0 khi chưa dựng bảng — bên gọi hiểu là "không cắt".
        private float VisibleCellCapacity()
        {
            var layout = _boardView != null ? _boardView.Layout : null;
            if (layout == null || _camera == null) return 0f;

            var threshold = Mathf.Max(0.01f, _minCellScreenPixels);
            var widest = Mathf.Min(_camera.orthographicSize, Screen.height / (2f * threshold));

            // Cộng phần nới của ExpandedCameraRect, và kẹp theo cạnh bảng vì VisibleCells
            // cũng kẹp như vậy.
            var margin = 2f * Mathf.Max(0, _visibleMarginCells) + 1f;

            return Mathf.Min(layout.Width, 2f * widest * _camera.aspect + margin) *
                   Mathf.Min(layout.Height, 2f * widest + margin);
        }

        /// Số ô của màu chiếm nhiều ô nhất trong màn — cận trên tuyệt đối của số marker
        /// cần tới cùng lúc.
        ///
        /// Quét cả lưới một lượt. Chạy đúng một lần mỗi khi vào màn, và lúc đó màn hình
        /// chờ đang che, nên vài nghìn phép tra dictionary không ai thấy.
        private int LargestColorCellCount()
        {
            if (!_prewarmFromLargestColor) return 0;

            var grid = _boardView != null ? _boardView.Grid : null;
            if (grid == null) return 0;

            _colorCounts.Clear();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;

                    _colorCounts.TryGetValue(index, out var count);
                    _colorCounts[index] = count + 1;
                }
            }

            var largest = 0;

            foreach (var pair in _colorCounts)
            {
                if (pair.Value > largest) largest = pair.Value;
            }

            return largest;
        }
    }
}
