using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Views;
using UnityEngine;

namespace JewelPainter.UI.Managers
{
    /// Mở popup nhắc nhở khi Gameplay báo người chơi cần chọn màu.
    public class NotificationPresenter : MonoBehaviour
    {
        private IPaintService _paintService;
        private IPopupService _popupService;
        private TutorialState _tutorialState;

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

        /// Hiện hoặc ẩn lời nhắc theo nhịp hướng dẫn.
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
            if (_isHoldingForTutorial) return;

            _popupService.Show(PopupKey.Notification);
        }

        /// Mở lời nhắc rồi gỡ hẹn giờ tự tắt của chính lần mở đó.
        private void ShowAndHold()
        {
            var popup = _popupService.Show(PopupKey.Notification) as NotificationPopupView;

            if (popup == null)
            {
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
