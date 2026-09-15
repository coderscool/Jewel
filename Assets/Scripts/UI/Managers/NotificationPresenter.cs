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
    ///
    /// Lớp này giữ HAI đường mở popup, và chúng khác nhau ở nhịp chứ không ở nội dung:
    ///
    /// - Người chơi chạm bảng mà chưa chọn màu → nhắc một giây rồi tự rút. Đây là lời
    ///   nhắc cho một cú lỡ tay, và một cú lỡ tay không đáng bị đứng chắn đường.
    ///
    /// - Màn hướng dẫn đang chỉ ngón tay vào ô màu → lời nhắc nằm cạnh ngón tay suốt cả
    ///   quãng đó, rút đi cùng lúc với nó. Ở đây nó không còn là lời nhắc về một lỗi vừa
    ///   xảy ra, mà là một nửa của chính bài hướng dẫn: ngón tay nói "bấm vào đây", lời
    ///   nhắc nói "để làm gì". Tự tắt sau một giây thì người chơi mới — người đang nhìn
    ///   khắp màn hình chứ chưa biết nhìn vào đâu — rất dễ lỡ mất nó.
    public class NotificationPresenter : MonoBehaviour
    {
        [Tooltip("Hướng dẫn cho người chơi mới. Có gán thì lời nhắc hiện cùng ngón tay và " +
                 "nằm suốt màn hướng dẫn. Để trống thì chỉ còn đường nhắc thường.")]
        [SerializeField] private TutorialOverlayView _tutorial;

        private IPaintService _paintService;
        private IPopupService _popupService;

        /// Lời nhắc đang được GIỮ cho màn hướng dẫn, không phải đang tự đếm giờ tắt.
        ///
        /// Cần cờ này để biết cú Hide lúc hướng dẫn rút đi là của mình: người chơi có thể
        /// đã lỡ tay một lần trước đó và popup đang nằm trong lượt đếm bình thường, tắt
        /// nhầm nó thì không hỏng gì, nhưng tắt một popup KHÁC đang mở thì có.
        private bool _isHoldingForTutorial;

        public void Init(IPaintService paintService, IPopupService popupService)
        {
            _paintService = paintService;
            _popupService = popupService;

            _paintService.OnColorRequired += HandleColorRequired;

            if (_tutorial == null) return;

            _tutorial.OnShowingChanged += HandleTutorialShowingChanged;

            // Bắt kịp trường hợp hướng dẫn đã hiện TRƯỚC khi lớp này được nối dây. Hiện
            // tại không xảy ra — hướng dẫn chờ 0.6 giây sau khi bảng dựng xong — nhưng nó
            // phụ thuộc vào một con số trong Inspector và vào thứ tự gọi Init ở
            // GameEntryPoint, hai thứ có thể đổi mà không ai nghĩ tới chỗ này.
            if (_tutorial.IsShowing) ShowAndHold();
        }

        private void OnDestroy()
        {
            if (_paintService != null) _paintService.OnColorRequired -= HandleColorRequired;

            if (_tutorial != null) _tutorial.OnShowingChanged -= HandleTutorialShowingChanged;
        }

        /// Ngón tay hiện ra thì lời nhắc lên theo; ngón tay rút thì lời nhắc rút theo.
        private void HandleTutorialShowingChanged(bool showing)
        {
            if (showing)
            {
                ShowAndHold();
                return;
            }

            if (!_isHoldingForTutorial) return;

            _isHoldingForTutorial = false;

            _popupService.Hide(PopupKey.Notification);
        }

        private void HandleColorRequired()
        {
            // Hướng dẫn đang chạy thì lời nhắc đã nằm sẵn trên màn hình rồi. Gọi Show lần
            // nữa ở đây là bắt nó mờ lại từ đầu — một cú nháy ngay giữa lúc người chơi
            // đang đọc, mà chẳng nói thêm được điều gì.
            if (_isHoldingForTutorial) return;

            _popupService.Show(PopupKey.Notification);
        }

        /// Mở lời nhắc rồi gỡ hẹn giờ tự tắt của chính lần mở đó.
        ///
        /// Hai bước chứ không một, vì PopupManager là nơi duy nhất biết cách dựng popup
        /// lần đầu — nó phải Show trước thì mới có instance để gọi bước thứ hai.
        private void ShowAndHold()
        {
            var popup = _popupService.Show(PopupKey.Notification) as NotificationPopupView;

            if (popup == null)
            {
                // Không phải lỗi chết người: lời nhắc vẫn hiện, chỉ là nó tự tắt sau một
                // giây như mọi lời nhắc khác. Nhưng đây gần như chắc chắn là prefab gán
                // sai ở PopupConfig, và triệu chứng thì trông y hệt "hướng dẫn hơi nhấp
                // nháy" — không có dòng này thì không ai lần ra.
                Debug.LogWarning($"{nameof(NotificationPresenter)}: popup {PopupKey.Notification} " +
                                 $"không phải {nameof(NotificationPopupView)} nên không giữ lại " +
                                 "được. Kiểm tra prefab gán cho key đó trong PopupConfig.", this);
                return;
            }

            popup.KeepOpenUntilHidden();

            _isHoldingForTutorial = true;
        }
    }
}
