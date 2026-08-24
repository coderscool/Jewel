using JewelPainter.UI.Views;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JewelPainter.Bootstrap
{
    /// Phím tắt chạy lại hiệu ứng ăn mừng, để chỉnh nhịp mà không phải tô hết một màn.
    ///
    /// Nằm ở Bootstrap chứ không ở UI vì hai lý do, cả hai đều là quy ước của project:
    /// code cheat được phép sống ở đây, và chỉ assembly này mới tham chiếu sẵn cả
    /// JewelPainter.UI lẫn Unity.InputSystem. Đặt ở UI thì phải khai thêm Unity.InputSystem
    /// cho cả tầng UI — thêm một phụ thuộc thật cho một thứ chỉ dùng lúc chỉnh tay.
    public class CelebrationCheat : MonoBehaviour
    {
        [Tooltip("Màn hình Home cần chạy thử. Kéo object có HomeScreenView vào đây.")]
        [SerializeField] private HomeScreenView _home;

        [Tooltip("Phím bấm để chạy lại hiệu ứng.")]
        [SerializeField] private Key _key = Key.C;

        private bool _hasWarned;

        // Chỉ tồn tại trong Editor và bản Development. Bản phát hành không phải hỏi bàn
        // phím mỗi frame cho một thứ người chơi không bao giờ chạm tới.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Máy không có bàn phím — điện thoại, hoặc Editor lúc chưa focus cửa sổ.
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!keyboard[_key].wasPressedThisFrame) return;

            if (_home == null)
            {
                WarnOnce($"{nameof(CelebrationCheat)} chưa gán Home nên phím {_key} không làm gì.");
                return;
            }

            // HomeScreenView tự lo phần "Home đang đóng" và "chưa có màn nào xong",
            // và tự báo ra Console. Ở đây không đoán hộ nó.
            _home.ReplayCelebration();
        }
#endif

        private void WarnOnce(string message)
        {
            if (_hasWarned) return;

            _hasWarned = true;
            Debug.LogWarning(message, this);
        }
    }
}
