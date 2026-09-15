using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Core.Services
{
    /// Thuần C#, không phải MonoBehaviour: nó chỉ giữ một cái cờ và đọc/ghi qua
    /// ISaveService, không có gì cần một chỗ đứng trong scene. Cùng khuôn với
    /// PlayerProgress, PlayerWallet, RatePrompt — container dựng bằng constructor.
    ///
    /// Mặc định BẬT. Rung là thứ người chơi quen có trên điện thoại; ai không thích thì
    /// tắt, còn mặc định tắt là gần như không ai biết nó tồn tại để mà bật.
    public class VibrationService : IVibrationService
    {
        private readonly ISaveService _save;

        private bool _isEnabled;

        public VibrationService(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));

            _isEnabled = _save.GetBool(PreferenceKeys.VibrationEnabled, true);
        }

        public bool IsEnabled => _isEnabled;

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;

            // Ghi NGAY, không đợi lúc thoát game. Người chơi tắt rung xong tắt app luôn
            // là chuyện thường, mà một cái công tắc không nhớ thì phải bấm lại mỗi lần mở.
            _save.SetBool(PreferenceKeys.VibrationEnabled, enabled);
            _save.Save();
        }

        /// CHƯA NỐI GÌ. Cố ý để trống, chờ quyết định rung ở những chỗ nào.
        ///
        /// Vẫn có mặt sẵn, và vẫn kiểm công tắc ở đây, vì hai lý do:
        ///
        /// - Chỗ gọi viết được ngay từ bây giờ. Tô trúng một ô, xong một màu, thắng màn —
        ///   những chỗ đó cứ gọi Vibrate(), và tới lúc thân hàm này có nội dung thì chúng
        ///   chạy đúng mà không phải sửa lại dòng nào.
        ///
        /// - Công tắc chỉ kiểm ở MỘT chỗ. Nếu để mỗi chỗ gọi tự hỏi IsEnabled thì sớm
        ///   muộn cũng có một chỗ quên, và cái quên đó chỉ lộ ra trên máy thật, ở đúng
        ///   người chơi đã cố ý tắt rung.
        ///
        /// Khi điền vào: Unity chỉ có Handheld.Vibrate() — một cú buzz dài cỡ nửa giây,
        /// quá nặng cho một cú chạm ô. Rung ngắn phải gọi thẳng Vibrator của Android qua
        /// AndroidJavaObject (có độ dài và biên độ), iOS thì cần một plugin nhỏ. Nói tôi
        /// biết lúc cần, đừng gọi Handheld.Vibrate cho việc tô ô.
        public void Vibrate()
        {
            if (!_isEnabled) return;
        }
    }
}
