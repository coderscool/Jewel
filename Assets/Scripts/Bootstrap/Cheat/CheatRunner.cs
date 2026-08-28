#if CHEAT_ENABLED
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Chỗ mượn coroutine cho JewelPainterCheatBridge.
    ///
    /// Bridge là class thuần C# — đó là chủ ý: nó chỉ map port sang API của game, không
    /// có lý do gì phải mang theo cả vòng đời của Unity. Nhưng "tô nốt 5000 ô" thì BẮT
    /// BUỘC phải rải qua nhiều frame, và rải frame thì cần một MonoBehaviour.
    ///
    /// Vì sao không tô hết trong một frame: mỗi ô là một lần bắn OnCellPainted, kéo theo
    /// một lần lộ màu, một lần gỡ marker gợi ý, một lần xin hiệu ứng hạt. Năm nghìn lần
    /// như thế trong một frame là máy đứng hình vài giây — và tệ hơn cả cú đứng là không
    /// ai NHÌN thấy bảng được tô, trong khi nhìn thấy mới đúng là việc của cheat này.
    ///
    /// Nên tách ra đúng một MonoBehaviour rỗng làm chỗ chạy coroutine, thay vì kéo cả
    /// bridge vào scene.
    [DisallowMultipleComponent]
    public class CheatRunner : MonoBehaviour
    {
        /// Sinh lúc chạy chứ không nằm sẵn trong scene — cùng lý do đã ghi ở WinPopupCheat:
        /// object trong scene sẽ để lại tham chiếu "Missing Script" ở bản build không có
        /// define CHEAT_ENABLED.
        public static CheatRunner Create()
        {
            var host = new GameObject(nameof(CheatRunner));
            DontDestroyOnLoad(host);

            return host.AddComponent<CheatRunner>();
        }
    }
}
#endif
