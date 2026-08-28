#if CHEAT_ENABLED
using CoreModules.CheatKit;
using CoreModules.CheatKit.Ports;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Composition root RIÊNG của cheat: dựng panel, gắn module đặc thù, bind port.
    ///
    /// Tách khỏi GameEntryPoint thay vì viết thẳng vào đó, vì toàn bộ file này biến mất
    /// khi không có define CHEAT_ENABLED. Nếu nằm trong GameEntryPoint thì entry point
    /// thật của game sẽ lấm tấm #if — mà đó là file người ta đọc để hiểu game khởi động
    /// thế nào, không phải để hiểu cheat.
    ///
    /// GameEntryPoint vì thế chỉ còn đúng ba dòng bọc #if, và bỏ define đi là nó sạch trơn.
    public static class CheatInstaller
    {
        public static void Install(
            ILevelService levelService,
            IPaintService paintService,
            PaintProgressStore paintStore,
            PlayerProgress progress,
            PlayerWallet wallet,
            HintCredits hintCredits)
        {
            // Có sẵn panel trong scene thì dùng, không thì dựng lúc chạy. Đường no-prefab
            // là mặc định ở đây: panel cheat không nên nằm trong scene thật, vì bản build
            // không có define sẽ để lại một object trống với script đã biến mất.
            var panel = UITestPanel.Instance != null ? UITestPanel.Instance : CheatPanelBuilder.Build();

            if (panel == null)
            {
                Debug.LogWarning("[Cheat] Không dựng được UITestPanel — bỏ qua phần cheat.");
                return;
            }

            var bridge = new JewelPainterCheatBridge(
                levelService, paintService, paintStore, progress, wallet, hintCredits, CheatRunner.Create());

            // Gắn module đặc thù TRƯỚC khi bind: Bind quét toàn bộ ICheatBindable đang có
            // dưới panel, nên module thêm sau sẽ không bao giờ nhận được service.
            CheatPanelBuilder.AddModule<JewelPainterCheatModule>(panel);

            // bridge nằm ở cả ba port chuẩn LẪN extras: cùng một object nên mọi module đọc
            // chung một nguồn trạng thái, không có chuyện hai module thấy hai con số khác nhau.
            var services = new CheatServices(level: bridge, flow: bridge, progress: bridge, bridge);

            var bindables = panel.GetComponentsInChildren<ICheatBindable>(true);

            for (var i = 0; i < bindables.Length; i++) bindables[i].Bind(services);

            CheatLog.Info("[Cheat] CheatKit sẵn sàng — chạm 5 lần vùng giữa-trên màn hình, hoặc bấm F1.");
        }
    }
}
#endif
