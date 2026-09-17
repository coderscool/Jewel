using JewelPainter.Core.Persistence;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Data;
using JewelPainter.UI.Managers;
using JewelPainter.UI.Views;
using VContainer;
using VContainer.Unity;

namespace JewelPainter.Bootstrap
{
    /// Composition root đăng ký mọi phụ thuộc của scene.
    public class GameLifetimeScope : LifetimeScope
    {
        private const int FreeHintCredits = 3;

        private const int FreeFreePaintUses = 3;

        private const int FreeFillColorUses = 3;

        [UnityEngine.Tooltip("Bảng mốc mở khoá booster — cùng asset đã gán cho HudView.\n\n" +
            "Để trống thì không có popup báo mở khoá nào cả. Hợp lý: không có bảng mốc " +
            "thì không booster nào bị khoá, nên cũng chẳng có gì để báo.")]
        [UnityEngine.SerializeField] private BoosterUnlockConfig _boosterUnlock;

        private const int LevelsPerRatePrompt = 4;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ISaveService, PlayerPrefsSaveService>(Lifetime.Singleton);

            builder.Register<PlayerProgress>(Lifetime.Singleton);
            builder.Register<PlayerWallet>(Lifetime.Singleton);
            builder.Register<TutorialState>(Lifetime.Singleton);

            builder.Register(_ => new HintCredits(
                _.Resolve<ISaveService>(), FreeHintCredits), Lifetime.Singleton);

            builder.Register(_ => new FreePaintCredits(
                _.Resolve<ISaveService>(), FreeFreePaintUses), Lifetime.Singleton);

            builder.Register(_ => new FillColorCredits(
                _.Resolve<ISaveService>(), FreeFillColorUses), Lifetime.Singleton);

            builder.Register(_ => new RatePrompt(
                _.Resolve<ISaveService>(), LevelsPerRatePrompt), Lifetime.Singleton);

            builder.Register<IVibrationService, VibrationService>(Lifetime.Singleton);

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

            builder.RegisterComponentInHierarchy<FreePaintController>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<FillColorController>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<BoardView>();
            builder.RegisterComponentInHierarchy<BoardNumberMesh>()
                   .AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<BoardCamera>();
            builder.RegisterComponentInHierarchy<BoardInput>();
            builder.RegisterComponentInHierarchy<BoardGridLinesShaded>()
                   .AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<HintLayer>();
            builder.RegisterComponentInHierarchy<HintMarkerEffect>();
            builder.RegisterComponentInHierarchy<JewelFlyEffect>();
            builder.RegisterComponentInHierarchy<JewelLayer>();
            builder.RegisterComponentInHierarchy<JewelLandSparkle>();
            builder.RegisterComponentInHierarchy<ColorCompleteSparkle>();
            builder.RegisterComponentInHierarchy<WinCelebration>();

            builder.RegisterComponentInHierarchy<ColorPaletteBar>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterEntryPoint<RatePopupPresenter>();

            if (_boosterUnlock != null)
            {
                builder.RegisterInstance(_boosterUnlock);
                builder.RegisterEntryPoint<BoosterUnlockPresenter>();
            }

            builder.RegisterEntryPoint<GameEntryPoint>();
        }
    }
}
