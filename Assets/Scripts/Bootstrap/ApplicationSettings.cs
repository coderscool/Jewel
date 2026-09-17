using UnityEngine;
using UnityEngine.EventSystems;

namespace JewelPainter.Bootstrap
{
    /// Cấu hình toàn ứng dụng, chạy trước khi scene đầu tiên được nạp.
    public static class ApplicationSettings
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            QualitySettings.vSyncCount = 1;
#else
            QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = TargetFrameRate;
#endif

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private const int BaseDragThresholdPixels = 10;

        private const int MaxDragThresholdPixels = 28;

        /// Cấu hình EventSystem sau khi scene nạp xong.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureInput()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var dpi = Screen.dpi;
            if (dpi <= 0f) return;

            var scaled = Mathf.RoundToInt(BaseDragThresholdPixels * dpi / 160f);

            eventSystem.pixelDragThreshold =
                Mathf.Clamp(scaled, BaseDragThresholdPixels, MaxDragThresholdPixels);
        }
    }
}
