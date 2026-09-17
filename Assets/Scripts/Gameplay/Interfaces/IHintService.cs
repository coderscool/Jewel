using System;
using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho nút gợi ý.
    public interface IHintService
    {
        bool CanUseHint { get; }

        int RemainingCredits { get; }

        /// Dùng gợi ý; false khi không dùng được.
        bool UseHint();

        /// Gợi ý như bình thường nhưng không trừ lượt.
        bool FocusHintWithoutSpending(out Vector2Int cell);

        event Action<int> OnCreditsChanged;

        event Action OnCreditsExhausted;

        event Action<bool> OnHintAvailabilityChanged;
    }
}
