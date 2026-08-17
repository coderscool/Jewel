using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using TMPro;
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
        [SerializeField] private float _showAtZoomProgress = 0.6f;

        [Tooltip("Đường lui khi LevelConfig để trống Fade Switch Size: ô chiếu lên màn hình " +
                 "nhỏ hơn ngần này pixel thì không hiện số.")]
        [SerializeField] private float _minCellScreenPixels = 32f;

        [Tooltip("Màu chữ số, dùng chung cho mọi ô. Số chỉ thật sự đọc được khi lớp màu " +
                 "đã mờ đi, lúc đó nền sau nó gần như trong suốt nên một màu cố định là đủ.")]
        [SerializeField] private Color _numberColor = Color.black;

        [Tooltip("Số chữ được DỰNG tối đa trong một frame. Không phải số chữ hiện ra — " +
                 "chữ dựng xong vẫn trong suốt cho tới khi cả vùng nhìn xong, rồi tất cả " +
                 "hiện cùng lúc.\n\n" +
                 "Nâng lên thì số hiện sớm hơn nhưng mỗi frame nặng hơn. 48 chữ mỗi frame " +
                 "tốn chừng 5ms, vẫn lọt trong ngân sách 16ms của 60fps.")]
        [SerializeField] private int _maxSpawnPerFrame = 48;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn. Ô ở rìa không " +
                 "bị thu về rồi sinh lại liên tục mỗi khi camera nhích một chút.")]
        [SerializeField] private int _visibleMarginCells = 2;

        [Header("Thử nghiệm")]
        [Tooltip("Bỏ qua Max Spawn Per Frame: dựng TẤT CẢ số trong tầm nhìn trong đúng " +
                 "một frame.\n\n" +
                 "Bật lên để đo xem máy chịu được tới đâu. Số hiện ngay lập tức, không có " +
                 "0.3 giây chờ — đổi lại nếu máy yếu thì sẽ thấy một cú khựng đúng lúc " +
                 "zoom qua ngưỡng hiện số.\n\n" +
                 "Đổi được ngay trong Play Mode: tick rồi zoom ra zoom vào là thấy khác " +
                 "liền, không cần chạy lại.")]
        [SerializeField] private bool _spawnAllInOneFrame;

        private readonly Dictionary<Vector2Int, TextMeshPro> _active = new();
        private readonly Stack<TextMeshPro> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();

        private BoardView _boardView;
        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;

        /// Mức zoom lúc mới vào màn, tức mức xa nhất — mốc trên của dải zoom.
        /// Đọc từ camera chứ không từ LevelConfig.CameraMaxSize, vì ô đó có thể để 0
        /// và BoardCamera tự tính; lúc ấy LevelConfig không biết con số thật.
        private float _baseSize = -1f;

        private bool _needsBaseCapture = true;
        private bool _needsRefresh;

        /// Có chữ nào đang dựng xong nhưng còn trong suốt, chờ được bật lên cùng lượt.
        private bool _hasHiddenLabels;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            _needsBaseCapture = true;
            _lastOrthographicSize = -1f;   // ép tính lại ở LateUpdate kế tiếp
        }

        private void LateUpdate()
        {
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

        /// true khi đã phủ hết ô trong tầm nhìn; false khi hết hạn mức sinh của frame này
        /// và còn việc dở, lúc đó LateUpdate sẽ gọi lại ở frame sau.
        ///
        /// Chữ dựng ra ở alpha 0 và chỉ được bật lên khi cả lượt đã xong. Người chơi thấy
        /// TOÀN BỘ số trong tầm nhìn hiện cùng một lúc, thay vì thấy chúng bò dần từ trên
        /// xuống theo hạn mức mỗi frame.
        ///
        /// Không thể bỏ hạn mức để dựng hết trong một frame: mỗi TextMeshPro phải dựng
        /// lưới chữ, hàng trăm cái cùng lúc là một cú khựng thấy rõ. Đổi alpha thì chỉ
        /// ghi lại màu đỉnh của lưới đã có sẵn — rẻ hơn hẳn, làm đồng loạt được.
        private bool Refresh()
        {
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

                    var label = Rent();
                    label.transform.position = layout.CellToWorldCenter(x, y);
                    label.color = _numberColor;
                    label.SetText("{0}", index + 1);
                    label.alpha = 0f;

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

        /// Bật hết chữ đang trong suốt lên cùng một lượt.
        ///
        /// Duyệt _active chứ không giữ danh sách riêng: chữ có thể bị Release trả về kho
        /// giữa chừng, mà một danh sách riêng sẽ còn giữ tham chiếu tới nó và bật sáng
        /// nhầm một chữ đã được dùng lại cho ô khác.
        private void RevealAll()
        {
            if (!_hasHiddenLabels) return;

            _hasHiddenLabels = false;

            foreach (var label in _active.Values)
            {
                if (label.alpha < 1f) label.alpha = 1f;
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

        /// Tính theo dải zoom của màn: từ mức lúc vào màn xuống tới mức lớp màu tan hết.
        /// Nhờ đo tương đối nên màn nào cũng cho cảm giác như nhau — ngưỡng pixel cố định
        /// thì màn có Camera Max Size lớn phải kéo sâu hơn hẳn mới thấy số.
        ///
        /// LevelConfig để trống Fade Switch Size thì không có mốc dưới để chia tỉ lệ,
        /// lúc đó quay về ngưỡng pixel.
        private bool ShouldShowNumbers()
        {
            var config = _boardView.Config;
            var fadeSwitchSize = config != null ? config.FadeSwitchSize : 0f;

            if (fadeSwitchSize > 0f && _baseSize > 0f)
            {
                var showSize = Mathf.Lerp(_baseSize, fadeSwitchSize, _showAtZoomProgress);

                return _camera.orthographicSize <= showSize;
            }

            return BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize)
                   >= _minCellScreenPixels;
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

        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var label)) return;

            label.gameObject.SetActive(false);
            _pool.Push(label);
            _active.Remove(cell);
        }

        private TextMeshPro Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return Instantiate(_numberPrefab, _root);
        }
    }
}
