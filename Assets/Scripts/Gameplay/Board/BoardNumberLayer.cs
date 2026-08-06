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

        [Tooltip("Số chữ được sinh tối đa trong MỘT frame. Zoom liên tục có thể đẩy hàng " +
                 "nghìn ô vào tầm nhìn cùng lúc; chia ra nhiều frame thì không có cú khựng " +
                 "nào, đổi lại số ở rìa hiện chậm hơn vài frame.")]
        [SerializeField] private int _maxSpawnPerFrame = 24;

        [Tooltip("Nới rộng vùng tính toán thêm bao nhiêu ô quanh tầm nhìn. Ô ở rìa không " +
                 "bị thu về rồi sinh lại liên tục mỗi khi camera nhích một chút.")]
        [SerializeField] private int _visibleMarginCells = 2;

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

            var budget = Mathf.Max(1, _maxSpawnPerFrame);

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

                    _active[cell] = label;

                    // Duyệt lại từ đầu ở frame sau; ô đã có thì bỏ qua ngay bằng một phép
                    // tra dictionary, rẻ hơn nhiều so với Instantiate nên không đáng lo.
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
