using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Popup báo một booster vừa mở khoá.
    ///
    /// MỘT script dùng cho CẢ BA prefab. Ba popup chỉ khác nhau ở ảnh và chữ — thứ nằm
    /// trong prefab — nên không có gì để tách thành ba class. Chúng chỉ cần chung một
    /// hành vi: bấm nút thì đóng.
    ///
    /// BoosterUnlockPresenter quyết định KHI NÀO mở và mở cái nào; view này không biết
    /// mình đang nói về booster nào, và cũng không cần biết.
    public class BoosterUnlockPopupView : PopupView
    {
        [Tooltip("Nút đóng — thường ghi \"Nhận\" hoặc \"OK\".\n\n" +
                 "Để trống cũng chạy, nhưng lúc đó popup không có đường ra nào: nó không " +
                 "tự tắt, và nền mờ phía sau nuốt mọi cú chạm ra ngoài. Chỉ bỏ trống khi " +
                 "trong prefab đã có nút khác tự nối vào Hide.")]
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
