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
    /// KHÔNG tự sang màn kế. Người chơi bấm nút trong popup thì UI gọi GoToNextLevel.
    /// Nhờ vậy Gameplay không cần biết popup tồn tại, mà bức tranh vừa hoàn thành cũng
    /// được đứng yên bao lâu tuỳ người chơi.
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

        public bool IsLastLevel =>
            _levelService == null || !_levelService.HasLevel(_levelService.CurrentLevel + 1);

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

        /// Popup thắng màn gọi khi người chơi bấm nút.
        public void GoToNextLevel()
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
            _levelService.LoadLevel(_levelService.CurrentLevel);
        }

        private void HandleLevelStarted(int levelId)
        {
            StopAllCoroutines();
            _hasAnnounced = false;
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_hasAnnounced) return;
            if (!_paintService.IsComplete) return;

            _hasAnnounced = true;

            // Ăn mừng chạy NGAY, còn popup đếm giờ song song. Hai thứ độc lập nhau về
            // thời gian: đổi thời lượng dải quét không kéo theo lúc popup hiện, và
            // ngược lại.
            if (_winCelebration != null) _winCelebration.Play();

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
