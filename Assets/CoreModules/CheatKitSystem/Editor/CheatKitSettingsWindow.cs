using UnityEditor;
using UnityEngine;

namespace CoreModules.CheatKit.EditorTools
{
    /// <summary>
    /// Cửa sổ bật/tắt nhanh CheatKit + chỉnh <see cref="CheatKitConfig"/>.
    /// Menu: <b>CoreModules ▸ CheatKit ▸ Settings</b>. Toggle nhanh: <b>CoreModules ▸ CheatKit ▸ Enabled (Active Platform)</b>.
    /// </summary>
    public sealed class CheatKitSettingsWindow : EditorWindow
    {
        private const string QuickMenu = "CoreModules/CheatKit/Enabled (Active Platform)";

        private bool _allPlatforms;
        private Vector2 _scroll;
        private UnityEditor.Editor _cfgEditor;

        [MenuItem("CoreModules/CheatKit/Settings", false, 0)]
        public static void Open()
        {
            var w = GetWindow<CheatKitSettingsWindow>("CheatKit");
            w.minSize = new Vector2(380f, 360f);
            w.Show();
        }

        // ── Toggle nhanh (có dấu check trong menu) ─────────────────────────
        [MenuItem(QuickMenu, false, 1)]
        private static void QuickToggle()
        {
            var t = CheatDefineSymbols.Active;
            CheatDefineSymbols.Set(t, !CheatDefineSymbols.IsDefined(t));
        }

        [MenuItem(QuickMenu, true)]
        private static bool QuickToggleValidate()
        {
            Menu.SetChecked(QuickMenu, CheatDefineSymbols.IsDefined(CheatDefineSymbols.Active));
            return true;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("CheatKit", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var active = CheatDefineSymbols.Active;
            bool enabled = CheatDefineSymbols.IsDefined(active);

            EditorGUILayout.HelpBox(
                enabled
                    ? $"ĐANG BẬT cho '{active}'  (CHEAT_ENABLED)"
                    : $"ĐANG TẮT cho '{active}'",
                enabled ? MessageType.Info : MessageType.Warning);

            _allPlatforms = EditorGUILayout.ToggleLeft(
                "Áp dụng mọi platform (Standalone/Android/iOS/WebGL)", _allPlatforms);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !enabled || _allPlatforms;
                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                if (GUILayout.Button("BẬT CheatKit", GUILayout.Height(36))) Apply(true);

                GUI.enabled = enabled || _allPlatforms;
                GUI.backgroundColor = new Color(0.92f, 0.55f, 0.55f);
                if (GUILayout.Button("TẮT CheatKit", GUILayout.Height(36))) Apply(false);

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(10);
            DrawConfigSection();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Build: template '🧪 Testing' (BuildPipelineSystem) đã tự thêm CHEAT_ENABLED.\n" +
                "Window này để bật/tắt NHANH khi làm việc/test trong Editor.\n" +
                "Đổi define sẽ khiến Unity biên dịch lại (asmdef CheatKit vào/ra theo đó).",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void Apply(bool on)
        {
            if (_allPlatforms)
            {
                foreach (var t in CheatDefineSymbols.CommonTargets)
                    CheatDefineSymbols.Set(t, on);
            }
            else
            {
                CheatDefineSymbols.Set(CheatDefineSymbols.Active, on);
            }
            GUIUtility.ExitGUI(); // tránh vẽ tiếp khi sắp recompile
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Config (runtime)", EditorStyles.boldLabel);

            var cfg = FindConfig();
            if (cfg == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có CheatKitConfig. Tạo để tinh chỉnh reveal/module (không bắt buộc — " +
                    "không có thì dùng mặc định).", MessageType.None);
                if (GUILayout.Button("Tạo CheatKitConfig asset")) CreateConfig();
                return;
            }

            if (_cfgEditor == null || _cfgEditor.target != cfg)
            {
                if (_cfgEditor != null) DestroyImmediate(_cfgEditor);
                _cfgEditor = UnityEditor.Editor.CreateEditor(cfg);
            }
            _cfgEditor.OnInspectorGUI();

            if (GUILayout.Button("Chọn asset trong Project")) Selection.activeObject = cfg;
        }

        private static CheatKitConfig FindConfig()
        {
            var loaded = Resources.Load<CheatKitConfig>(CheatKitConfig.ResourcePath);
            if (loaded != null) return loaded;

            var guids = AssetDatabase.FindAssets("t:CheatKitConfig");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<CheatKitConfig>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static void CreateConfig()
        {
            const string dir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var cfg = CreateInstance<CheatKitConfig>();
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/CheatKitConfig.asset");
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log($"[CheatKit] Đã tạo config: {path}");
        }

        private void OnDisable()
        {
            if (_cfgEditor != null) DestroyImmediate(_cfgEditor);
        }
    }
}
