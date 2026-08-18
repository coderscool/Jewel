#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Bấm W để mở lại popup thắng màn.
    ///
    /// Chỉnh nhịp của băng chúc mừng, tiền bay và nút Continue mà mỗi lần thử phải tô
    /// kín cả bảng thì không ai chỉnh nổi. Phím này cho xem lại toàn bộ chuỗi hiệu ứng
    /// trong một giây.
    ///
    /// CẢ FILE nằm trong #if nên nó không tồn tại trong build phát hành — không class,
    /// không Update, không có gì để quên gỡ. Và vì object tự sinh lúc chạy chứ không
    /// nằm sẵn trong scene, build phát hành cũng không có tham chiếu "Missing Script"
    /// nào trỏ tới nó.
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

            // Đóng rồi mới mở lại. Gọi Show đè lên popup đang hiện thì băng vẫn nằm đúng
            // chỗ cũ và cú rơi không có gì để rơi — nhìn ra như hiệu ứng bị hỏng.
            _popupService.Hide(PopupKey.LevelComplete);
            _popupService.Show(PopupKey.LevelComplete);
        }

        /// Tìm muộn, ngay lần bấm đầu tiên, chứ không tìm ở Start: object này sinh ra
        /// bằng RuntimeInitializeOnLoadMethod nên không có gì đảm bảo nó chạy sau khi
        /// VContainer dựng xong scene.
        ///
        /// FindAnyObjectByType ở đây hợp lệ: chạy đúng một lần, và code cheat không nên
        /// bắt Bootstrap khai thêm một phụ thuộc chỉ tồn tại lúc phát triển.
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
