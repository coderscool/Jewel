using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Managers
{
    /// Mở popup nhắc nhở khi Gameplay báo người chơi cần chọn màu.
    ///
    /// Cần một class riêng vì popup chỉ được PopupManager tạo ra ở lần mở đầu tiên —
    /// trước đó không có object nào của nó tồn tại để tự nghe sự kiện.
    public class NotificationPresenter : MonoBehaviour
    {
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

        private void HandleColorRequired() => _popupService.Show(PopupKey.Notification);
    }
}
