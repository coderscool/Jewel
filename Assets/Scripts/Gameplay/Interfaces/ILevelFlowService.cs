using System;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract cho luồng thắng màn.
    public interface ILevelFlowService
    {
        event Action OnCelebrationStarted;

        event Action OnLevelCleared;

        bool IsLastLevel { get; }

        int ClearedLevel { get; }
    }
}
