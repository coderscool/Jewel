using System.Collections;
using JewelPainter.Gameplay.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace JewelPainter.Gameplay.Board
{
    /// Màn ăn mừng khi tô xong bức tranh.
    public class WinCelebration : MonoBehaviour
    {
        [Tooltip("Kho hiệu ứng loé.")]
        [SerializeField] private BurstEffectPool _burstPool;

        [Header("Camera")]
        [Tooltip("Thời gian camera thu về tâm bảng và mức kéo xa nhất, tính bằng giây.")]
        [SerializeField] private float _cameraDuration = 1.1f;

        [Header("Dải quét")]
        [Tooltip("Thời gian dải sáng đi hết từ góc trên trái tới góc dưới phải.")]
        [SerializeField] private float _sweepDuration = 1.5f;

        [Tooltip("Chờ ngần này giây rồi mới bắt đầu quét, để camera kịp lùi ra một chút.")]
        [SerializeField] private float _sweepStartDelay = 0.15f;

        [Tooltip("Số ô loé trên mỗi đường chéo.")]
        [Range(1, 64)]
        [SerializeField] private int _sparklesPerDiagonal = 32;

        [Tooltip("Số ô loé tối đa trong một frame.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Header("Nhịp lắng sau khi quét")]
        [Tooltip("Thời gian chờ sau khi dải quét tắt rồi mới lùi camera.")]
        [FormerlySerializedAs("_frameDelay")]
        [SerializeField] private float _settleDelay = 0.1f;

        [Tooltip("Thời gian camera lùi ra và nhấc tranh lên.")]
        [FormerlySerializedAs("_frameCameraDuration")]
        [SerializeField] private float _settleDuration = 0.45f;

        [Tooltip("Số ô camera lùi ra thêm mỗi phía.")]
        [SerializeField] private float _settleExtraCells = 2.5f;

        [Tooltip("Tỉ lệ chiều cao màn hình nhấc bức tranh lên.")]
        [SerializeField] private float _settleRiseFraction = 0.06f;

        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isSweeping;
        private float _elapsed;

        private int _nextDiagonal;

        private int _droppedSlots;

        public float CelebrationTotalSeconds
        {
            get
            {
                var total = Mathf.Max(0f, _sweepStartDelay) + Mathf.Max(0f, _sweepDuration);

                if (!HasSettleStage) return total;

                return total + Mathf.Max(0f, _settleDelay) + _settleDuration;
            }
        }

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
            StopAllCoroutines();

            _isSweeping = false;
            _nextDiagonal = 0;
            _droppedSlots = 0;

            if (_burstPool == null) return;

            _burstPool.ReleaseAll();
            _burstPool.Prewarm();
        }

        /// Chạy màn ăn mừng.
        public void Play()
        {
            if (_boardView == null || _boardView.Grid == null) return;

            StopAllCoroutines();

            if (_boardCamera != null) _boardCamera.FrameWholeBoard(_cameraDuration);

            _elapsed = 0f;
            _nextDiagonal = 0;
            _droppedSlots = 0;

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

            var budget = _maxSpawnPerFrame > 0 && progress < 1f ? _maxSpawnPerFrame : int.MaxValue;

            while (_nextDiagonal <= front && budget > 0)
            {
                budget -= SpawnDiagonal(grid, layout, _nextDiagonal);
                _nextDiagonal++;
            }

            if (_nextDiagonal <= lastDiagonal) return;

            _isSweeping = false;
            ReportDroppedSlots(lastDiagonal);

            BeginSettleStage();
        }

        private bool HasSettleStage => _settleDuration > 0f;

        /// Màn diễn thứ hai: bức tranh lùi lại một nấc và dâng lên một chút.
        private void BeginSettleStage()
        {
            if (!HasSettleStage) return;

            StartCoroutine(SettleRoutine());
        }

        private IEnumerator SettleRoutine()
        {
            if (_settleDelay > 0f) yield return new WaitForSeconds(_settleDelay);

            if (_boardCamera == null) yield break;

            _boardCamera.FrameWholeBoard(_settleDuration, _settleExtraCells, _settleRiseFraction);
        }

        /// Cảnh báo khi dải quét bị thủng vì kho đầy.
        private void ReportDroppedSlots(int lastDiagonal)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_droppedSlots <= 0) return;

            var diagonalsPerSecond = _sweepDuration > 0f ? (lastDiagonal + 1) / _sweepDuration : 0f;

            Debug.LogWarning(
                $"{nameof(WinCelebration)}: kho hiệu ứng đầy, {_droppedSlots} điểm sáng bị bỏ. " +
                "Dải quét sẽ loãng hẳn ở KHÚC GIỮA — đó là nơi đường chéo dài nhất nên " +
                "khoang nào cũng đầy.\n" +
                $"Cần sống cùng lúc ≈ {diagonalsPerSecond:0} đường/giây x {_sparklesPerDiagonal} " +
                "x độ dài một cú loé (FrameCount / Fps / Speed của kho).\n" +
                "Sửa bằng MỘT trong ba: nâng Max Concurrent của kho, hạ Sparkles Per " +
                "Diagonal, hoặc nâng Speed của kho cho cú loé ngắn lại.", this);
#endif
        }

        /// Loé một đường chéo, trả về số ô thật sự loé được.
        private int SpawnDiagonal(PixelGrid grid, BoardLayout layout, int diagonal)
        {
            var minX = Mathf.Max(0, diagonal - grid.Height + 1);
            var maxX = Mathf.Min(grid.Width - 1, diagonal);
            var span = maxX - minX + 1;

            if (span <= 0) return 0;

            var slots = Mathf.Clamp(_sparklesPerDiagonal, 1, span);
            var spawned = 0;

            for (var slot = 0; slot < slots; slot++)
            {
                var from = minX + slot * span / slots;
                var to = minX + (slot + 1) * span / slots - 1;
                if (to < from) to = from;

                if (TrySpawnInSlot(grid, layout, diagonal, from, to, out var poolFull)) spawned++;
                else if (poolFull) _droppedSlots++;
            }

            return spawned;
        }

        /// Tìm một ô đã tô trong khoang [from, to] rồi loé nó.
        private bool TrySpawnInSlot(PixelGrid grid, BoardLayout layout, int diagonal,
            int from, int to, out bool poolFull)
        {
            poolFull = false;

            var width = to - from + 1;
            var start = (int)(Scatter(diagonal, from) * width);

            for (var i = 0; i < width; i++)
            {
                var x = from + (start + i) % width;
                var y = diagonal - x;

                if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                if (_burstPool.Play(layout.CellToWorldCenter(x, y))) return true;

                poolFull = true;
                return false;
            }

            return false;
        }

        /// Số giả ngẫu nhiên trong [0, 1) lặp lại được theo đầu vào.
        private static float Scatter(int diagonal, int column)
        {
            var hash = diagonal * 73856093 ^ (column + 1) * 19349663;

            return ((hash >> 8) & 0xFFFF) / 65536f;
        }
    }
}
