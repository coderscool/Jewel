using System;
using System.Collections;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Nối các mảnh đã có lại thành luồng thắng màn: phát hiện tô xong, chạy màn ăn
    /// mừng, chờ một nhịp, rồi báo ra ngoài rằng đã thắng.
    ///
    /// Ghi nhận đã qua màn NGAY lúc phát hiện tô xong, không đợi người chơi bấm nút —
    /// tô xong là đã xong. Nút trong popup chỉ còn việc điều hướng về Home, nên thoát
    /// game trong lúc popup đang mở vẫn giữ được màn vừa thắng.
    ///
    /// KHÔNG tự NẠP màn kế. Gameplay vì thế không cần biết popup hay Home tồn tại.
    ///
    /// Tách khỏi LevelManager vì điều phối LUỒNG màn chơi là việc khác với giữ DỮ LIỆU
    /// màn chơi. LevelManager không cần biết điều gì khiến một màn kết thúc.
    public class LevelFlowController : MonoBehaviour, ILevelFlowService
    {
        [Tooltip("Bao lâu kể từ lúc viên ngọc cuối đáp xuống thì hiện popup thắng màn, " +
                 "tính bằng giây. Đây là con số TUYỆT ĐỐI, không cộng dồn với thời lượng " +
                 "của WinCelebration.\n\n" +
                 "Đặt ngắn hơn màn ăn mừng thì popup hiện đè lên lúc dải lấp lánh còn " +
                 "đang quét — đôi khi đó lại là thứ bạn muốn.")]
        [SerializeField] private float _popupDelaySeconds = 2f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;
        private WinCelebration _winCelebration;

        /// Đã báo thắng cho màn này rồi. Giữ lại vì OnJewelLanded còn nổ thêm vài lần
        /// nữa sau ô cuối, khi những viên đang bay lần lượt đáp xuống.
        private bool _hasAnnounced;

        public event Action OnLevelCleared;

        public int ClearedLevel { get; private set; } = -1;

        /// Hỏi theo MÀN VỪA XONG, không theo màn hiện tại: lúc popup mở thì tiến trình
        /// đã nhích sang màn kế rồi, nên đọc CurrentLevel + 1 là hỏi về màn sau nữa.
        public bool IsLastLevel
        {
            get
            {
                if (_levelService == null) return true;

                var reference = ClearedLevel >= 0 ? ClearedLevel : _levelService.CurrentLevel;

                return !_levelService.HasLevel(reference + 1);
            }
        }

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            JewelFlyEffect flyEffect,
            WinCelebration winCelebration)
        {
            _levelService = levelService;
            _paintService = paintService;
            _flyEffect = flyEffect;
            _winCelebration = winCelebration;

            // Nghe lúc viên ngọc ĐÁP XUỐNG, không phải lúc bấm tô. Xét sớm thì màn ăn
            // mừng bắt đầu trong khi viên cuối vẫn đang bay giữa trời.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        /// Ghi nhận đã qua màn. Chạy NGAY lúc phát hiện tô xong, không đợi người chơi bấm.
        ///
        /// Tô xong là đã xong — bấm nút chỉ là chuyện đi tiếp. Để việc ghi nhận nằm sau
        /// cú bấm thì người chơi thắng màn rồi thoát game trong lúc popup đang mở sẽ mất
        /// trắng màn vừa qua, và lần mở sau lại phải tô lại từ đầu.
        private void AdvanceProgress()
        {
            if (_levelService == null) return;

            var nextLevel = _levelService.CurrentLevel + 1;

            if (!_levelService.HasLevel(nextLevel))
            {
                // Màn cuối: KHÔNG tăng tiến trình. Tăng rồi thì lần mở game sau sẽ nạp
                // một màn không tồn tại và người chơi nhận được bảng trống.
                Debug.Log($"Đã hoàn thành màn cuối ({_levelService.CurrentLevel}). " +
                          "Không còn màn nào tiếp theo.");
                return;
            }

            _levelService.CompleteCurrentLevel();
        }

        /// Bắt lại trường hợp màn mở ra ĐÃ tô kín sẵn.
        ///
        /// Xảy ra khi người chơi tô xong ô cuối rồi thoát game trước khi bấm nút trong
        /// popup: tiến trình chưa nhích, nhưng bản lưu những ô đã tô thì đã ghi đủ. Lần
        /// mở sau, màn cũ hiện ra hoàn thiện mà KHÔNG còn ô nào để tô — OnJewelLanded
        /// không bao giờ nổ nữa, nên popup không bao giờ hiện và không còn đường nào sang
        /// màn kế. Người chơi kẹt vĩnh viễn ở một bức tranh đã xong.
        ///
        /// Ở đây chỉ mở lại popup, KHÔNG chạy WinCelebration: dải quét và cú thu camera
        /// là phần thưởng cho khoảnh khắc vừa đặt viên ngọc cuối, chiếu lại ở một phiên
        /// khác thì nó chỉ là quãng chờ.
        ///
        /// Vẫn đi qua nút bấm chứ không tự sang màn: luật "chỉ nhích tiến trình khi người
        /// chơi bấm" giữ nguyên.
        private void HandleLevelStarted(int levelId)
        {
            StopAllCoroutines();

            _hasAnnounced = false;
            ClearedLevel = -1;

            // PaintManager Init TRƯỚC lớp này nên nó đã khôi phục xong trạng thái tô lúc
            // handler này chạy. Thứ tự đó được cắm ở GameEntryPoint.
            if (_paintService == null || !_paintService.IsComplete) return;

            // KHÔNG chạy WinCelebration: dải quét và cú thu camera là phần thưởng cho
            // khoảnh khắc vừa đặt viên ngọc cuối. Chiếu lại ở một phiên khác thì nó chỉ
            // còn là quãng chờ.
            BeginClear(playCelebration: false);
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_hasAnnounced) return;
            if (!_paintService.IsComplete) return;

            BeginClear(playCelebration: true);
        }

        private void BeginClear(bool playCelebration)
        {
            _hasAnnounced = true;

            // Chụp lại TRƯỚC khi nhích: sau AdvanceProgress thì CurrentLevel đã là màn kế,
            // mà popup và màn ăn mừng ở Home đều cần biết màn nào vừa xong.
            ClearedLevel = _levelService.CurrentLevel;

            AdvanceProgress();

            // Ăn mừng chạy NGAY, còn popup đếm giờ song song. Hai thứ độc lập nhau về
            // thời gian: đổi thời lượng dải quét không kéo theo lúc popup hiện, và
            // ngược lại.
            if (playCelebration && _winCelebration != null) _winCelebration.Play();

            StartCoroutine(AnnounceCleared());
        }

        private IEnumerator AnnounceCleared()
        {
            // Đếm thẳng một con số thay vì đợi WinCelebration báo xong. Đổi lại là bạn
            // phải tự canh nó với thời lượng màn ăn mừng, nhưng bù lại thời điểm popup
            // hiện ra nằm gọn trong một ô Inspector chứ không phải suy từ ba ô khác.
            if (_popupDelaySeconds > 0f) yield return new WaitForSeconds(_popupDelaySeconds);

            OnLevelCleared?.Invoke();
        }
    }
}
