#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Bấm W để mở lại popup thắng màn.
    public class WinPopupCheat : MonoBehaviour
    {
        private const Key ReplayKey = Key.W;

        private IPopupService _popupService;
        private bool _hasWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject(nameof(WinPopupCheat));
            host.AddComponent<WinPopupCheat>();

            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!keyboard[ReplayKey].wasPressedThisFrame) return;

            if (!TryResolvePopupService()) return;

            _popupService.Hide(PopupKey.LevelComplete);
            _popupService.Show(PopupKey.LevelComplete);
        }

        /// Tìm popup service ở lần bấm đầu tiên.
        private bool TryResolvePopupService()
        {
            if (_popupService != null) return true;

            _popupService = FindAnyObjectByType<PopupManager>();

            if (_popupService != null) return true;

            if (!_hasWarned)
            {
                _hasWarned = true;
                Debug.LogWarning($"{nameof(WinPopupCheat)}: không tìm thấy PopupManager trong scene.");
            }

            return false;
        }
    }
}
#endif
