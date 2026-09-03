using JewelPainter.UI.Definitions;
using JewelPainter.UI.Views;

namespace JewelPainter.UI.Interfaces
{
    public interface IPopupService
    {
        PopupView Show(PopupKey key);
        void Hide(PopupKey key);
        void HideAll();

        /// Có popup nào đang mở không. Dùng để biết màn hình đã sạch chưa trước khi chen
        /// một popup không do người chơi yêu cầu.
        bool IsAnyVisible();
    }
}
