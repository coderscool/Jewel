using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Popup báo một booster vừa mở khoá.
    public class BoosterUnlockPopupView : PopupView
    {
        [Tooltip("Nút đóng popup.")]
        [SerializeField] private Button _claimButton;

        private void Awake()
        {
            if (_claimButton != null) _claimButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_claimButton != null) _claimButton.onClick.RemoveListener(Hide);
        }
    }
}
