using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Base cho mọi popup. Show/Hide bằng SetActive — KHÔNG Destroy,
    /// PopupManager giữ instance để tái dùng.
    /// Chưa dùng thư viện tween; muốn thêm animation thì override Show/Hide ở class con.
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        public bool IsVisible => gameObject.activeSelf;

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
            gameObject.SetActive(true);

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public virtual void Hide()
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            gameObject.SetActive(false);
        }
    }
}
