#if CHEAT_ENABLED
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Chỗ mượn coroutine cho JewelPainterCheatBridge.
    [DisallowMultipleComponent]
    public class CheatRunner : MonoBehaviour
    {
        /// Sinh CheatRunner lúc chạy.
        public static CheatRunner Create()
        {
            var host = new GameObject(nameof(CheatRunner));
            DontDestroyOnLoad(host);

            return host.AddComponent<CheatRunner>();
        }
    }
}
#endif
