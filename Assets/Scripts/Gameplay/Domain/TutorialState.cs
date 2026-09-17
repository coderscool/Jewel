using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Trạng thái hướng dẫn của người chơi.
    public class TutorialState
    {
        private readonly ISaveService _save;
        private bool _hasPaintedOnce;

        public TutorialState(ISaveService save)
        {
            _save = save;
            _hasPaintedOnce = _save.GetBool(PreferenceKeys.HasPaintedOnce);
        }

        public bool HasPaintedOnce => _hasPaintedOnce;

        public TutorialStage Stage { get; private set; } = TutorialStage.None;

        public bool IsRunning => Stage != TutorialStage.None;

        public bool LocksInput =>
            Stage == TutorialStage.PickColor || Stage == TutorialStage.PaintCells;

        public event Action<TutorialStage> OnStageChanged;

        /// Đổi nhịp hướng dẫn hiện tại.
        public void SetStage(TutorialStage stage)
        {
            if (Stage == stage) return;

            Stage = stage;

            OnStageChanged?.Invoke(stage);
        }

        /// Ghi nhận một ô vừa được tô.
        public void MarkPainted()
        {
            if (_hasPaintedOnce) return;

            _hasPaintedOnce = true;

            _save.SetBool(PreferenceKeys.HasPaintedOnce, true);
            _save.Save();
        }
    }
}
