using System.Collections;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Nối ba mảnh đã có lại thành luồng thắng màn: phát hiện tô xong, chờ một nhịp,
    /// rồi sang màn kế.
    ///
    /// Tách khỏi LevelManager vì điều phối LUỒNG màn chơi là việc khác với giữ DỮ LIỆU
    /// màn chơi. LevelManager không cần biết điều gì khiến một màn kết thúc.
    public class LevelFlowController : MonoBehaviour
    {
        [Tooltip("Chờ bao lâu sau khi tô xong ô cuối rồi mới sang màn kế, tính bằng giây. " +
                 "Chừa một nhịp cho người chơi ngắm bức tranh vừa hoàn thành.")]
        [SerializeField] private float _delaySeconds = 1.5f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        private bool _isTransitioning;

        public void Init(ILevelService levelService, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _levelService = levelService;
            _paintService = paintService;
            _flyEffect = flyEffect;

            // Nghe lúc viên ngọc ĐÁP XUỐNG, không phải lúc bấm tô. Xét sớm thì màn
            // chuyển trong khi viên cuối vẫn đang bay giữa trời.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId)
        {
            StopAllCoroutines();
            _isTransitioning = false;
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_isTransitioning) return;
            if (!_paintService.IsComplete) return;

            _isTransitioning = true;
            StartCoroutine(GoToNextLevel());
        }

        private IEnumerator GoToNextLevel()
        {
            yield return new WaitForSeconds(_delaySeconds);

            var nextLevel = _levelService.CurrentLevel + 1;

            if (!_levelService.HasLevel(nextLevel))
            {
                // Màn cuối: KHÔNG tăng tiến trình. Tăng rồi thì lần mở game sau sẽ nạp
                // một màn không tồn tại và người chơi nhận được bảng trống.
                Debug.Log($"Đã hoàn thành màn cuối ({_levelService.CurrentLevel}). Không còn màn nào tiếp theo.");
                _isTransitioning = false;
                yield break;
            }

            _levelService.CompleteCurrentLevel();
            _levelService.LoadLevel(_levelService.CurrentLevel);

            _isTransitioning = false;
        }
    }
}
