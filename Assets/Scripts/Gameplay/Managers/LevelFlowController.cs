using System;
using System.Collections;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Điều phối luồng thắng màn: phát hiện tô xong, ăn mừng rồi báo thắng.
    public class LevelFlowController : MonoBehaviour, ILevelFlowService
    {
        [Tooltip("Thời gian từ lúc WinCelebration chạy tới lúc hiện popup thắng màn.")]
        [SerializeField] private float _popupDelaySeconds = 1.85f;

        [Tooltip("Thời gian chờ sau khi dải loé của màu cuối tắt rồi mới mở màn ăn mừng.")]
        [SerializeField] private float _winCelebrationDelaySeconds = 0.25f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;
        private WinCelebration _winCelebration;
        private ColorCompleteSparkle _colorCompleteSparkle;

        private bool _hasAnnounced;

        private int _loadedLevel = -1;

        public event Action OnCelebrationStarted;

        public event Action OnLevelCleared;

        public int ClearedLevel { get; private set; } = -1;

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
            WinCelebration winCelebration,
            ColorCompleteSparkle colorCompleteSparkle)
        {
            _levelService = levelService;
            _paintService = paintService;
            _flyEffect = flyEffect;
            _winCelebration = winCelebration;
            _colorCompleteSparkle = colorCompleteSparkle;

            _flyEffect.OnJewelLanded += HandleJewelLanded;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        /// Ghi nhận đã qua màn.
        private void AdvanceProgress()
        {
            if (_levelService == null) return;

            _levelService.CompleteCurrentLevel();
        }

        /// Xử lý khi màn bắt đầu, kể cả màn đã tô kín sẵn.
        private void HandleLevelStarted(int levelId)
        {
            StopAllCoroutines();

            _hasAnnounced = false;
            ClearedLevel = -1;
            _loadedLevel = levelId;

            if (_paintService == null || !_paintService.IsComplete) return;

            if (_levelService.IsCompleted(levelId)) return;

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

            ClearedLevel = _loadedLevel >= 0 ? _loadedLevel : _levelService.CurrentLevel;

            var isReplay = _levelService.IsCompleted(ClearedLevel);

            if (!isReplay) AdvanceProgress();
            else _levelService.MarkLevelFinished(ClearedLevel);

            if (playCelebration)
            {
                StartCoroutine(CelebrateRoutine(announceAfter: !isReplay));
                return;
            }

            if (isReplay) return;

            StartCoroutine(AnnounceCleared());
        }

        /// Đợi dải loé của màu cuối tắt hẳn, nghỉ một nhịp, rồi mở màn ăn mừng.
        private IEnumerator CelebrateRoutine(bool announceAfter)
        {
            if (_colorCompleteSparkle != null)
            {
                yield return null;

                while (_colorCompleteSparkle.IsCelebrating) yield return null;
            }

            if (_winCelebrationDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_winCelebrationDelaySeconds);
            }

            if (announceAfter) OnCelebrationStarted?.Invoke();

            if (_winCelebration != null) _winCelebration.Play();

            WarnIfPopupMistimed();

            if (!announceAfter) yield break;

            yield return AnnounceCleared();
        }

        /// Cảnh báo khi popup thắng màn lệch nhịp với dải quét.
        private void WarnIfPopupMistimed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_winCelebration == null) return;

            var celebration = _winCelebration.CelebrationTotalSeconds;
            var gap = _popupDelaySeconds - celebration;

            if (gap > 0.35f)
            {
                Debug.LogWarning(
                    $"{nameof(LevelFlowController)}: Popup Delay Seconds ({_popupDelaySeconds:0.##}s) " +
                    $"dài hơn màn ăn mừng ({celebration:0.##}s) tới {gap:0.##}s — màn hình sẽ ĐỨNG CHẾT " +
                    $"đúng bằng chừng đó trước khi popup vào. Đặt khoảng {celebration + 0.15f:0.##} là vừa.",
                    this);

                return;
            }

            if (gap < -0.35f)
            {
                Debug.LogWarning(
                    $"{nameof(LevelFlowController)}: Popup Delay Seconds ({_popupDelaySeconds:0.##}s) " +
                    $"ngắn hơn màn ăn mừng ({celebration:0.##}s) {-gap:0.##}s — popup che mất phần cuối " +
                    $"màn ăn mừng. Cố ý thì bỏ qua, không thì đặt khoảng {celebration + 0.15f:0.##}.",
                    this);
            }
#endif
        }

        private IEnumerator AnnounceCleared()
        {
            if (_popupDelaySeconds > 0f) yield return new WaitForSeconds(_popupDelaySeconds);

            OnLevelCleared?.Invoke();
        }
    }
}
