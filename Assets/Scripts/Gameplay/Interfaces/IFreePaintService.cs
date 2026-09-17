using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho booster tô tự do.
    public interface IFreePaintService
    {
        bool CanUse { get; }

        bool IsActive { get; }

        float RemainingSeconds { get; }

        bool IsPaused { get; }

        /// Tạm dừng hoặc chạy tiếp đồng hồ.
        void SetPaused(bool paused);

        float DurationSeconds { get; }

        int RemainingCredits { get; }

        /// Dùng booster; false khi không dùng được.
        bool Use();

        event Action<bool> OnActiveChanged;

        event Action<int> OnCreditsChanged;

        event Action OnCreditsExhausted;

        event Action<bool> OnAvailabilityChanged;
    }
}
