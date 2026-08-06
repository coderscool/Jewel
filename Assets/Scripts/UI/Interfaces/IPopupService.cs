using JewelPainter.UI.Definitions;
using JewelPainter.UI.Views;

namespace JewelPainter.UI.Interfaces
{
    public interface IPopupService
    {
        PopupView Show(PopupKey key);
        void Hide(PopupKey key);
        void HideAll();
    }
}
