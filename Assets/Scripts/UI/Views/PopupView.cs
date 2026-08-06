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
