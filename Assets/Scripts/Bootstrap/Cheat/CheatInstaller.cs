#if CHEAT_ENABLED
using CoreModules.CheatKit;
using CoreModules.CheatKit.Ports;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Views;
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Composition root riêng của cheat: dựng panel, gắn module đặc thù, bind port.
    public static class CheatInstaller
    {
        public static void Install(
            ILevelService levelService,
            IPaintService paintService,
            PaintProgressStore paintStore,
            PlayerProgress progress,
            PlayerWallet wallet,
            HintCredits hintCredits,
            FreePaintCredits freePaintCredits,
            FillColorCredits fillColorCredits,
            HudView hud)
        {
            var panel = UITestPanel.Instance != null ? UITestPanel.Instance : CheatPanelBuilder.Build();

            if (panel == null)
            {
                Debug.LogWarning("[Cheat] Không dựng được UITestPanel — bỏ qua phần cheat.");
                return;
            }

            var bridge = new JewelPainterCheatBridge(
                levelService, paintService, paintStore, progress, wallet, hintCredits, freePaintCredits,
                fillColorCredits, hud, CheatRunner.Create());

            CheatPanelBuilder.AddModule<JewelPainterCheatModule>(panel);

            var services = new CheatServices(level: bridge, flow: bridge, progress: bridge, bridge);

            var bindables = panel.GetComponentsInChildren<ICheatBindable>(true);

            for (var i = 0; i < bindables.Length; i++) bindables[i].Bind(services);

            CheatLog.Info("[Cheat] CheatKit sẵn sàng — chạm 5 lần vùng giữa-trên màn hình, hoặc bấm F1.");
        }
    }
}
#endif
