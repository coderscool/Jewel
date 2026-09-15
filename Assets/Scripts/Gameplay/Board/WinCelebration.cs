using System.Collections;
using JewelPainter.Gameplay.Domain;
using UnityEngine;
using UnityEngine.Serialization;

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
    ///
    /// **Thời lượng là một lời hứa, không phải một mong muốn.** Mặt sóng đi theo đồng
    /// hồ: nó luôn tới góc cuối đúng vào giây thứ Sweep Duration, nên bảng 39x52 và bảng
    /// 101x105 ăn mừng xong cùng một lúc — bảng lớn thì sóng chạy nhanh hơn. Hệ quả là
    /// kho hiệu ứng KHÔNG được phép phanh mặt sóng: kho đầy thì khoang đó mất một ô loé
    /// và sóng đi tiếp, chứ không đứng đợi. Thiếu một điểm sáng không ai nhận ra; dải
    /// quét lê thêm hai giây trên màn lớn thì ai cũng thấy.
    ///
    /// **Ngân sách phải tính trước, không dò bằng mắt.** Số hiệu ứng sống cùng lúc ở
    /// khúc giữa xấp xỉ:
    ///
    ///     (W + H - 1) / Sweep Duration  x  Sparkles Per Diagonal  x  độ dài một cú loé
    ///
    /// Vượt Max Concurrent của kho thì phần thừa bị bỏ, và nó bỏ đúng KHÚC GIỮA — đó là
    /// nơi đường chéo dài nhất nên khoang nào cũng đầy, trong khi hai đầu bảng đường chéo
    /// ngắn, số khoang bị chính chiều dài của nó kẹp lại. Triệu chứng vì thế rất dễ đọc
    /// nhầm: dải sáng chạy đẹp, giữa chừng loãng hẳn đi rồi cuối lại đẹp. Đó không phải
    /// lỗi nhịp, đó là trần kho. Cuối mỗi lần quét lớp này đếm và báo ra số điểm bị bỏ.
    public class WinCelebration : MonoBehaviour
    {
        [Tooltip("Kho hiệu ứng loé. Gán ParticleBurstPool hay FlipbookBurstPool đều " +
                 "được — lớp này không quan tâm hiệu ứng được vẽ bằng gì.")]
        [SerializeField] private BurstEffectPool _burstPool;

        [Header("Camera")]
        [Tooltip("Thời gian camera thu về tâm bảng và mức kéo xa nhất, tính bằng giây.")]
        [SerializeField] private float _cameraDuration = 1.1f;

        [Header("Dải quét")]
        [Tooltip("Thời gian dải sáng đi hết từ góc trên trái tới góc dưới phải.\n\n" +
                 "GIỐNG NHAU CHO MỌI MÀN. Mặt sóng chạy theo tỉ lệ thời gian đã trôi chứ " +
                 "không theo số đường chéo, nên bảng càng lớn sóng càng đi nhanh — mọi màn " +
                 "quét xong đúng vào con số này.\n\n" +
                 "Muốn tốc độ sóng như nhau ở mọi cỡ bảng thì đây là ô SAI để chỉnh: phải " +
                 "đổi công thức, không phải đổi số.")]
        [SerializeField] private float _sweepDuration = 1.5f;

        [Tooltip("Chờ ngần này giây rồi mới bắt đầu quét, để camera kịp lùi ra một chút. " +
                 "Để 0 là chạy đồng thời ngay từ đầu.\n\n" +
                 "CỘNG THÊM vào Sweep Duration chứ không nằm trong đó: tổng thời gian ăn " +
                 "mừng là hai số cộng lại.")]
        [SerializeField] private float _sweepStartDelay = 0.15f;

        [Tooltip("Số ô loé trên MỖI đường chéo. Đây là mật độ của dải sáng.\n\n" +
                 "Không phụ thuộc cỡ bảng: bảng 32x32 và bảng 64x64 đều cho ra dải dày " +
                 "như nhau, chỉ khác là dải trên bảng lớn thưa hơn theo chiều dài. Nhờ " +
                 "vậy chỉnh một lần là đúng cho mọi màn.\n\n" +
                 "Tổng số lần loé xấp xỉ (W + H - 1) x số này, rải trong Sweep Duration.\n\n" +
                 "TRẦN THẬT SỰ NẰM Ở KHO, KHÔNG NẰM Ở ĐÂY. Số hiệu ứng sống cùng lúc bằng " +
                 "tốc độ bắn nhân độ dài một cú loé. Vượt Max Concurrent của kho thì phần " +
                 "thừa bị bỏ, và nâng ô này thêm nữa không làm dải sáng dày lên chút nào — " +
                 "chỉ làm nhiều cú loé bị nuốt hơn. Nâng mật độ thì phải nâng Max Concurrent " +
                 "cùng lúc.")]
        [Range(1, 64)]
        [SerializeField] private int _sparklesPerDiagonal = 32;

        [Tooltip("Số ô loé tối đa trong MỘT frame. **Để 0 là không giới hạn** — và đó " +
                 "là giá trị nên dùng.\n\n" +
                 "Bản thân tốc độ của front đã là cái phanh: nó chỉ đi (W + H - 1) / " +
                 "Sweep Duration đường chéo mỗi giây, nên số ô loé mỗi frame tự nó đã " +
                 "bị chặn ở khoảng đó nhân Sparkles Per Diagonal.\n\n" +
                 "Đặt một số dương thì phải LỚN HƠN tích đó, không thì dải sáng tụt lại " +
                 "sau vệt quét. Hạn mức này bị BỎ QUA ở frame chốt để lời hứa về thời " +
                 "lượng không bị nó phá — nên đặt quá thấp là dồn phần nợ vào đúng một " +
                 "frame cuối và tạo ra cú khựng ngay lúc đáng lẽ đẹp nhất.")]
        [SerializeField] private int _maxSpawnPerFrame;

        [Header("Nhịp lắng sau khi quét")]
        [Tooltip("Chờ ngần này giây SAU KHI dải quét tắt rồi mới lùi camera.\n\n" +
                 "Một nhịp thở, không phải một quãng chờ: hai màn diễn dính liền nhau thì " +
                 "mắt đọc thành một, mà hở quá thì thành hai cảnh rời.")]
        [FormerlySerializedAs("_frameDelay")]
        [SerializeField] private float _settleDelay = 0.1f;

        [Tooltip("Thời gian camera lùi ra và nhấc tranh lên. Để 0 là BỎ HẲN màn diễn thứ " +
                 "hai — camera dừng luôn ở khung hình thắng màn.")]
        [FormerlySerializedAs("_frameCameraDuration")]
        [SerializeField] private float _settleDuration = 0.45f;

        [Tooltip("Camera lùi ra thêm chừng này Ô mỗi phía, làm bức tranh nhỏ lại.\n\n" +
                 "Tính bằng ô chứ không bằng hệ số zoom: cùng một hệ số cho ra phần lề rất " +
                 "khác nhau giữa bảng 39x52 và bảng 101x105, còn số ô thì đúng ở mọi cỡ.")]
        [SerializeField] private float _settleExtraCells = 2.5f;

        [Tooltip("Đồng thời nhấc bức tranh lên chừng này PHẦN chiều cao màn hình. 0.06 là " +
                 "6% chiều cao màn.\n\n" +
                 "Tính theo màn hình chứ không theo ô vì đây là quyết định bố cục: chừa " +
                 "chỗ phía dưới cho cụm thưởng sắp hiện. Thứ phải đúng là khoảng cách mắt " +
                 "nhìn thấy, không phải số ô.\n\n" +
                 "Số âm thì tranh đi xuống.")]
        [SerializeField] private float _settleRiseFraction = 0.06f;


        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isSweeping;
        private float _elapsed;


        /// Đường chéo kế tiếp cần xử lý. Giữ lại giữa các frame để dải sáng không quay
        /// đầu và không loé lại chỗ đã đi qua.
        private int _nextDiagonal;

        /// Số khoang mất điểm sáng vì kho đầy, tính cho cả lần quét. Chỉ để báo ra cuối
        /// lượt — xem chú thích đầu lớp.
        private int _droppedSlots;

        /// Tổng thời gian từ lúc Play() tới lúc màn ăn mừng đứng yên: quét xong, camera
        /// lùi xong, khung tranh đã vào chỗ.
        ///
        /// Có mặt để LevelFlowController canh giờ mở popup mà không phải chép tay mấy con
        /// số của lớp này sang một Inspector khác — chép tay thì chỉnh một bên là bên kia
        /// lệch, và thứ người chơi thấy là một khoảng màn hình đứng chết.
        ///
        /// KHÔNG cộng độ dài một cú loé: điểm sáng cuối còn sáng thêm nửa giây nữa, nhưng
        /// nửa giây đó là phần đuôi đang tan, popup chồng lên nó là đúng.
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

        /// LevelFlowController gọi khi ô cuối cùng đã đáp xuống.
        public void Play()
        {
            if (_boardView == null || _boardView.Grid == null) return;

            // Dọn dư âm của lượt trước: chơi lại một màn thì Play() chạy lần nữa mà không
            // qua HandleBoardRebuilt, và nhịp lắng của lượt cũ có thể còn đang chạy.
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

            // 0 nghĩa là không giới hạn. int.MaxValue thay vì rẽ nhánh riêng: một
            // đường chéo nhiều nhất cũng chỉ Sparkles Per Diagonal ô nên phép trừ không
            // bao giờ chạm đáy.
            //
            // Tới frame chốt thì hạn mức bị gỡ hẳn. Đây là chỗ duy nhất giữ được lời hứa
            // "mọi màn xong trong Sweep Duration": hạn mức có thể làm phần đuôi nợ lại vài
            // đường chéo, và không gỡ thì món nợ đó kéo dải sáng dài quá hạn.
            var budget = _maxSpawnPerFrame > 0 && progress < 1f ? _maxSpawnPerFrame : int.MaxValue;

            // Hạn mức xét GIỮA các đường chéo, không cắt ngang một đường. Vượt hạn mức
            // nhiều nhất là bằng Sparkles Per Diagonal, tức là vài ô — không đáng để
            // thêm một chỗ dừng thứ hai ở giữa đường chéo.
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

        /// Màn diễn thứ hai có chạy không.
        ///
        /// Một con số 0 ở ô thời gian là cách tắt: không cần thêm một ô tick nữa cho cùng
        /// một quyết định, và "lùi camera trong 0 giây" vốn đã chẳng có gì để xem.
        private bool HasSettleStage => _settleDuration > 0f;

        /// Màn diễn thứ hai: bức tranh lùi lại một nấc và dâng lên một chút.
        ///
        /// Nối vào ĐUÔI dải quét chứ không hẹn giờ song song từ lúc Play(). Dải quét có
        /// thể kết thúc muộn hơn Sweep Duration một hai frame, và một cái hẹn giờ riêng sẽ
        /// lệch đúng ngần đó — ít, nhưng đây là chỗ hai chuyển động phải khớp nhau.
        private void BeginSettleStage()
        {
            if (!HasSettleStage) return;

            StartCoroutine(SettleRoutine());
        }

        private IEnumerator SettleRoutine()
        {
            if (_settleDelay > 0f) yield return new WaitForSeconds(_settleDelay);

            if (_boardCamera == null) yield break;

            // Thu nhỏ và dâng lên là MỘT chuyển động, do một lời gọi duy nhất lo: cả hai
            // đều chỉ là camera đi tới một khung hình khác, và camera nội suy một lần thì
            // hai thành phần không thể lệch pha nhau. Tách thành hai tween song song là tự
            // mở ra khả năng chúng đi lệch nhịp — mà mắt đọc ra ngay, thành một cú trôi
            // chéo thay vì một bức tranh đang lùi lại.
            _boardCamera.FrameWholeBoard(_settleDuration, _settleExtraCells, _settleRiseFraction);
        }

        /// Báo ra khi dải quét bị thủng vì kho đầy, kèm đủ số để sửa ngay mà không phải
        /// ngồi đoán. Chỉ trong Editor và bản dev: trên máy yếu việc rơi vài điểm sáng là
        /// chuyện có thể chấp nhận, không đáng đổ log vào bản phát hành.
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

        /// Loé một đường chéo, trả về số ô THẬT SỰ loé được.
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

        /// Tìm một ô ĐÃ TÔ trong khoang [from, to] rồi loé nó. false = khoang này không
        /// ra được điểm sáng nào, vì cả khoang rỗng hoặc vì kho đã đầy.
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
        /// Kho đầy thì bỏ luôn khoang này, KHÔNG thử ô khác: trần đồng thời là của cả
        /// kho, ô nào cũng sẽ bị từ chối y hệt. Và không xếp lại hàng để loé bù — xem
        /// chú thích đầu lớp về lời hứa thời lượng.
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

                // Tách hẳn "kho đầy" khỏi "cả khoang rỗng". Gộp hai thứ vào một con số
                // thì bức tranh nào nhiều nền trong suốt cũng báo động giả, và lời cảnh
                // báo mất hết giá trị.
                poolFull = true;
                return false;
            }

            return false;
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
