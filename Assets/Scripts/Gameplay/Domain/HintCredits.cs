using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số lượt gợi ý miễn phí còn lại.
    ///
    /// Toàn bộ phần đếm và ghi nằm ở CreditPool — lớp này chỉ nói kho của nó lưu ở key
    /// nào. Giữ lại thành một TÊN RIÊNG thay vì dùng thẳng CreditPool ở composition root:
    /// container phân giải theo kiểu, mà hai kho khác nhau cùng kiểu thì không phân giải
    /// nổi cái nào ra cái nào.
    public class HintCredits : CreditPool
    {
        public HintCredits(ISaveService save, int startingCredits)
            : base(save, startingCredits, PreferenceKeys.HintCredits, PreferenceKeys.HintCreditsGranted)
        {
        }
    }
}
