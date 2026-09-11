using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Booster "tô tự do": bật luật tô tự do trong ít giây rồi tự tắt.
    ///
    /// Đứng ở Gameplay chứ không nằm trong HudView vì "còn được tô tự do bao lâu nữa" là
    /// trạng thái luật chơi — nó quyết định ô nào tô được. HudView chỉ bấm nút, đọc số
    /// giây, và bật/tắt nút theo IFreePaintService.
    ///
    /// Không tự giữ cái cờ: cờ nằm ở PaintManager, cạnh đúng những hàm mà nó đổi luật.
    /// Giữ ở đây thì PaintManager phải đi hỏi ngược lên một manager khác mỗi lần có người
    /// chạm vào bảng.
    public class FreePaintController : MonoBehaviour, IFreePaintService
    {
        [Tooltip("Một lượt dùng kéo dài bao nhiêu giây.")]
        [SerializeField] private float _durationSeconds = 20f;

        [Tooltip("Đếm bằng thời gian CÓ nhân Time.timeScale.\n\n" +
                 "Bỏ tick là dùng thời gian thật (unscaled): booster vẫn chạy khi game bị " +
                 "dừng bằng timeScale = 0, và người chơi mở một popup là mất trắng ngần ấy " +
                 "giây. Tick vào thì mở popup coi như tạm dừng booster.")]
        [SerializeField] private bool _useScaledTime = true;

        private IPaintService _paintService;
        private PaintManager _paintManager;
        private FreePaintCredits _credits;

        private float _remainingSeconds;

        /// Đồng hồ đang bị giữ lại — xem IFreePaintService.SetPaused.
        private bool _paused;

        /// Giá trị đã báo ra lần gần nhất — chỉ bắn sự kiện khi thật sự đổi. Cùng lý do
        /// đã ghi ở HintFocusController.
        private bool _lastAvailability;

        public event Action<bool> OnActiveChanged;
        public event Action<int> OnCreditsChanged;
        public event Action OnCreditsExhausted;
        public event Action<bool> OnAvailabilityChanged;

        public bool IsActive => _paintService != null && _paintService.FreePaintActive;

        public float RemainingSeconds => IsActive ? _remainingSeconds : 0f;

        public bool IsPaused => _paused;

        public void SetPaused(bool paused) => _paused = paused;

        public float DurationSeconds => Mathf.Max(0.01f, _durationSeconds);

        public int RemainingCredits => _credits?.Remaining ?? 0;

        public bool CanUse
        {
            get
            {
                if (_paintService == null) return false;

                // KHÔNG xám đi trong lúc CHÍNH NÓ đang chạy.
                //
                // Hai chục giây đếm ngược là quãng dài nhất của bất cứ booster nào, và
                // một cái nút xám suốt quãng đó đọc ra là hỏng chứ không đọc ra là đang
                // chạy — nhất là khi đồng hồ nằm ở chỗ khác trên màn hình.
                //
                // Cú bấm lặp lại vẫn không tốn lượt: Use() thoát ngay ở đầu khi booster
                // đang chạy, TRƯỚC cú trừ. Xem chú thích ở đó.

                // KHÔNG khoá theo ColorLocked.
                //
                // Cờ đó vẫn còn nguyên việc của nó — chặn đổi màu giữa chừng — nhưng nó
                // không còn làm xám ba cái nút booster nữa. Một booster đang chạy mà hai
                // nút kia xám đi thì người chơi đọc ra là game đang bận, chứ không đọc ra
                // là "đợi nó xong đã"; họ bấm vào khoảng không và không hiểu vì sao.
                //
                // Đổi lại: hai booster chồng lên nhau thì hơi lộn xộn. Đó là lựa chọn có
                // chủ ý — người chơi tiêu lượt của chính họ, và họ được quyền tiêu lãng phí.

                // Hỏi CẢ BẢNG: còn ô nào chưa tô thì booster còn việc để làm. Không hỏi
                // riêng màu đang chọn, và cũng không đòi phải chọn màu — cả điểm của
                // booster này là không cần chọn màu nào.
                return !_paintService.IsComplete;
            }
        }

        public void Init(PaintManager paintManager, FreePaintCredits credits)
        {
            _paintManager = paintManager;
            _paintService = paintManager;
            _credits = credits;

            if (_credits != null) _credits.OnCreditsChanged += HandleCreditsChanged;

            _paintService.OnBoardReady += RefreshAvailability;
            _paintService.OnCellPainted += HandleCellPainted;

            // Nghe chính cái cờ mình bật, thay vì tin rằng chỉ mình mới tắt nó.
            // PaintManager tắt booster ở mỗi lần nạp màn, và đó là đường tắt mà lớp này
            // không nhìn thấy — không nghe thì đồng hồ vẫn chạy tiếp trong màn mới rồi
            // bật lại luật ở một frame nào đó.
            _paintService.OnFreePaintChanged += HandleFreePaintChanged;
            _paintService.OnColorLockChanged += HandleColorLockChanged;

            _lastAvailability = CanUse;
        }

        private void OnDestroy()
        {
            if (_credits != null) _credits.OnCreditsChanged -= HandleCreditsChanged;

            if (_paintService == null) return;

            _paintService.OnBoardReady -= RefreshAvailability;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnFreePaintChanged -= HandleFreePaintChanged;
            _paintService.OnColorLockChanged -= HandleColorLockChanged;
        }

        public bool Use()
        {
            if (!CanUse) return false;

            // Đang chạy dở thì cú bấm này rơi vào khoảng không — KHÔNG trừ lượt, không
            // thả lại đồng hồ.
            //
            // Phép kiểm này từng nằm trong CanUse, nhưng ở đó nó làm xám cái nút suốt
            // hai chục giây. Chuyển xuống đây thì nút vẫn sáng mà lượt vẫn an toàn: chỉ
            // riêng chỗ này mới biết một cú bấm có đáng bị tính tiền hay không.
            if (IsActive) return false;

            // Mọi đường thoát nằm hết TRÊN cú trừ lượt. Có đường nào lấy mất một lượt rồi
            // trả về false là người chơi mất lượt mà không thấy gì xảy ra.
            if (_credits != null && !_credits.TrySpend())
            {
                OnCreditsExhausted?.Invoke();
                return false;
            }

            _remainingSeconds = DurationSeconds;

            // Thả đồng hồ ở mỗi lượt dùng mới, không tin rằng lượt trước đã thả đúng.
            _paused = false;

            _paintManager.SetFreePaint(true);

            return true;
        }

        /// Tắt sớm. Công khai để cheat hoặc luồng khác dừng được mà không phải đợi hết giờ.
        public void Cancel()
        {
            _paintManager.SetFreePaint(false);
        }

        private void Update()
        {
            if (!IsActive) return;

            // Giữ nguyên số giây chứ không trừ đi rồi bù lại: người chơi mở bảng cài đặt
            // giữa lượt thì lượt đó phải còn nguyên đúng ngần ấy giây khi đóng lại.
            if (_paused) return;

            _remainingSeconds -= _useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;

            if (_remainingSeconds > 0f) return;

            Cancel();
        }

        private void HandleFreePaintChanged(bool active)
        {
            // Dọn đồng hồ ở đây, không ở Cancel: cờ có thể bị tắt từ chỗ khác.
            if (!active)
            {
                _remainingSeconds = 0f;
                _paused = false;
            }

            OnActiveChanged?.Invoke(active);
            RefreshAvailability();
        }

        private void HandleColorLockChanged(bool locked) => RefreshAvailability();

        private void HandleCreditsChanged(int remaining) => OnCreditsChanged?.Invoke(remaining);

        /// Ô cuối cùng của màn được tô là lúc booster hết việc. Nghe từng ô nghe thì phí,
        /// nhưng thân hàm chỉ là một phép so rồi thoát.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            RefreshAvailability();

            // Tô kín bảng thì tắt luôn, khỏi để đồng hồ chạy nốt trên một màn đã xong.
            if (IsActive && _paintService.IsComplete) Cancel();
        }

        private void RefreshAvailability()
        {
            var available = CanUse;
            if (available == _lastAvailability) return;

            _lastAvailability = available;
            OnAvailabilityChanged?.Invoke(available);
        }
    }
}
