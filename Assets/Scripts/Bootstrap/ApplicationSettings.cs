using UnityEngine;

namespace JewelPainter.Bootstrap
{
    /// Cấu hình toàn ứng dụng, chạy trước khi scene đầu tiên được nạp.
    ///
    /// Dùng RuntimeInitializeOnLoadMethod thay vì một MonoBehaviour trong scene: không
    /// có ô nào để quên gán, không phụ thuộc thứ tự Awake, và chạy sớm hơn mọi thứ khác.
    /// Đổi lại các giá trị nằm trong code chứ không chỉnh được trong Inspector — chấp
    /// nhận được vì đây là cấu hình cả ứng dụng, không phải thứ tinh chỉnh theo màn.
    public static class ApplicationSettings
    {
        /// Unity mặc định **30fps trên mobile**, không phải "nhanh nhất có thể" như trên
        /// PC. Không đặt lại thì build lên điện thoại luôn khoá ở 30, và mọi phép đo
        /// hiệu năng trên máy thật đều lệch.
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            // Tắt vSync TRƯỚC: nó ghi đè targetFrameRate. Trên mobile vSync vốn bị bỏ
            // qua, nhưng trong Editor thì không — để nguyên sẽ làm bạn tưởng dòng dưới
            // không có tác dụng.
            QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = TargetFrameRate;

            // Game tô màu: người chơi hay ngồi ngắm hoặc nghĩ lâu mà không chạm màn hình,
            // mặc định máy sẽ tự tắt màn giữa chừng.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
