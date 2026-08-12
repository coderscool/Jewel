using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Màn ăn mừng khi tô xong bức tranh: camera thu về toàn cảnh, đồng thời một dải
    /// lấp lánh quét chéo qua bảng từ góc trên trái xuống góc dưới phải.
    ///
    /// Quét theo ĐƯỜNG CHÉO chứ không theo hàng: mọi ô có cùng tổng (x + y) nằm trên
    /// một đường chéo, nên chỉ cần cho một con số chạy từ 0 tới (W + H - 2) là có ngay
    /// mặt sóng đi từ góc này sang góc kia. Không phải xếp trước danh sách ô nào cả.
    ///
    /// Không loé mọi ô. Bảng 64x64 là 4096 hệ hạt trong hơn một giây — chắc chắn khựng,
    /// mà nhìn cũng chỉ ra một mảng trắng. Cell Step bốc thưa ra cho thành dải lấp lánh.
    public class WinCelebration : MonoBehaviour
    {
        [SerializeField] private ParticleBurstPool _burstPool;

        [Header("Camera")]
        [Tooltip("Thời gian camera thu về tâm bảng và mức kéo xa nhất, tính bằng giây.")]
        [SerializeField] private float _cameraDuration = 1.1f;

        [Header("Dải quét")]
        [Tooltip("Thời gian dải sáng đi hết từ góc trên trái tới góc dưới phải.")]
        [SerializeField] private float _sweepDuration = 1.4f;

        [Tooltip("Chờ ngần này giây rồi mới bắt đầu quét, để camera kịp lùi ra một chút. " +
                 "Để 0 là chạy đồng thời ngay từ đầu.")]
        [SerializeField] private float _sweepStartDelay = 0.15f;

        [Tooltip("Cứ mấy ô thì loé một ô, tính theo CẢ HAI trục. 1 là mọi ô — đừng dùng " +
                 "cho bảng lớn. 4 nghĩa là 1/16 số ô, bảng 64x64 còn khoảng 256 lần loé.")]
        [Range(1, 8)]
        [SerializeField] private int _cellStep = 4;

        [Tooltip("Số ô loé tối đa trong MỘT frame. Chặn cú khựng khi dải quét đi qua " +
                 "đoạn giữa bảng, nơi mỗi đường chéo dài nhất.")]
        [SerializeField] private int _maxSpawnPerFrame = 12;

        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isSweeping;
        private float _elapsed;

        /// Đường chéo kế tiếp cần xử lý. Giữ lại giữa các frame để dải sáng không quay
        /// đầu và không loé lại chỗ đã đi qua.
        private int _nextDiagonal;

        public void Init(BoardView boardView, BoardCamera boardCamera)
        {
            _boardView = boardView;
            _boardCamera = boardCamera;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt()
        {
            _isSweeping = false;

            if (_burstPool == null) return;

            _burstPool.ReleaseAll();
            _burstPool.Prewarm();
        }

        /// LevelFlowController gọi khi ô cuối cùng đã đáp xuống.
        public void Play()
        {
            if (_boardView == null || _boardView.Grid == null) return;

            if (_boardCamera != null) _boardCamera.FrameWholeBoard(_cameraDuration);

            _elapsed = 0f;
            _nextDiagonal = 0;

            if (_burstPool == null || !_burstPool.HasPrefab)
            {
                Debug.LogWarning($"{nameof(WinCelebration)} chưa có Burst Pool kèm prefab — " +
                                 "camera vẫn thu về nhưng không có dải lấp lánh.");
                _isSweeping = false;
                return;
            }

            _isSweeping = true;
        }

        private void Update()
        {
            if (!_isSweeping) return;

            _elapsed += Time.deltaTime;

            AdvanceSweep();
        }

        private void AdvanceSweep()
        {
            var sweepTime = _elapsed - _sweepStartDelay;
            if (sweepTime < 0f) return;

            var grid = _boardView.Grid;
            var layout = _boardView.Layout;

            if (grid == null || layout == null)
            {
                _isSweeping = false;
                return;
            }

            var lastDiagonal = grid.Width + grid.Height - 2;
            var progress = _sweepDuration > 0f ? Mathf.Clamp01(sweepTime / _sweepDuration) : 1f;
            var front = Mathf.RoundToInt(progress * lastDiagonal);

            var budget = Mathf.Max(1, _maxSpawnPerFrame);

            // Hạn mức xét GIỮA các đường chéo, không cắt ngang một đường. Cắt giữa
            // chừng thì phần còn lại của đường đó bị bỏ luôn ở frame sau, và dải sáng
            // thủng một mảng. Vượt hạn mức nhiều nhất là bằng số ô của một đường chéo,
            // mà con số đó đã bị Cell Step ghìm xuống rồi.
            while (_nextDiagonal <= front && budget > 0)
            {
                budget -= SpawnDiagonal(grid, layout, _nextDiagonal);
                _nextDiagonal++;
            }

            if (_nextDiagonal > lastDiagonal) _isSweeping = false;
        }

        /// Trả về số ô đã loé trên đường chéo này.
        ///
        /// x chạy trong đoạn giao giữa [0, Width) và [d - Height + 1, d], vì y = d - x
        /// cũng phải nằm trong bảng. Nhờ vậy không phải duyệt cả lưới rồi lọc.
        private int SpawnDiagonal(PixelGrid grid, BoardLayout layout, int diagonal)
        {
            var step = Mathf.Max(1, _cellStep);

            var minX = Mathf.Max(0, diagonal - grid.Height + 1);
            var maxX = Mathf.Min(grid.Width - 1, diagonal);

            var spawned = 0;

            for (var x = minX; x <= maxX; x++)
            {
                var y = diagonal - x;

                if (x % step != 0 || y % step != 0) continue;
                if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                _burstPool.Play(layout.CellToWorldCenter(x, y));
                spawned++;
            }

            return spawned;
        }
    }
}
