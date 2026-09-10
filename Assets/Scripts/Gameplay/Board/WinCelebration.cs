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
    /// Không loé mọi ô. Bảng 64x64 là 4096 hiệu ứng trong hơn một giây — chắc chắn khựng,
    /// mà nhìn cũng chỉ ra một mảng trắng. Sparkles Per Diagonal bốc thưa ra cho thành
    /// dải lấp lánh, nhưng bốc theo TỪNG đường chéo để không đường nào bị rỗng.
    public class WinCelebration : MonoBehaviour
    {
        [Tooltip("Kho hiệu ứng loé. Gán ParticleBurstPool hay FlipbookBurstPool đều " +
                 "được — lớp này không quan tâm hiệu ứng được vẽ bằng gì.")]
        [SerializeField] private BurstEffectPool _burstPool;

        [Header("Camera")]
        [Tooltip("Thời gian camera thu về tâm bảng và mức kéo xa nhất, tính bằng giây.")]
        [SerializeField] private float _cameraDuration = 1.1f;

        [Header("Dải quét")]
        [Tooltip("Thời gian dải sáng đi hết từ góc trên trái tới góc dưới phải.")]
        [SerializeField] private float _sweepDuration = 1.4f;

        [Tooltip("Chờ ngần này giây rồi mới bắt đầu quét, để camera kịp lùi ra một chút. " +
                 "Để 0 là chạy đồng thời ngay từ đầu.")]
        [SerializeField] private float _sweepStartDelay = 0.15f;

        [Tooltip("Số ô loé trên MỖI đường chéo. Đây là mật độ của dải sáng.\n\n" +
                 "Không phụ thuộc cỡ bảng: bảng 32x32 và bảng 64x64 đều cho ra dải dày " +
                 "như nhau, chỉ khác là dải trên bảng lớn thưa hơn theo chiều dài. Nhờ " +
                 "vậy chỉnh một lần là đúng cho mọi màn.\n\n" +
                 "Tổng số lần loé xấp xỉ (W + H - 1) x số này. Bảng 64x64 với giá trị 4 " +
                 "là khoảng 500 lần, rải trong Sweep Duration.")]
        [Range(1, 16)]
        [SerializeField] private int _sparklesPerDiagonal = 4;

        [Tooltip("Số ô loé tối đa trong MỘT frame. **Để 0 là không giới hạn** — và đó " +
                 "là giá trị nên dùng.\n\n" +
                 "Bản thân tốc độ của front đã là cái phanh: nó chỉ đi (W + H - 1) / " +
                 "Sweep Duration đường chéo mỗi giây, nên số ô loé mỗi frame tự nó đã " +
                 "bị chặn ở khoảng đó nhân Sparkles Per Diagonal.\n\n" +
                 "Đặt một số dương thì phải LỚN HƠN tích đó, không thì dải sáng tụt lại " +
                 "sau vệt quét và lê lết mãi mới hết. Bảng 64x64, Sweep Duration 1.4, " +
                 "60fps: front đi 1.5 đường/frame, nên con số tối thiểu là 1.5 x " +
                 "Sparkles Per Diagonal — nhân đôi lên cho có chỗ đuổi kịp sau mỗi lần " +
                 "kho đầy.")]
        [SerializeField] private int _maxSpawnPerFrame;


        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isSweeping;
        private float _elapsed;


        /// Đường chéo kế tiếp cần xử lý. Giữ lại giữa các frame để dải sáng không quay
        /// đầu và không loé lại chỗ đã đi qua.
        private int _nextDiagonal;

        /// Đã loé tới ô thứ mấy trên đường chéo đang làm dở.
        ///
        /// Cần nhớ vì kho có thể đầy giữa chừng một đường chéo. Bỏ qua phần còn lại là
        /// đục một lỗ vào dải sáng; quay lại làm từ đầu đường chéo đó là loé đúp mấy ô
        /// đầu. Chỉ có nhớ chỗ dừng mới không mất và không lặp.
        private int _nextSlot;

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
            _nextDiagonal = 0;
            _nextSlot = 0;

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
            _nextSlot = 0;

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

            // 0 nghĩa là không giới hạn. int.MaxValue thay vì rẽ nhánh riêng: một
            // đường chéo nhiều nhất cũng chỉ Sparkles Per Diagonal ô nên phép trừ không
            // bao giờ chạm đáy.
            var budget = _maxSpawnPerFrame > 0 ? _maxSpawnPerFrame : int.MaxValue;

            // Hạn mức xét GIỮA các đường chéo, không cắt ngang một đường. Vượt hạn mức
            // nhiều nhất là bằng Sparkles Per Diagonal, tức là vài ô — không đáng để
            // thêm một chỗ dừng thứ hai bên cạnh _nextSlot.
            while (_nextDiagonal <= front && budget > 0)
            {
                // Kho đầy thì DỪNG HẲN frame này, giữ nguyên cả _nextDiagonal lẫn
                // _nextSlot. Front vẫn chạy tiếp, nên khi có chỗ trống dải sáng sẽ đuổi
                // theo — chậm một nhịp còn hơn thủng một mảng.
                if (!SpawnDiagonal(grid, layout, _nextDiagonal, ref budget)) return;

                _nextDiagonal++;
            }

            if (_nextDiagonal > lastDiagonal) _isSweeping = false;
        }

        /// Loé một đường chéo. Trả về false khi kho đầy giữa chừng.
        ///
        /// x chạy trong đoạn giao giữa [0, Width) và [d - Height + 1, d], vì y = d - x
        /// cũng phải nằm trong bảng. Nhờ vậy không phải duyệt cả lưới rồi lọc.
        ///
        /// Cách bốc thưa: chia đoạn đó thành Sparkles Per Diagonal khoang bằng nhau rồi
        /// lấy một ô trong mỗi khoang. MỌI đường chéo vì thế đều có ô loé.
        ///
        /// Luật cũ — "x và y đều chia hết cho step" — mới là thứ làm dải sáng nhấp nháy.
        /// Trên một đường chéo thì y = d - x, nên x chia hết cho step kéo theo y chia hết
        /// chỉ khi chính d chia hết cho step. Với step 4, đúng 31 trên 127 đường chéo của
        /// bảng 64x64 có ô loé, ba đường liền sau mỗi vệt là rỗng trơn. Cái nhìn thấy là
        /// những vệt rời cách nhau 44ms chứ không phải một dải quét — và luật đó hỏng ở
        /// MỌI step lớn hơn 1, tức là ở mọi giá trị dùng được.
        private bool SpawnDiagonal(PixelGrid grid, BoardLayout layout, int diagonal, ref int budget)
        {
            var minX = Mathf.Max(0, diagonal - grid.Height + 1);
            var maxX = Mathf.Min(grid.Width - 1, diagonal);
            var span = maxX - minX + 1;

            if (span <= 0)
            {
                _nextSlot = 0;
                return true;
            }

            var slots = Mathf.Clamp(_sparklesPerDiagonal, 1, span);

            for (var slot = _nextSlot; slot < slots; slot++)
            {
                var from = minX + slot * span / slots;
                var to = minX + (slot + 1) * span / slots - 1;
                if (to < from) to = from;

                if (!TrySpawnInSlot(grid, layout, diagonal, from, to, out var accepted))
                {
                    // Kho từ chối. Ghi lại chỗ dừng để frame sau làm tiếp đúng khoang này.
                    _nextSlot = slot;
                    return false;
                }

                if (accepted) budget--;
            }

            _nextSlot = 0;
            return true;
        }

        /// Tìm một ô ĐÃ TÔ trong khoang [from, to] rồi loé nó.
        ///
        /// Bắt đầu từ một vị trí xê dịch theo đường chéo chứ không phải từ mép trái khoang.
        /// Luôn lấy ô đầu khoang thì các điểm sáng xếp thành những đường thẳng đều tăm tắp
        /// cắt ngang bảng, và mắt đọc ra ngay là một cái lưới trượt qua chứ không phải ánh
        /// sáng lấp lánh.
        ///
        /// Cuộn vòng trong khoang thay vì bỏ cuộc khi gặp ô rỗng: tranh có nền trong suốt
        /// thì phần lớn khoang ở rìa bảng rơi vào vùng rỗng, và bỏ cuộc là dải sáng mỏng
        /// dần rồi mất hẳn ở hai đầu.
        ///
        /// accepted = false nghĩa là cả khoang không có ô nào đã tô — khác hẳn với việc
        /// kho từ chối, nên không tính vào hạn mức mỗi frame.
        private bool TrySpawnInSlot(PixelGrid grid, BoardLayout layout, int diagonal,
            int from, int to, out bool accepted)
        {
            accepted = false;

            var width = to - from + 1;
            var start = (int)(Scatter(diagonal, from) * width);

            for (var i = 0; i < width; i++)
            {
                var x = from + (start + i) % width;
                var y = diagonal - x;

                if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                if (!_burstPool.Play(layout.CellToWorldCenter(x, y))) return false;

                accepted = true;
                return true;
            }

            return true;
        }

        /// Một số trong [0, 1) trông ngẫu nhiên nhưng LẶP LẠI ĐƯỢC với cùng cặp đầu vào.
        ///
        /// Dùng phép băm thay vì Random để dải quét của cùng một màn luôn trông y hệt
        /// nhau. Chơi lại mà mỗi lần lấp lánh một kiểu thì không sai, nhưng lúc bạn ngồi
        /// chỉnh mật độ sẽ không so được hai lần chạy với nhau.
        private static float Scatter(int diagonal, int column)
        {
            var hash = diagonal * 73856093 ^ (column + 1) * 19349663;

            return ((hash >> 8) & 0xFFFF) / 65536f;
        }
    }
}
