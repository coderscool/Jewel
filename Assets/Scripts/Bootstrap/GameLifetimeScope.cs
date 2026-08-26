using JewelPainter.Core.Persistence;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Managers;
using JewelPainter.UI.Views;
using VContainer;
using VContainer.Unity;

namespace JewelPainter.Bootstrap
{
    /// Composition root — nơi DUY NHẤT được biết mọi tầng.
    /// Không ai using ngược vào Bootstrap.
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Core — class thuần, container tự dựng
            builder.Register<ISaveService, PlayerPrefsSaveService>(Lifetime.Singleton);

            // Gameplay Domain — thuần C#, nhận ISaveService qua constructor
            builder.Register<PlayerProgress>(Lifetime.Singleton);
            builder.Register<PlayerWallet>(Lifetime.Singleton);

            // MonoBehaviour có sẵn trong scene — Find một lần lúc khởi động, hợp lệ ở đây
            builder.RegisterComponentInHierarchy<SoundService>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<LevelManager>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<PopupManager>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<HudView>();
            builder.RegisterComponentInHierarchy<HomeScreenView>();
            builder.RegisterComponentInHierarchy<LoadingScreenView>();
            builder.RegisterComponentInHierarchy<TutorialOverlayView>();

            builder.RegisterComponentInHierarchy<PaintProgressStore>();

            builder.RegisterComponentInHierarchy<PaintManager>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<LevelFlowController>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<WinPopupPresenter>();
            builder.RegisterComponentInHierarchy<NotificationPresenter>();

            builder.RegisterComponentInHierarchy<HintFocusController>()
                   .AsImplementedInterfaces().AsSelf();

            // Board — mặt sân chơi trong world
            builder.RegisterComponentInHierarchy<BoardView>();
            builder.RegisterComponentInHierarchy<BoardNumberLayer>();
            builder.RegisterComponentInHierarchy<BoardCamera>();
            builder.RegisterComponentInHierarchy<BoardInput>();
            builder.RegisterComponentInHierarchy<BoardGridLines>();
            builder.RegisterComponentInHierarchy<HintLayer>();
            builder.RegisterComponentInHierarchy<HintMarkerEffect>();
            builder.RegisterComponentInHierarchy<JewelFlyEffect>();
            builder.RegisterComponentInHierarchy<JewelLayer>();
            builder.RegisterComponentInHierarchy<JewelLandSparkle>();
            builder.RegisterComponentInHierarchy<ColorCompleteSparkle>();
            builder.RegisterComponentInHierarchy<WinCelebration>();

            builder.RegisterComponentInHierarchy<ColorPaletteBar>()
                   .AsImplementedInterfaces().AsSelf();

            // Điểm khởi động: nối dây rồi bắt đầu màn chơi
            builder.RegisterEntryPoint<GameEntryPoint>();
        }
    }
}
