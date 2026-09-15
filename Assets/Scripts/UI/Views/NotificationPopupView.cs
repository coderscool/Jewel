using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Lời nhắc ngắn, tự tắt sau vài giây. Hiện nay chỉ dùng cho một việc: người chơi
    /// tô hoặc bấm gợi ý mà chưa chọn màu nào.
    ///
    /// Tự tắt chứ không bắt bấm nút đóng: đây là một câu nhắc, không phải một câu hỏi.
    /// Bắt người chơi bấm để bỏ qua thứ chính họ vừa gây ra là phạt họ hai lần.
    ///
    /// Lúc tắt thì mờ dần thay vì biến mất phựt một cái. Popup này xuất hiện đúng vào
    /// lúc người chơi đang thao tác dở, nên một cú tắt đột ngột đọc ra như game vừa giật.
    public class NotificationPopupView : PopupView
    {
        [Tooltip("Ảnh lời nhắc. Gán thẳng sprite trong prefab cũng được — ô này chỉ cần " +
                 "khi muốn đổi ảnh lúc chạy.")]
        [SerializeField] private Image _messageImage;

        [Tooltip("Tự tắt sau ngần này giây. Để 0 thì nằm mãi tới khi có ai gọi Hide.")]
        [SerializeField] private float _autoHideSeconds = 1f;

        [Tooltip("Thời gian mờ dần lúc tắt. Để 0 thì tắt ngay như cũ.\n\n" +
                 "Làm mờ cả CanvasGroup của popup nên ảnh lời nhắc và nền phía sau cùng " +
                 "nhạt đi một lượt — không cần đụng tới từng Image.")]
        [SerializeField] private float _fadeOutSeconds = 0.35f;

        /// Đang trong lượt mờ dần. Cần cờ này để Show biết mình vừa cắt ngang một cú tắt.
        private bool _isFadingOut;

        /// KHÔNG chặn chạm xuống màn chơi, dù ô tick ở lớp cha mặc định là có.
        ///
        /// Đây là popup duy nhất không phủ kín màn hình: nó nổi lên một góc rồi tự tắt
        /// sau một giây. Mà thứ khiến nó xuất hiện lại chính là người chơi đang tô — cắt
        /// tay họ một giây vì một câu nhắc do chính cú chạm đó gây ra là phạt hai lần.
        ///
        /// Override trong code chứ không bỏ tick trong prefab: đây là sự thật về cấu trúc
        /// của popup này, không phải một lựa chọn. Một ô tick thì ai dựng lại prefab cũng
        /// có thể quên, và lúc quên thì cả game khoá cứng mỗi lần hiện lời nhắc.
        public override bool BlocksBackground => false;

        public override void Show()
        {
            _isFadingOut = false;

            // base.Show đặt lại alpha về 1, nên nhắc liên tiếp lúc đang mờ dần vẫn hiện
            // lại đầy đủ chứ không kế thừa độ mờ dở dang.
            base.Show();

            RestartAutoHide();
        }

        /// Huỷ lượt đếm tự tắt của lần hiện ĐANG CHẠY: popup nằm lại cho tới khi có ai
        /// gọi Hide.
        ///
        /// Gọi NGAY SAU Show — nó không tự hiện popup, chỉ gỡ cái hẹn giờ mà Show vừa
        /// đặt. Tách ra thành một lời gọi thứ hai thay vì thêm một biến thể Show, vì
        /// "nằm lại bao lâu" là chuyện của NGƯỜI GỌI chứ không phải thuộc tính của
        /// popup: cùng một lời nhắc, lúc người chơi lỡ tay thì một giây là đủ, còn lúc
        /// nó đứng cạnh ngón tay hướng dẫn thì phải ở lại tới khi ngón tay rút đi.
        ///
        /// Không có cờ nào được giữ lại: lần Show kế tiếp lại đặt hẹn giờ như thường,
        /// nên không có trạng thái dính lại làm một lời nhắc bình thường bỗng nằm mãi.
        public void KeepOpenUntilHidden()
        {
            // Lượt đếm vừa được Show đặt là coroutine DUY NHẤT đang chạy trên object này
            // ở thời điểm này — Show đã dừng sạch mọi thứ trước đó.
            StopAllCoroutines();

            _isFadingOut = false;
        }

        /// Dùng khi muốn đổi ảnh trước lúc hiện. Gọi hàm này THAY CHO Show, không phải
        /// gọi sau — đổi ảnh sau khi đã hiện thì người chơi thấy loé một nhịp ảnh cũ.
        public void ShowMessage(Sprite message)
        {
            if (_messageImage != null && message != null) _messageImage.sprite = message;

            Show();
        }

        /// Mờ dần rồi mới tắt. Đường thoát thẳng vẫn còn cho các trường hợp không mờ được.
        public override void Hide()
        {
            if (_fadeOutSeconds <= 0f || !isActiveAndEnabled || CanvasGroup == null)
            {
                base.Hide();
                return;
            }

            if (_isFadingOut) return;

            StopAllCoroutines();
            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            _isFadingOut = true;

            // Ngắt tương tác NGAY từ đầu lượt mờ. Popup đang nhạt dần mà vẫn ăn cú chạm
            // là kiểu bực nhất: người chơi bấm vào thứ sắp biến mất và không hiểu vì sao
            // thao tác của mình rơi vào hư không.
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            var from = CanvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < _fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(elapsed / _fadeOutSeconds), Ease.InQuad);
                CanvasGroup.alpha = Mathf.Lerp(from, 0f, t);

                yield return null;
            }

            _isFadingOut = false;

            // base.Hide tắt GameObject, nên coroutine dừng ngay sau dòng này — không đặt
            // gì phía sau mà mong nó chạy.
            base.Hide();
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
            // Đếm bằng thời gian KHÔNG theo timeScale, để lời nhắc vẫn tự tắt kể cả khi
            // game đang bị một popup khác dừng lại.
            var elapsed = 0f;

            while (elapsed < _autoHideSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Hide();
        }
    }
}
