using System.Diagnostics;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Logger nội bộ của CheatKit — giữ package ĐỘC LẬP, không phụ thuộc logger của game.
    ///
    /// Info/Warning bị strip ngoài Editor &amp; Development Build (qua <see cref="ConditionalAttribute"/>),
    /// nên 0 chi phí ở release. Error luôn giữ để lỗi cấu hình cheat vẫn lộ ra khi test trên device.
    /// Toàn bộ asmdef còn được gate thêm bởi define <c>CHEAT_ENABLED</c> → release không có byte nào.
    /// </summary>
    public static class CheatLog
    {
        private const string Tag = "[CheatKit] ";

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message) => UnityEngine.Debug.Log(Tag + message);

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(string message) => UnityEngine.Debug.LogWarning(Tag + message);

        public static void Error(string message) => UnityEngine.Debug.LogError(Tag + message);
    }
}
