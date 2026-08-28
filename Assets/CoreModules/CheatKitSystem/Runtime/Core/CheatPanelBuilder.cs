using CoreModules.CheatKit.Modules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Dựng cheat panel runtime — KHÔNG cần prefab (tránh prefab mồ côi / vỡ ref khi đổi script).
    /// Tạo Canvas overlay (sorting cao) + nút toggle luôn hiển thị + container panel cuộn dọc, mỗi
    /// module tự <see cref="ICheatModuleUi.BuildDefaultUI"/>. Hierarchy dựng khi GameObject còn TẮT
    /// rồi mới bật → Awake/Initialize của panel chạy sau khi field UI của module đã gán xong.
    ///
    /// Trả về <see cref="UITestPanel"/> để installer của game gọi Bind(service).
    /// </summary>
    public static class CheatPanelBuilder
    {
        public static UITestPanel Build()
        {
            var cfg = CheatKitConfig.GetOrDefault();
            EnsureEventSystem();

            // Canvas (tắt trong lúc dựng để hoãn Awake/Initialize).
            var canvasGO = new GameObject("[CheatPanel]");
            canvasGO.SetActive(false);
            Object.DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760; // trên mọi UI game
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var canvasRT = (RectTransform)canvasGO.transform;

            // PanelRoot: lớp full-screen TRONG SUỐT (không chặn raycast) — đây là thứ UITestPanel
            // ẩn/hiện. Mở = tap 5 lần top-center (ScreenTapReveal mặc định). Chứa cả container lẫn
            // nút close → ẩn panel thì cả 2 biến mất; phần còn lại của màn hình KHÔNG bị chặn input.
            var panelRootGO = new GameObject("PanelRoot", typeof(RectTransform));
            var panelRootRT = (RectTransform)panelRootGO.transform;
            panelRootRT.SetParent(canvasRT, false);
            panelRootRT.anchorMin = Vector2.zero;
            panelRootRT.anchorMax = Vector2.one;
            panelRootRT.offsetMin = panelRootRT.offsetMax = Vector2.zero;

            // Nút 🐞 ĐÓNG panel — góc phải-trên (chỉ hiện khi panel mở vì nằm trong PanelRoot).
            var closeBtn = CheatUi.Button(panelRootRT, "🐞", new Color(0.55f, 0.20f, 0.20f, 0.95f), 30);
            var cRt = (RectTransform)closeBtn.transform;
            cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-16f, -16f);
            cRt.sizeDelta = new Vector2(96f, 96f);
            var closeLe = closeBtn.GetComponent<LayoutElement>();
            if (closeLe != null) closeLe.ignoreLayout = true;

            // Panel container (cuộn dọc các module), neo góc trái-trên.
            var panelGO = new GameObject("Container", typeof(RectTransform));
            var panelRT = (RectTransform)panelGO.transform;
            panelRT.SetParent(panelRootRT, false);
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(16f, -16f);
            panelRT.sizeDelta = new Vector2(560f, 0f);

            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.92f);
            var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fit = panelGO.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CheatUi.Label(panelRT, "CHEAT TOOL", 30, 52f);

            // Modules — bật/tắt theo config; mỗi cái 1 section tự dựng UI.
            if (cfg.includeLevelSelect) BuildModuleSection<LevelSelectModule>(panelRT);
            if (cfg.includeGameControl) BuildModuleSection<GameControlModule>(panelRT);
            if (cfg.includePerformance) BuildModuleSection<PerformanceStatsModule>(panelRT);

            // UITestPanel sống trên canvas (KHÔNG bị ẩn) → Update/reveal vẫn chạy khi panel đóng.
            // Root để ẩn/hiện = panelRootGO (gồm container + nút close).
            var uiPanel = canvasGO.AddComponent<UITestPanel>();
            uiPanel.SetPanelRootBeforeInit(panelRootGO);
            uiPanel.SetStartVisibleBeforeInit(cfg.startVisible);
            uiPanel.SetRevealGesture(new ScreenTapReveal(cfg.revealTaps, cfg.revealWindow, cfg.revealRegion));

            // 🐞 = ĐÓNG. Mở dùng cử chỉ tap 5 lần top-center (ScreenTapReveal mặc định trong UITestPanel).
            closeBtn.onClick.AddListener(uiPanel.Hide);

            canvasGO.SetActive(true); // → Awake → InitializePanel (auto-discover + init module)
            return uiPanel;
        }

        /// <summary>
        /// OCP SEAM — thêm một module GENRE vào panel ĐÃ dựng mà KHÔNG sửa builder/panel.
        /// Dựng section dưới Container, đăng ký + initialize ngay (panel đã qua InitializePanel nên
        /// không được trông chờ auto-discover nữa). Caller sau đó gọi <c>Bind(services)</c> như thường.
        /// Trả null nếu panel/Container không hợp lệ (graceful).
        /// </summary>
        public static T AddModule<T>(UITestPanel panel) where T : Component, ICheatModuleUi, IUITestModule
        {
            if (panel == null) return null;

            // Container = "PanelRoot/Container" do Build() dựng (VerticalLayoutGroup cuộn dọc module).
            var container = panel.transform.Find("PanelRoot/Container") as RectTransform;
            if (container == null)
            {
                CheatLog.Warning("CheatPanelBuilder.AddModule: không tìm thấy Container — panel chưa dựng đúng?");
                return null;
            }

            var module = BuildModuleSection<T>(container);
            panel.RegisterModule(module);
            module.Initialize(); // panel đã init → tự init module mới (không qua auto-discover).
            return module;
        }

        private static T BuildModuleSection<T>(RectTransform parent) where T : Component, ICheatModuleUi
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.padding = new RectOffset(10, 10, 10, 10);
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = true;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var module = go.AddComponent<T>();
            ((ICheatModuleUi)module).BuildDefaultUI(rt);
            return module;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            var es = new GameObject("[CheatEventSystem]");
            Object.DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>(); // legacy input — fallback nếu scene chưa có ES
        }
    }
}
