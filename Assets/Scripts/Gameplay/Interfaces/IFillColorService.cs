using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho booster tô hết màu đang chọn.
    public interface IFillColorService
    {
        bool CanUse { get; }

        bool IsFilling { get; }

        float FillProgress { get; }

        int RemainingCredits { get; }

        /// Dùng booster; false khi không dùng được.
        bool Use();

        event Action<bool> OnFillingChanged;

        event Action<int> OnCreditsChanged;

        event Action OnCreditsExhausted;

        event Action<bool> OnAvailabilityChanged;
    }
}
