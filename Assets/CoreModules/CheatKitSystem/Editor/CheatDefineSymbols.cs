using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace CoreModules.CheatKit.EditorTools
{
    /// <summary>
    /// Tiện ích bật/tắt define <c>CHEAT_ENABLED</c> trong PlayerSettings theo từng platform.
    /// KHÔNG gate (Editor asmdef thường) → luôn dùng được kể cả khi CheatKit đang TẮT.
    /// Thay đổi define → Unity tự recompile (asmdef CheatKit vào/ra theo đó).
    /// </summary>
    public static class CheatDefineSymbols
    {
        public const string Symbol = "CHEAT_ENABLED";

        /// <summary>Các platform phổ biến để áp dụng hàng loạt.</summary>
        public static readonly NamedBuildTarget[] CommonTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.WebGL,
        };

        /// <summary>Platform đang active trong Build Settings.</summary>
        public static NamedBuildTarget Active
        {
            get
            {
                var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                return NamedBuildTarget.FromBuildTargetGroup(group);
            }
        }

        public static bool IsDefined(NamedBuildTarget target) => GetSymbols(target).Contains(Symbol);

        /// <summary>Bật/tắt define cho 1 platform (no-op nếu đã đúng trạng thái).</summary>
        public static void Set(NamedBuildTarget target, bool enabled)
        {
            var symbols = GetSymbols(target);
            bool has = symbols.Contains(Symbol);
            if (enabled == has) return;

            if (enabled) symbols.Add(Symbol);
            else symbols.RemoveAll(s => s == Symbol);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        }

        private static List<string> GetSymbols(NamedBuildTarget target) =>
            PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
    }
}
