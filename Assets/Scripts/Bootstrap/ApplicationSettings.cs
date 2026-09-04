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
            // vSync xử lý KHÁC NHAU giữa máy thật và Editor, và gộp chung là chuốc lấy
            // hiện tượng giật nhẹ ở những vật chuyển động nhanh.
            //
            // Trên mobile: hệ điều hành tự đồng bộ theo nhịp quét màn hình, vSyncCount bị
            // bỏ qua hoàn toàn. Để 0 và giao việc giới hạn cho targetFrameRate là đúng.
            //
            // Trong Editor và trên PC thì vSyncCount CÓ tác dụng, và tắt nó là bỏ luôn
            // phần đồng bộ với màn hình. Unity chuyển sang giới hạn bằng cách ngủ cho đủ
            // 1/60 giây, nhưng thời điểm đẩy khung hình ra không còn khớp nhịp quét — cùng
            // một khoảng thời gian giữa hai frame lại rơi vào hai nhịp quét khác nhau.
            // Profiler vẫn báo 60fps đều tăm tắp, mà mắt thì thấy giật.
            //
            // Vật đứng yên hoặc đi chậm không lộ ra. Viên ngọc bay băng qua màn hình trong
            // nửa giây là thứ NHANH NHẤT trong game này, nên nó lộ ra đầu tiên.
#if UNITY_EDITOR || UNITY_STANDALONE
            // Nhịp khung hình khớp màn hình. Đổi lại targetFrameRate bị bỏ qua, nên máy
            // 144Hz sẽ chạy 144fps — muốn xem đúng cảm giác 60fps của điện thoại thì tạm
            // đổi hai dòng này về như nhánh dưới.
            QualitySettings.vSyncCount = 1;
#else
            // Tắt vSync TRƯỚC: nếu nền tảng có đọc tới nó thì nó ghi đè targetFrameRate.
            QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = TargetFrameRate;
#endif

            // Game tô màu: người chơi hay ngồi ngắm hoặc nghĩ lâu mà không chạm màn hình,
            // mặc định máy sẽ tự tắt màn giữa chừng.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
