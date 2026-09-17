using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số lượt gợi ý miễn phí còn lại.
    public class HintCredits : CreditPool
    {
        public HintCredits(ISaveService save, int startingCredits)
            : base(save, startingCredits, PreferenceKeys.HintCredits, PreferenceKeys.HintCreditsGranted)
        {
        }
    }
}
