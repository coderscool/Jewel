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

        /// Lời nhắc KHÔNG làm tối nền. Nó chỉ ghé qua vài giây rồi tự tắt, mà người chơi
        /// vẫn đang tô dở — tối cả màn hình cho một câu nhắc là chặn tay họ giữa chừng
        /// vì một thứ không đòi hỏi gì.
        public override bool DimsBackground => false;

        public override void Show()
        {
            _isFadingOut = false;

            // base.Show đặt lại alpha về 1, nên nhắc liên tiếp lúc đang mờ dần vẫn hiện
            // lại đầy đủ chứ không kế thừa độ mờ dở dang.
            base.Show();

            RestartAutoHide();
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
