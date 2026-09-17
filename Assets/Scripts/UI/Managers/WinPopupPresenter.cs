using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Managers
{
    /// Mở popup thắng màn khi luồng màn chơi báo đã tô xong.
    public class WinPopupPresenter : MonoBehaviour
    {
        private ILevelFlowService _levelFlow;
        private IPopupService _popupService;

        public void Init(ILevelFlowService levelFlow, IPopupService popupService)
        {
            _levelFlow = levelFlow;
            _popupService = popupService;

            _levelFlow.OnLevelCleared += HandleLevelCleared;
        }

        private void OnDestroy()
        {
            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
        }

        private void HandleLevelCleared() => _popupService.Show(PopupKey.LevelComplete);
    }
}
