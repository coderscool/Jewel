using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace JewelPainter.UI.Managers
{
    /// Mở popup mời đánh giá sau mỗi vài màn.
    public class RatePopupPresenter : ITickable, IDisposable
    {
        private const float QuietSeconds = 0.6f;

        private readonly ILevelFlowService _levelFlow;
        private readonly IPopupService _popupService;
        private readonly RatePrompt _prompt;

        private bool _isPending;
        private float _quietElapsed;

        public RatePopupPresenter(
            ILevelFlowService levelFlow,
            IPopupService popupService,
            RatePrompt prompt)
        {
            _levelFlow = levelFlow;
            _popupService = popupService;
            _prompt = prompt;

            _levelFlow.OnLevelCleared += HandleLevelCleared;
        }

        /// Huỷ đăng ký sự kiện khi scope bị huỷ.
        public void Dispose()
        {
            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
        }

        private void HandleLevelCleared()
        {
            if (_prompt == null || !_prompt.RegisterLevelCleared()) return;

            _isPending = true;
            _quietElapsed = 0f;
        }

        public void Tick()
        {
            if (!_isPending) return;

            if (_popupService.IsAnyVisible())
            {
                _quietElapsed = 0f;
                return;
            }

            _quietElapsed += Time.unscaledDeltaTime;
            if (_quietElapsed < QuietSeconds) return;

            _isPending = false;
            _quietElapsed = 0f;

            _prompt.MarkPrompted();

            _popupService.Show(PopupKey.Rate);
        }
    }
}
