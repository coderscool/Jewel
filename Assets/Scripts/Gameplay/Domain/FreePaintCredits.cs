using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số lượt dùng booster "tô tự do" còn lại. Xem CreditPool cho phần đếm và ghi.
    public class FreePaintCredits : CreditPool
    {
        public FreePaintCredits(ISaveService save, int startingCredits)
            : base(save, startingCredits, PreferenceKeys.FreePaintCredits, PreferenceKeys.FreePaintCreditsGranted)
        {
        }
    }
}
