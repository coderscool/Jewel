using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số lượt dùng booster "tô hết màu đang chọn" còn lại.
    public class FillColorCredits : CreditPool
    {
        public FillColorCredits(ISaveService save, int startingCredits)
            : base(save, startingCredits, PreferenceKeys.FillColorCredits, PreferenceKeys.FillColorCreditsGranted)
        {
        }
    }
}
