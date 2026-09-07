using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Hiện chỉ số bảng màu lên từng ô.
    ///
    /// Chỉ sinh TextMeshPro cho ô đang lọt tầm nhìn camera, và chỉ tính lại khi
    /// camera đổi — không phải mỗi frame. Ô nhỏ hơn ngưỡng đọc được thì ẩn hết số.
    public class BoardNumberLayer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private TextMeshPro _numberPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Số hiện ra ở đâu trong dải zoom CỦA MÀN ĐÓ: 0 là ngay khi vào màn " +
                 "(Camera Max Size), 1 là mãi tới lúc lớp màu tan hết (Fade Switch Size). " +
                 "0.6 là đi được 60% quãng đường giữa hai mốc. HẠ xuống thì số hiện sớm hơn. " +
                 "Chỉ có tác dụng khi LevelConfig có điền Fade Switch Size.")]
        [Range(0f, 1f)]
        [SerializeField] private float _showAtZoomProgress = 0.2f;

        [Tooltip("Đường lui khi LevelConfig để trống Fade Switch Size: ô chiếu lên màn hình " +
                 "nhỏ hơn ngần này pixel thì không hiện số.")]
        [SerializeField] private float _minCellScreenPixels = 32f;

        [Tooltip("Màu chữ số, dùng chung cho mọi ô. Số chỉ thật sự đọc được khi lớp màu " +
                 "đã mờ đi, lúc đó nền sau nó gần như trong suốt nên một màu cố định là đủ.")]
        [SerializeField] private Color _numberColor = Color.black;

        [Tooltip("Số chữ được LẤY RA tối đa trong một frame. Không phải số chữ hiện ra — " +
                 "chữ lấy ra vẫn tắt renderer cho tới khi cả vùng nhìn xong, rồi tất cả " +
                 "bật cùng lúc.\n\n" +
                 "Từ khi Prewarm From Board Size dựng đủ chữ cho cả lưới, lấy ra chỉ còn là " +
                 "đặt vị trí — rẻ hơn hẳn thời còn phải Instantiate. Nâng con số này lên " +
                 "vài trăm là an toàn, và số sẽ hiện gần như tức thì thay vì bò dần ra.")]
        [SerializeField] private int _maxSpawnPerFrame = 400;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn. Ô ở rìa không " +
                 "bị thu về rồi sinh lại liên tục mỗi khi camera nhích một chút.")]
        [SerializeField] private int _visibleMarginCells = 2;

        [Tooltip("Dải trễ quanh ngưỡng hiện số, tính theo phần của KHOẢNG từ ngưỡng tới " +
                 "mức zoom xa nhất. 0.08 nghĩa là đã hiện rồi thì phải kéo ra thêm 8% quãng " +
                 "đó mới tắt.\n\n" +
                 "Không có dải này thì zoom qua lại quanh đúng ngưỡng là cả nghìn chữ bị " +
                 "tắt rồi bật lại mỗi frame — cú giật nặng nhất khi zoom liên tục. " +
                 "Để 0 là tắt dải trễ.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _showHysteresis = 0.08f;

        [Header("Thử nghiệm")]
        [Tooltip("Bỏ qua Max Spawn Per Frame: dựng TẤT CẢ số trong tầm nhìn trong đúng " +
                 "một frame.\n\n" +
                 "Bật lên để đo xem máy chịu được tới đâu. Số hiện ngay lập tức, không có " +
                 "0.3 giây chờ — đổi lại nếu máy yếu thì sẽ thấy một cú khựng đúng lúc " +
                 "zoom qua ngưỡng hiện số.\n\n" +
                 "Đổi được ngay trong Play Mode: tick rồi zoom ra zoom vào là thấy khác " +
                 "liền, không cần chạy lại.")]
        [SerializeField] private bool _spawnAllInOneFrame;

        [Tooltip("Dựng sẵn bao nhiêu chữ cho MỖI SỐ lúc vào màn. Việc này chạy lúc màn " +
                 "hình chờ đang che, nên người chơi không thấy.\n\n" +
                 "Chỉ là mức SÀN nếu ô dưới được tick. Để 0 và bỏ tick ô dưới là tắt hẳn, " +
                 "lúc đó lần zoom đầu vẫn phải dựng chữ.")]
        [SerializeField] private int _prewarmPerNumber = 64;

        [Tooltip("Dựng sẵn ĐÚNG số ô mang mỗi con số trong lưới, thay vì một con số cố định.\n\n" +
                 "Ở mức zoom mà số hiện ra thì gần như cả bảng nằm trong khung hình, nên " +
                 "số chữ cần cùng lúc CHÍNH LÀ số ô của lưới. Một con số cứng không biết " +
                 "điều đó: bảng 27x36 còn tạm đủ, sang 32x45 là thiếu hàng trăm chữ và " +
                 "chúng phải Instantiate ngay giữa lúc đang zoom — đó là cú khựng bạn thấy.\n\n" +
                 "Đổi lại thời gian vào màn dài thêm chút, nhưng lúc đó màn hình chờ đang che.")]
        [SerializeField] private bool _prewarmFromBoardSize = true;

        [Tooltip("Dựng sẵn tối đa bao nhiêu chữ trong MỘT frame.\n\n" +
                 "Đây là thứ giữ cho lúc vào màn không đứng hình. Bảng 67x68 cần ~4.600 " +
                 "chữ; dồn hết vào một frame là Instantiate cộng ForceMeshUpdate ngần ấy " +
                 "lần liên tiếp — trên máy yếu mất cả giây, và đó đúng là cú đơ khi vào màn.\n\n" +
                 "250 thì 4.600 chữ trải ra ~19 frame, tức khoảng 0.3 giây — nằm gọn trong " +
                 "khoảng màn hình chờ đang che.\n\n" +
                 "Để 0 là quay lại dựng hết trong một frame.")]
        [SerializeField] private int _prewarmPerFrame = 250;

        [Tooltip("Cắt bớt lượng dựng sẵn theo SỨC CHỨA CỦA MÀN HÌNH.\n\n" +
                 "Số chữ cần cùng lúc bị chặn bởi màn hình chứ không bởi cỡ bảng: ở mức " +
                 "zoom mà số bắt đầu hiện, khung nhìn chỉ phủ được một vùng nhất định. " +
                 "Bảng càng lớn thì phần dựng thừa càng lớn, vì sức chứa của màn là hằng " +
                 "số còn số ô thì tăng theo bình phương cạnh.\n\n" +
                 "Bỏ tick là quay lại dựng đủ một chữ cho MỌI ô có màu.")]
        [SerializeField] private bool _capPrewarmToScreen = true;

        [Tooltip("Hệ số an toàn nhân vào sức chứa màn hình khi cắt.\n\n" +
                 "Cần vì phép cắt chia đều theo tỉ lệ ô của từng số, mà một màu dồn cục " +
                 "vào một góc thì lúc người chơi zoom đúng vào góc đó sẽ cần nhiều chữ " +
                 "của số ấy hơn phần chia. Thiếu thì kho phải Instantiate bù ngay giữa " +
                 "lúc zoom — đúng cú khựng mà việc dựng sẵn sinh ra để tránh.\n\n" +
                 "1.3 là điểm khởi đầu. Thấy khựng lúc zoom qua ngưỡng hiện số thì nâng lên.")]
        [Range(1f, 3f)]
        [SerializeField] private float _prewarmScreenSafety = 1.3f;

        /// Một chữ, kèm SỐ nó đang mang và renderer của nó.
        ///
        /// Nhớ con số vì lúc trả về kho cần biết trả vào ngăn nào — mà lúc đó lưới có thể
        /// đã đổi sang màn khác. Nhớ renderer để khỏi GetComponent mỗi lần bật tắt: một
        /// lượt zoom đụng tới cả nghìn chữ.
        private struct Label
        {
            public TextMeshPro Text;
            public MeshRenderer Renderer;
            public int Number;
        }

        private readonly Dictionary<Vector2Int, Label> _active = new();

        /// Kho chia theo TỪNG SỐ, không dùng chung một ngăn.
        ///
        /// Đây là chỗ tiết kiệm lớn nhất. SetText buộc TextMeshPro dựng lại lưới chữ —
        /// đó chính là thứ gây khựng. Kho dùng chung thì chữ lấy ra gần như luôn mang
        /// sai số nên lần nào cũng phải dựng lại. Chia theo số thì chữ "3" lấy ra đã
        /// sẵn là "3": chỉ cần đặt vị trí và bật lên, không đụng tới lưới chữ.
        private readonly Dictionary<int, Stack<Label>> _poolByNumber = new();

        private readonly List<Vector2Int> _toRelease = new();

        /// Số ô mang mỗi con số trong lưới màn hiện tại. Dùng để dựng sẵn đúng lượng cần.
        private readonly Dictionary<int, int> _cellCountByNumber = new();

        /// Danh sách các số cần dựng, tách ra khỏi dictionary ở trên.
        ///
        /// Vòng dựng sẵn giờ chạy qua nhiều frame, mà duyệt thẳng dictionary thì chỉ cần
        /// một chỗ khác chạm vào nó giữa hai frame là cả vòng lặp ném lỗi.
        private readonly List<int> _prewarmNumbers = new();

        /// Lượt dựng sẵn đang chạy. Vào màn mới giữa chừng thì huỷ lượt cũ.
        private Coroutine _prewarmRoutine;

        private BoardView _boardView;
        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;

        /// Mức zoom lúc mới vào màn, tức mức xa nhất — mốc trên của dải zoom.
        /// Đọc từ camera chứ không từ LevelConfig.CameraMaxSize, vì ô đó có thể để 0
        /// và BoardCamera tự tính; lúc ấy LevelConfig không biết con số thật.
        private float _baseSize = -1f;

        private bool _needsBaseCapture = true;
        private bool _needsRefresh;

        /// Có chữ nào đã đặt xong vị trí nhưng còn tắt renderer, chờ bật lên cùng lượt.
        private bool _hasHiddenLabels;

        /// Prefab sai kiểu — lớp số tự tắt hẳn thay vì ném lỗi mỗi frame.
        private bool _prefabRejected;

        /// Lần xét gần nhất kết luận là ĐANG hiện số. Dải trễ cần biết trạng thái cũ để
        /// nới ngưỡng theo đúng chiều.
        private bool _numbersShown;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _boardView.OnCoverChanged += HandleCoverChanged;
        }

        private void OnDestroy()
        {
            if (_boardView == null) return;

            _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            _boardView.OnCoverChanged -= HandleCoverChanged;
        }

        /// Xem chú thích cùng tên ở JewelLayer.
        private void HandleCoverChanged() => _needsRefresh = true;

        /// Chặn prefab sai NGAY tại cửa, một lần cho cả màn.
        ///
        /// Nhờ cửa này mà Release và RevealAll — hai hàm chạy cả nghìn lượt mỗi frame lúc
        /// zoom — được phép coi Renderer là luôn có, khỏi kiểm null. Kiểm null trên
        /// UnityEngine.Object là một lần gọi xuống engine hỏi object còn sống không, đắt
        /// hơn hẳn so sánh tham chiếu thường.
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
            _lastOrthographicSize = -1f;   // ép tính lại ở LateUpdate kế tiếp
        }

        /// Dựng sẵn chữ cho mọi số màn này dùng, lúc màn hình chờ đang che.
        ///
        /// Chỉ dựng cho số THẬT SỰ có trong lưới: bảng màu có thể khai 16 màu mà ảnh
        /// chỉ dùng 9, dựng cả 16 là phí một phần ba số chữ.
        ///
        /// Lượng dựng cho mỗi số lấy từ chính lưới, không phải một hằng số. Ở mức zoom mà
        /// số hiện ra thì gần như cả bảng nằm trong khung hình, nên số ô của một con số
        /// CHÍNH LÀ số chữ cần cùng lúc cho con số đó. Dùng hằng số thì bảng càng lớn
        /// càng thiếu, mà phần thiếu phải Instantiate ngay giữa lúc người chơi đang zoom.
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
            if (_numberPrefab == null) yield break;

            var grid = _boardView.Grid;
            var colors = _boardView.Colors;

            if (grid == null || colors == null) yield break;

            CountCellsByNumber(grid, colors.Count);

            _prewarmNumbers.Clear();
            foreach (var pair in _cellCountByNumber) _prewarmNumbers.Add(pair.Key);

            // Nhường một frame TRƯỚC khi tính trần. ResolveShowSize đọc _baseSize, mà
            // _baseSize chỉ được chụp ở LateUpdate đầu tiên sau khi bảng dựng xong — hỏi
            // sớm hơn thì nó còn là -1 và cái trần tính ra vô nghĩa.
            yield return null;

            var ratio = ResolvePrewarmRatio(grid);

            var perFrame = _prewarmPerFrame > 0 ? _prewarmPerFrame : int.MaxValue;
            var budget = perFrame;

            foreach (var number in _prewarmNumbers)
            {
                // Con số cứng thành mức SÀN, để vẫn chỉnh tay lên được nếu cần.
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

        /// Tỉ lệ cắt cho lượng chữ dựng sẵn — 1 nghĩa là không cắt.
        ///
        /// Chia đều theo tỉ lệ số ô của từng con số, không cắt phẳng mỗi số một lượng
        /// bằng nhau: ở mức zoom mà số hiện ra, vùng nhìn thấy là một mẫu khá đều của cả
        /// bức tranh, nên màu chiếm nửa tranh thì cũng cần chừng nửa số chữ.
        ///
        /// Sàn _prewarmPerNumber vẫn giữ nguyên, nên màu hiếm không bị cắt về gần 0.
        private float ResolvePrewarmRatio(PixelGrid grid)
        {
            if (!_capPrewarmToScreen || !_prewarmFromBoardSize) return 1f;

            var showSize = ResolveShowSize();
            if (showSize <= 0f) return 1f;

            // Mức zoom RỘNG NHẤT mà chữ còn sống: ngưỡng đã nới thêm dải trễ. Lấy ngay
            // ngưỡng thì hụt, vì đã hiện rồi người chơi còn kéo ra được thêm một quãng
            // nữa mà chữ chưa tắt.
            var ceiling = Mathf.Max(showSize, _baseSize);
            var widest = Mathf.Lerp(showSize, ceiling, Mathf.Clamp01(_showHysteresis));

            // Khung nhìn quy ra ô — một ô rộng đúng một world unit. Cộng phần nới của
            // ExpandedCameraRect, và kẹp theo cạnh bảng vì VisibleCells cũng kẹp như vậy.
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
                // Lấy ở LateUpdate chứ không trong handler, để BoardCamera kịp đặt lại
                // mức zoom cho bảng mới. Không phụ thuộc thứ tự đăng ký event.
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
        private static readonly ProfilerMarker RefreshMarker = new("JewelPainter.Numbers.Refresh");

        /// true khi đã phủ hết ô trong tầm nhìn; false khi hết hạn mức sinh của frame này
        /// và còn việc dở, lúc đó LateUpdate sẽ gọi lại ở frame sau.
        ///
        /// Chữ lấy ra còn tắt renderer, chỉ bật lên khi cả lượt đã xong. Người chơi thấy
        /// TOÀN BỘ số trong tầm nhìn hiện cùng một lúc, thay vì thấy chúng bò dần từ trên
        /// xuống theo hạn mức mỗi frame.
        ///
        /// Hạn mức giờ chỉ còn là van an toàn. Việc nặng — Instantiate và dựng lưới chữ —
        /// đã dời hết sang Prewarm, nên vòng này chỉ đặt vị trí và gạt một cờ bool.
        private bool Refresh()
        {
            using var _ = RefreshMarker.Auto();

            // Bị che thì thu hết về kho — xem chú thích cùng chỗ ở JewelLayer.
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

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            var colors = _boardView.Colors;
            var visible = layout.VisibleCells(ExpandedCameraRect());

            ReleaseOutside(visible);

            // int.MaxValue thay vì rẽ nhánh riêng: lưới lớn nhất cũng chỉ vài nghìn ô
            // nên phép trừ không bao giờ chạm đáy, và phần thân vòng lặp giữ nguyên một
            // đường chạy duy nhất cho cả hai chế độ.
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

                    var number = index + 1;
                    var label = Rent(number);

                    // KHÔNG gọi SetText ở đây: chữ lấy ra từ ngăn của số này đã mang sẵn
                    // đúng nội dung. Đây là toàn bộ lý do kho được chia theo số.
                    label.Text.transform.position = layout.CellToWorldCenter(x, y);

                    _active[cell] = label;
                    _hasHiddenLabels = true;

                    // Duyệt lại từ đầu ở frame sau; ô đã có thì bỏ qua ngay bằng một phép
                    // tra dictionary, rẻ hơn nhiều so với Instantiate nên không đáng lo.
                    if (--budget <= 0) return false;
                }
            }

            RevealAll();
            return true;
        }

        /// Bật hết chữ đang ẩn lên cùng một lượt.
        ///
        /// Duyệt _active chứ không giữ danh sách riêng: chữ có thể bị Release trả về kho
        /// giữa chừng, mà một danh sách riêng sẽ còn giữ tham chiếu tới nó và bật sáng
        /// nhầm một chữ đã được dùng lại cho ô khác.
        ///
        /// Gạt renderer chứ không đổi alpha. Đặt alpha buộc TextMeshPro ghi lại màu đỉnh
        /// và xếp lưới chữ vào hàng dựng lại — làm thế với cả nghìn chữ trong một frame
        /// là đúng cú khựng mà hàm này sinh ra để tránh.
        private void RevealAll()
        {
            if (!_hasHiddenLabels) return;

            _hasHiddenLabels = false;

            foreach (var entry in _active.Values)
            {
                entry.Renderer.enabled = true;
            }
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

        /// Có hiện số ở mức zoom hiện tại không, KÈM DẢI TRỄ.
        ///
        /// Không có dải trễ thì zoom qua lại quanh đúng ngưỡng là cả nghìn chữ bị tắt
        /// rồi bật lại mỗi frame, kéo theo ngần ấy lần vào ra dictionary và gạt renderer.
        /// Đó là cú giật nặng nhất khi zoom liên tục.
        ///
        /// Đã hiện thì phải kéo ra QUÁ ngưỡng thêm một khoảng mới tắt. Nhờ vậy dao động
        /// nhỏ quanh ngưỡng không kích hoạt lần bật tắt nào.
        private bool ShouldShowNumbers()
        {
            var threshold = ResolveShowSize();
            if (threshold <= 0f) return false;

            // Nới ngưỡng ra khi đang hiện, giữ nguyên khi đang ẩn.
            //
            // Nới theo KHOẢNG CÒN LẠI tới mức zoom xa nhất, không nhân theo tỉ lệ của
            // chính ngưỡng. Nhân tỉ lệ thì dải trễ dễ vượt quá mức xa nhất camera có thể
            // tới — ví dụ ngưỡng 33.6 nhân 8% ra 36.29, mà camera chỉ kéo ra được tới 36,
            // nên số đã hiện là không bao giờ ẩn lại được nữa.
            var ceiling = Mathf.Max(threshold, _baseSize);
            var limit = _numbersShown
                ? Mathf.Lerp(threshold, ceiling, Mathf.Clamp01(_showHysteresis))
                : threshold;

            _numbersShown = _camera.orthographicSize <= limit;

            return _numbersShown;
        }

        /// orthographicSize mà tại đó số bắt đầu hiện.
        ///
        /// Tính theo dải zoom của màn: từ mức lúc vào màn xuống tới mức lớp màu tan hết.
        /// Nhờ đo tương đối nên màn nào cũng cho cảm giác như nhau — ngưỡng pixel cố định
        /// thì màn có Camera Max Size lớn phải kéo sâu hơn hẳn mới thấy số.
        ///
        /// LevelConfig để trống Fade Switch Size thì không có mốc dưới để chia tỉ lệ,
        /// lúc đó quay về ngưỡng pixel — quy đổi ra cùng một đơn vị để chỗ trên chỉ phải
        /// so sánh một lần.
        private float ResolveShowSize()
        {
            var config = _boardView.Config;
            var fadeSwitchSize = config != null ? config.FadeSwitchSize : 0f;

            if (fadeSwitchSize > 0f && _baseSize > 0f)
            {
                return Mathf.Lerp(_baseSize, fadeSwitchSize, _showAtZoomProgress);
            }

            if (_minCellScreenPixels <= 0f) return 0f;

            // cellPixels = Screen.height / (2 * size), nên đảo lại ra size.
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

        /// Tắt RENDERER chứ không tắt GameObject — cùng lý do đã ghi ở JewelLayer.Release:
        /// SetActive phải duyệt cây con, gửi thông điệp vòng đời và cập nhật lại cấu trúc
        /// culling, đắt gấp nhiều lần một cờ bool. Lớp số là lớp đông object nhất trong
        /// ba lớp, nên đây cũng là chỗ ăn nhiều nhất khi zoom qua lại liên tục.
        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var entry)) return;

            entry.Renderer.enabled = false;
            PoolFor(entry.Number).Push(entry);

            _active.Remove(cell);
        }

        /// Lấy ra ở trạng thái ẨN. Refresh chỉ đặt vị trí; cả lượt xong thì RevealAll mới
        /// bật đồng loạt, nên người chơi không thấy chữ bò dần ra theo từng frame.
        private Label Rent(int number)
        {
            var pool = PoolFor(number);

            return pool.Count > 0 ? pool.Pop() : CreateLabel(number);
        }

        /// Đổ chữ và màu MỘT LẦN duy nhất, ngay lúc tạo. Từ đó về sau chữ này chỉ đổi
        /// vị trí và cờ renderer — hai thứ không buộc dựng lại lưới chữ.
        ///
        /// ForceMeshUpdate ngay tại đây để lưới chữ được dựng LÚC NÀY, trong lượt dựng sẵn
        /// mà màn hình chờ đang che. Không gọi thì TextMeshPro để dành việc đó tới frame
        /// đầu tiên chữ được vẽ — tức đúng frame người chơi zoom qua ngưỡng hiện số.
        ///
        /// Đổi Number Color trong Inspector lúc đang chạy vì thế không có tác dụng với
        /// chữ đã tạo. Đổi rồi thì vào lại màn.
        private Label CreateLabel(int number)
        {
            var text = Instantiate(_numberPrefab, _root);

            text.color = _numberColor;
            text.SetText("{0}", number);
            text.ForceMeshUpdate();

            // Không cần kiểm null: HandleBoardRebuilt đã chặn prefab thiếu MeshRenderer
            // trước khi tới đây. Nhờ vậy Release và RevealAll — hai hàm chạy cả nghìn lượt
            // mỗi frame khi zoom — không phải kiểm tra gì, mà kiểm null trên UnityEngine.Object
            // không rẻ như so sánh tham chiếu thường.
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
