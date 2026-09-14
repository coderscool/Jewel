using UnityEngine;
using UnityEngine.EventSystems;

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

        /// Ngưỡng "thế nào là KÉO chứ không phải CHẠM", tính bằng pixel màn hình.
        ///
        /// Unity để cứng 10 pixel, và con số đó ra đời từ thời màn hình ~160 dpi. Trên
        /// điện thoại 440 dpi thì 10 pixel chỉ là **0.6 milimét** — ngón tay không ai giữ
        /// yên nổi trong ngần ấy. Hệ quả: rất nhiều cú chạm vào ô màu bị EventSystem xếp
        /// thành cú kéo, ScrollRect nuốt mất, và Button không bao giờ nhận được
        /// OnPointerClick. Người chơi thấy "bấm mà không ăn", bấm lại lần hai.
        ///
        /// Quy đổi theo dpi thì ngưỡng luôn tương đương một khoảng cách VẬT LÝ, và máy
        /// nào cũng cần đúng một cái vẩy tay như nhau mới tính là kéo.
        ///
        /// Đánh đổi: ngưỡng cao thì thanh cuộn "ì" hơn một chút ở đầu nét kéo — phải đi
        /// xa hơn mới bắt đầu trượt. Với thanh màu thì đổi thế là lời: chạm chọn màu là
        /// thao tác chính, cuộn chỉ là phụ.
        private const int BaseDragThresholdPixels = 10;

        /// Chặn trên cho ngưỡng. Máy dpi rất cao mà cứ nhân thẳng thì phải kéo gần nửa
        /// centimet mới trượt được, và lúc đó thanh cuộn mới là thứ hỏng.
        private const int MaxDragThresholdPixels = 28;

        /// AfterSceneLoad chứ không phải BeforeSceneLoad: EventSystem là một object TRONG
        /// scene, trước mốc đó nó chưa tồn tại và EventSystem.current còn là null.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureInput()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            // Screen.dpi trả 0 trên khá nhiều máy Android không khai báo — giữ nguyên mặc
            // định còn hơn nhân với 0 rồi khoá cứng mọi cú kéo.
            var dpi = Screen.dpi;
            if (dpi <= 0f) return;

            var scaled = Mathf.RoundToInt(BaseDragThresholdPixels * dpi / 160f);

            eventSystem.pixelDragThreshold =
                Mathf.Clamp(scaled, BaseDragThresholdPixels, MaxDragThresholdPixels);
        }
    }
}
