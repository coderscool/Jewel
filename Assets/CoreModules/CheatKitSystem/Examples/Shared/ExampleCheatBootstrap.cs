using CoreModules.CheatKit.Ports;
using UnityEngine;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// COMPOSITION ROOT của một example scene — điểm DUY NHẤT biết cả mock-game lẫn CheatKit.
    /// Bấm Play: dựng panel cheat (runtime, no-prefab), tạo mock-game + bridge theo <see cref="_genre"/>,
    /// bind vào module. Mở panel: chạm 5 lần vùng giữa-trên hoặc phím F1.
    ///
    /// Genre RPG/Action còn append module ĐẶC THÙ qua <see cref="CheatPanelBuilder.AddModule{T}"/>
    /// (OCP — không sửa kit) rồi truyền custom port vào <see cref="CheatServices"/>.extras.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExampleCheatBootstrap : MonoBehaviour
    {
        [Tooltip("Dòng game của scene này — quyết định cách wire mock-game + module.")]
        [SerializeField] private GenreKind _genre = GenreKind.Puzzle;

        private void Start()
        {
            var panel = FindObjectOfType<UITestPanel>(true);
            if (panel == null) panel = CheatPanelBuilder.Build();

            switch (_genre)
            {
                case GenreKind.Puzzle: WirePuzzle(panel); break;
                case GenreKind.Casual: WireCasual(panel); break;
                case GenreKind.Rpg: WireRpg(panel); break;
                case GenreKind.Action: WireAction(panel); break;
            }

            CheatLog.Info($"[CheatExample:{_genre}] Sẵn sàng — chạm 5 lần vùng giữa-trên hoặc F1 để mở panel.");
        }

        private static void WirePuzzle(UITestPanel panel)
        {
            var bridge = new PuzzleCheatBridge(new MockGameModel(levelCount: 20, coins: 500));
            BindAll(panel, new CheatServices(level: bridge, flow: bridge, progress: bridge));
        }

        private static void WireCasual(UITestPanel panel)
        {
            var bridge = new CasualCheatBridge(new MockGameModel(levelCount: 0, coins: 300, endless: true));
            BindAll(panel, new CheatServices(level: bridge, flow: bridge, progress: bridge));
        }

        private static void WireRpg(UITestPanel panel)
        {
            var bridge = new RpgCheatBridge(new RpgMockGame());
            CheatPanelBuilder.AddModule<RpgCheatModule>(panel); // OCP: thêm module genre, không sửa kit
            // bridge cũng là IRpgCheatService → đẩy vào extras cho module RPG lấy qua Get<T>().
            BindAll(panel, new CheatServices(level: bridge, flow: bridge, progress: bridge, bridge));
        }

        private static void WireAction(UITestPanel panel)
        {
            var bridge = new ActionCheatBridge(new ActionMockGame());
            CheatPanelBuilder.AddModule<ActionCheatModule>(panel);
            // ISP: game không có tiền tệ → progress: null (panel tự disable phần coin/progress).
            BindAll(panel, new CheatServices(level: bridge, flow: bridge, progress: null, bridge));
        }

        private static void BindAll(UITestPanel panel, CheatServices services)
        {
            var bindables = panel.GetComponentsInChildren<ICheatBindable>(true);
            for (int i = 0; i < bindables.Length; i++)
                bindables[i].Bind(services);
        }
    }
}
