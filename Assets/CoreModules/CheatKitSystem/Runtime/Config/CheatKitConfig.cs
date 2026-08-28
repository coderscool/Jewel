using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Cấu hình runtime cho CheatKit (asmdef KHÔNG gate CHEAT_ENABLED → Editor đọc/sửa được kể cả
    /// khi cheat đang TẮT). Chỉ là DATA — không kéo theo logic cheat nào vào release.
    ///
    /// Đặt asset trong một thư mục <c>Resources</c> bất kỳ với tên "CheatKitConfig" để
    /// <see cref="CheatPanelBuilder"/> tự nạp. Không có asset → dùng giá trị mặc định.
    /// </summary>
    [CreateAssetMenu(menuName = "CoreModules/CheatKit Config", fileName = "CheatKitConfig", order = 0)]
    public sealed class CheatKitConfig : ScriptableObject
    {
        public const string ResourcePath = "CheatKitConfig";

        [Header("Reveal — mở panel")]
        [Tooltip("Số lần chạm liên tiếp để mở panel.")]
        [Range(2, 10)] public int revealTaps = 5;

        [Tooltip("Khoảng thời gian (giây) cho phép hoàn tất chuỗi chạm.")]
        [Range(0.3f, 5f)] public float revealWindow = 1.5f;

        [Tooltip("Vùng nhận chạm (toạ độ màn hình chuẩn hoá 0..1; mặc định dải top-center).")]
        public Rect revealRegion = new Rect(0.30f, 0.82f, 0.40f, 0.18f);

        [Header("Panel")]
        [Tooltip("Hiện panel ngay khi vào game (thường để false — mở bằng cử chỉ).")]
        public bool startVisible = false;

        [Header("Modules — bật/tắt từng module")]
        public bool includeLevelSelect = true;
        public bool includeGameControl = true;
        public bool includePerformance = true;

        /// <summary>Nạp asset từ Resources; null → trả instance mặc định (không cần asset vẫn chạy).</summary>
        public static CheatKitConfig GetOrDefault()
        {
            var cfg = Resources.Load<CheatKitConfig>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<CheatKitConfig>();
        }
    }
}
