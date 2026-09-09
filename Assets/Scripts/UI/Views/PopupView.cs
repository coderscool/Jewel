using DG.Tweening;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Base cho mọi popup. Mờ dần vào và mờ dần ra, KHÔNG Destroy —
    /// PopupManager giữ instance để tái dùng.
    ///
    /// Muốn thêm chuyển động riêng thì override Show/Hide ở class con và gọi base.
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Thời gian mờ dần VÀO, tính bằng giây. Để 0 là hiện tức khắc như bản cũ.")]
        [SerializeField] private float _fadeInDuration = 0.15f;

        [Tooltip("Thời gian mờ dần RA. Để 0 là tắt tức khắc.\n\n" +
                 "Trong suốt quãng này popup đã KHÔNG nhận chạm nữa — nó chỉ còn là hình " +
                 "ảnh đang tan đi. Nhờ vậy bấm hai lần vào nút đóng không chạy hai lần.")]
        [SerializeField] private float _fadeOutDuration = 0.15f;

        /// Lượt mờ đang chạy. Mở lại giữa lúc đang đóng thì huỷ lượt cũ.
        private Tween _fade;

        /// Trạng thái LOGIC, không phải trạng thái của GameObject.
        ///
        /// Tách ra vì trong lúc mờ ra, object vẫn còn bật nhưng popup coi như đã đóng:
        /// PopupManager.HideAll duyệt qua và gọi Hide lần nữa lên chính nó, mà lần gọi đó
        /// sẽ khởi động lại cú mờ từ alpha đang dở.
        private bool _isShown;

        public bool IsVisible => _isShown;

        /// Popup này có làm tối nền phía sau không.
        ///
        /// Mặc định CÓ, vì phần lớn popup đòi người chơi dừng lại quyết định một việc —
        /// làm tối nền là cách nói "chỗ khác đợi đã". Popup nào chỉ ghé qua báo một tiếng
        /// rồi tự tắt thì override về false: làm tối cả màn hình cho một câu nhắc thoáng
        /// qua khiến nó nặng nề hơn hẳn thứ nó đáng có.
        public virtual bool DimsBackground => true;

        protected CanvasGroup CanvasGroup => _canvasGroup;

        public virtual void Show()
        {
            KillFade();

            _isShown = true;

            gameObject.SetActive(true);

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            var duration = Mathf.Max(0f, _fadeInDuration);

            if (duration <= 0f)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            _canvasGroup.alpha = 0f;
            _fade = FadeTo(1f, duration);
        }

        public virtual void Hide()
        {
            KillFade();

            // Đã đóng rồi thì chỉ chốt lại cho chắc. Không có nhánh này thì HideAll gọi
            // lên một popup đang mờ dở sẽ khởi động lại cú mờ từ giữa chừng.
            if (!_isShown)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            _isShown = false;

            // Khoá chạm NGAY, không đợi mờ xong: suốt quãng đang tan, popup chỉ còn là
            // hình ảnh. Bấm thêm lần nữa vào nút đóng vì thế không chạy thêm lần nào.
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var duration = Mathf.Max(0f, _fadeOutDuration);

            if (duration <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            _fade = FadeTo(0f, duration).OnComplete(() =>
            {
                _fade = null;

                // CanvasGroup nằm cùng object, nên nó chết là object cũng chết. Không có
                // chốt này thì đổi scene giữa lúc popup đang tan sẽ ném lỗi ở dòng dưới.
                if (_canvasGroup == null) return;

                gameObject.SetActive(false);
            });
        }

        /// DOVirtual.Float chứ KHÔNG phải CanvasGroup.DOFade.
        ///
        /// DOFade nằm trong DOTweenModuleUI.cs — một file .cs rời trong Plugins, nên nó
        /// biên dịch vào Assembly-CSharp, mà asmdef JewelPainter.UI không với tới assembly
        /// đó. Chỉ DOTween.dll là auto-reference. Cùng cái bẫy đã ghi ở WinPopupView.
        ///
        /// SetUpdate(true) — chạy theo thời gian KHÔNG phụ thuộc timeScale. Popup rất hay
        /// mở đúng lúc game bị dừng, mà một cú mờ đứng hình thì vô nghĩa.
        private Tween FadeTo(float target, float duration)
        {
            var from = _canvasGroup.alpha;

            return DOVirtual.Float(from, target, duration, value =>
                {
                    if (_canvasGroup != null) _canvasGroup.alpha = value;
                })
                .SetUpdate(true);
        }

        /// CỐ Ý không có OnDisable/OnDestroy ở lớp cha.
        ///
        /// Unity gọi mấy hàm vòng đời theo TÊN, không qua bảng ảo. Năm popup con đang khai
        /// `private void OnDestroy()` của riêng chúng, và một hàm cùng tên ở lớp cha sẽ bị
        /// che im lặng — Unity chỉ gọi bản con, phần dọn dẹp ở đây không bao giờ chạy. Đó
        /// là kiểu hỏng không có lỗi biên dịch nào chỉ ra.
        ///
        /// Không cần chúng: KillFade đã chạy ở đầu cả Show lẫn Hide, nên không có đường
        /// nào để hai lượt mờ chồng lên nhau. Còn tween sống sót qua lúc object bị tắt thì
        /// vô hại — lượt Show kế tiếp đặt lại alpha trước khi nó kịp ghi gì.
        private void KillFade()
        {
            if (_fade == null) return;

            if (_fade.IsActive()) _fade.Kill();

            _fade = null;
        }
    }
}
