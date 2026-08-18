using System.Collections;
using TMPro;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Lời nhắc ngắn, tự tắt sau vài giây. Hiện nay chỉ dùng cho một việc: người chơi
    /// tô hoặc bấm gợi ý mà chưa chọn màu nào.
    ///
    /// Tự tắt chứ không bắt bấm nút đóng: đây là một câu nhắc, không phải một câu hỏi.
    /// Bắt người chơi bấm để bỏ qua thứ chính họ vừa gây ra là phạt họ hai lần.
    public class NotificationPopupView : PopupView
    {
        [Tooltip("Nội dung lời nhắc. Điền thẳng vào TMP trong prefab cũng được — ô này " +
                 "chỉ cần khi muốn đổi chữ lúc chạy.")]
        [SerializeField] private TMP_Text _messageText;

        [Tooltip("Tự tắt sau ngần này giây. Để 0 thì nằm mãi tới khi có ai gọi Hide.")]
        [SerializeField] private float _autoHideSeconds = 1.6f;

        public override void Show()
        {
            base.Show();

            RestartAutoHide();
        }

        /// Dùng khi muốn đổi chữ trước lúc hiện. Gọi hàm này THAY CHO Show, không phải
        /// gọi sau — đổi chữ sau khi đã hiện thì người chơi thấy loé một nhịp chữ cũ.
        public void ShowMessage(string message)
        {
            if (_messageText != null && !string.IsNullOrEmpty(message))
            {
                _messageText.SetText(message);
            }

            Show();
        }

        private void RestartAutoHide()
        {
            // Dừng lượt đếm cũ trước: nhắc liên tiếp mấy lần thì lần sau phải gia hạn
            // thời gian hiện, không phải để lượt đếm đầu tiên tắt popup giữa chừng.
            StopAllCoroutines();

            if (_autoHideSeconds <= 0f) return;
            if (!isActiveAndEnabled) return;

            StartCoroutine(AutoHideRoutine());
        }

        private IEnumerator AutoHideRoutine()
        {
            yield return new WaitForSeconds(_autoHideSeconds);

            Hide();
        }
    }
}
