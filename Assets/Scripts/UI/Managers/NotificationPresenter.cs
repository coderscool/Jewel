using JewelPainter.Gameplay.Domain;
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
    /// - Hai nhịp MỜI CHỌN MÀU của màn hướng dẫn (nhịp 1 và nhịp cuối), lúc ngón tay chỉ
    ///   vào ô màu → lời nhắc nằm cạnh ngón tay suốt nhịp đó, rút đi khi sang nhịp khác. Ở đây nó không còn là lời nhắc về một lỗi vừa
    ///   xảy ra, mà là một nửa của chính bài hướng dẫn: ngón tay nói "bấm vào đây", lời
    ///   nhắc nói "để làm gì". Tự tắt sau một giây thì người chơi mới — người đang nhìn
    ///   khắp màn hình chứ chưa biết nhìn vào đâu — rất dễ lỡ mất nó.
    public class NotificationPresenter : MonoBehaviour
    {
        private IPaintService _paintService;
        private IPopupService _popupService;
        private TutorialState _tutorialState;

        /// Lời nhắc đang được GIỮ cho màn hướng dẫn, không phải đang tự đếm giờ tắt.
        ///
        /// Cần cờ này để biết cú Hide lúc hướng dẫn rút đi là của mình: người chơi có thể
        /// đã lỡ tay một lần trước đó và popup đang nằm trong lượt đếm bình thường, tắt
        /// nhầm nó thì không hỏng gì, nhưng tắt một popup KHÁC đang mở thì có.
        private bool _isHoldingForTutorial;

        public void Init(IPaintService paintService, IPopupService popupService,
            TutorialState tutorialState)
        {
            _paintService = paintService;
            _popupService = popupService;
            _tutorialState = tutorialState;

            _paintService.OnColorRequired += HandleColorRequired;

            if (_tutorialState == null) return;

            _tutorialState.OnStageChanged += HandleTutorialStageChanged;

            // Bắt kịp trường hợp hướng dẫn đã hiện TRƯỚC khi lớp này được nối dây. Hiện
            // tại không xảy ra — hướng dẫn chờ 0.6 giây sau khi bảng dựng xong — nhưng nó
            // phụ thuộc vào một con số trong Inspector và vào thứ tự gọi Init ở
            // GameEntryPoint, hai thứ có thể đổi mà không ai nghĩ tới chỗ này.
            if (_tutorialState.Stage == TutorialStage.PickColor
                || _tutorialState.Stage == TutorialStage.PickNextColor)
            {
                ShowAndHold();
            }
        }

        private void OnDestroy()
        {
            if (_paintService != null) _paintService.OnColorRequired -= HandleColorRequired;

            if (_tutorialState != null) _tutorialState.OnStageChanged -= HandleTutorialStageChanged;
        }

        /// Lời nhắc đi cùng hai nhịp MỜI CHỌN MÀU, và chỉ hai nhịp đó.
        ///
        /// Nhịp 1 và nhịp 3 hỏi cùng một câu — "chọn màu đi" — nên cùng một tấm biển nói
        /// đúng cho cả hai. Nhịp 2 thì câu hỏi đã đổi thành "kéo tay qua mấy ô này": giữ
        /// nguyên biển cũ lúc đó là để một lời hướng dẫn SAI nằm trên màn hình, tệ hơn
        /// hẳn việc không có lời nào.
        ///
        /// So với danh sách nhịp chứ không so với "hướng dẫn có đang chạy không": ba nhịp
        /// nói ba việc khác nhau, mà chỉ hai trong số đó cần tới tấm biển này.
        private void HandleTutorialStageChanged(TutorialStage stage)
        {
            if (stage == TutorialStage.PickColor || stage == TutorialStage.PickNextColor)
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
            // Hướng dẫn đang giữ lời nhắc thì đừng Show lần nữa: nó sẽ mờ lại từ đầu —
            // một cú nháy ngay giữa lúc người chơi đang đọc, chẳng nói thêm được gì.
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
