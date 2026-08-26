using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Views;
using UnityEngine;

namespace JewelPainter.UI.Managers
{
    /// Mở popup nhắc nhở khi Gameplay báo người chơi cần chọn màu.
    ///
    /// Cần một class riêng vì popup chỉ được PopupManager tạo ra ở lần mở đầu tiên —
    /// trước đó không có object nào của nó tồn tại để tự nghe sự kiện.
    public class NotificationPresenter : MonoBehaviour
    {
        [Tooltip("Hướng dẫn cho người chơi mới. Có gán thì lời nhắc im lặng trong lúc " +
                 "hướng dẫn đang hiện. Để trống thì lời nhắc luôn hiện.")]
        [SerializeField] private TutorialOverlayView _tutorial;

        private IPaintService _paintService;
        private IPopupService _popupService;

        public void Init(IPaintService paintService, IPopupService popupService)
        {
            _paintService = paintService;
            _popupService = popupService;

            _paintService.OnColorRequired += HandleColorRequired;
        }

        private void OnDestroy()
        {
            if (_paintService != null) _paintService.OnColorRequired -= HandleColorRequired;
        }

        private void HandleColorRequired()
        {
            // Hướng dẫn đang chỉ thẳng vào ô màu và nói đúng việc cần làm. Chồng thêm một
            // popup nói lại cùng điều đó thì nó che mất chính thứ ngón tay đang chỉ.
            if (_tutorial != null && _tutorial.IsShowing) return;

            _popupService.Show(PopupKey.Notification);
        }
    }
}
