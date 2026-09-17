using JewelPainter.Core.Persistence;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Managers;
using JewelPainter.UI.Views;
using VContainer.Unity;

namespace JewelPainter.Bootstrap
{
    /// Nối phụ thuộc cho các MonoBehaviour trong scene rồi mở màn chơi hiện tại.
    public class GameEntryPoint : IStartable
    {
        private readonly ISaveService _save;
        private readonly PlayerProgress _progress;
        private readonly SoundService _sound;

        private MusicDirector _musicDirector;
        private readonly LevelManager _levelManager;
        private readonly ILevelService _levelService;
        private readonly PaintProgressStore _paintProgressStore;
        private readonly PaintManager _paintManager;
        private readonly IPaintService _paintService;
        private readonly HudView _hud;
        private readonly BoardView _boardView;
        private readonly IBoardNumbers _numberLayer;
        private readonly BoardCamera _boardCamera;
        private readonly BoardInput _boardInput;
        private readonly IBoardGridLines _gridLines;
        private readonly HintLayer _hintLayer;
        private readonly HintMarkerEffect _hintMarker;
        private readonly LevelFlowController _levelFlow;
        private readonly JewelFlyEffect _jewelFlyEffect;
        private readonly JewelLayer _jewelLayer;
        private readonly JewelLandSparkle _jewelLandSparkle;
        private readonly ColorCompleteSparkle _colorCompleteSparkle;
        private readonly WinCelebration _winCelebration;
        private readonly ColorPaletteBar _paletteBar;
        private readonly HintFocusController _hintFocus;
        private readonly HintCredits _hintCredits;
        private readonly FreePaintController _freePaint;
        private readonly FreePaintCredits _freePaintCredits;
        private readonly FillColorController _fillColor;
        private readonly FillColorCredits _fillColorCredits;
        private readonly PlayerWallet _wallet;
        private readonly TutorialState _tutorialState;
        private readonly IPopupService _popupService;
        private readonly WinPopupPresenter _winPopupPresenter;
        private readonly NotificationPresenter _notificationPresenter;
        private readonly HomeScreenView _home;
        private readonly LoadingScreenView _loading;
        private readonly TutorialOverlayView _tutorial;

        public GameEntryPoint(
            ISaveService save,
            PlayerProgress progress,
            SoundService sound,
            LevelManager levelManager,
            ILevelService levelService,
            PaintProgressStore paintProgressStore,
            PaintManager paintManager,
            IPaintService paintService,
            HudView hud,
            BoardView boardView,
            IBoardNumbers numberLayer,
            BoardCamera boardCamera,
            BoardInput boardInput,
            IBoardGridLines gridLines,
            HintLayer hintLayer,
            HintMarkerEffect hintMarker,
            LevelFlowController levelFlow,
            JewelFlyEffect jewelFlyEffect,
            JewelLayer jewelLayer,
            JewelLandSparkle jewelLandSparkle,
            ColorCompleteSparkle colorCompleteSparkle,
            WinCelebration winCelebration,
            ColorPaletteBar paletteBar,
            HintFocusController hintFocus,
            HintCredits hintCredits,
            FreePaintController freePaint,
            FreePaintCredits freePaintCredits,
            FillColorController fillColor,
            FillColorCredits fillColorCredits,
            PlayerWallet wallet,
            TutorialState tutorialState,
            IPopupService popupService,
            WinPopupPresenter winPopupPresenter,
            NotificationPresenter notificationPresenter,
            HomeScreenView home,
            LoadingScreenView loading,
            TutorialOverlayView tutorial
            )
        {
            _save = save;
            _progress = progress;
            _sound = sound;
            _levelManager = levelManager;
            _levelService = levelService;
            _paintProgressStore = paintProgressStore;
            _paintManager = paintManager;
            _paintService = paintService;
            _hud = hud;
            _boardView = boardView;
            _numberLayer = numberLayer;
            _boardCamera = boardCamera;
            _boardInput = boardInput;
            _gridLines = gridLines;
            _hintLayer = hintLayer;
            _hintMarker = hintMarker;
            _levelFlow = levelFlow;
            _jewelFlyEffect = jewelFlyEffect;
            _jewelLayer = jewelLayer;
            _jewelLandSparkle = jewelLandSparkle;
            _colorCompleteSparkle = colorCompleteSparkle;
            _winCelebration = winCelebration;
            _paletteBar = paletteBar;
            _hintFocus = hintFocus;
            _hintCredits = hintCredits;
            _freePaint = freePaint;
            _freePaintCredits = freePaintCredits;
            _fillColor = fillColor;
            _fillColorCredits = fillColorCredits;
            _wallet = wallet;
            _tutorialState = tutorialState;
            _popupService = popupService;
            _winPopupPresenter = winPopupPresenter;
            _notificationPresenter = notificationPresenter;
            _home = home;
            _loading = loading;
            _tutorial = tutorial;
        }

        public void Start()
        {
            _sound.Init(_save);
            _levelManager.Init(_progress);

            _paintProgressStore.Init(_save, _levelService);

            _paintManager.Init(_levelService, _paintProgressStore);

            _boardView.Init(_levelService, _paintService);
            _gridLines.Init(_boardView);

            _boardInput.Init(_boardView, _paintService, _tutorialState);
            _boardCamera.Init(_boardView, _levelService, _boardInput);

            _hintMarker.Init(_boardView);
            _hintFocus.Init(_paintService, _boardCamera, _hintMarker, _hintCredits);

            _freePaint.Init(_paintManager, _freePaintCredits);

            _fillColor.Init(_paintManager, _fillColorCredits, _jewelFlyEffect);

            _hud.Init(
                _levelService, _paintService, _hintFocus, _freePaint, _fillColor, _levelFlow,
                _popupService, _wallet, _sound, _progress, _tutorialState);

            _paletteBar.Init(_paintService, _levelService, _levelFlow, _jewelFlyEffect, _sound,
                _tutorialState, _colorCompleteSparkle);

            _tutorial.Init(_levelService, _paintService, _paletteBar, _tutorialState, _hintFocus,
                _boardView, _boardCamera);

            _jewelFlyEffect.Init(_boardView, _paintService, _paletteBar, _sound);
            _hintLayer.Init(_boardView, _paintService, _jewelFlyEffect);

            _numberLayer.Init(_boardView, _paintService, _jewelFlyEffect);
            _jewelLayer.Init(_boardView, _paintService, _jewelFlyEffect);
            _jewelLandSparkle.Init(_boardView, _jewelFlyEffect);
            _colorCompleteSparkle.Init(_boardView, _paintService, _jewelFlyEffect, _sound);
            _winCelebration.Init(_boardView, _boardCamera);
            _levelFlow.Init(_levelService, _paintService, _jewelFlyEffect, _winCelebration,
                _colorCompleteSparkle);

            _winPopupPresenter.Init(_levelFlow, _popupService);
            _notificationPresenter.Init(_paintService, _popupService, _tutorialState);

            _home.Init(_levelService, _popupService, _paintProgressStore, _wallet, _boardView, _sound);

            _musicDirector = new MusicDirector(_sound, _levelService, _home, _loading);

            _loading.Bind(_levelService);

#if CHEAT_ENABLED
            Cheat.CheatInstaller.Install(
                _levelService, _paintService, _paintProgressStore, _progress, _wallet, _hintCredits,
                _freePaintCredits, _fillColorCredits, _hud);
#endif

            _levelService.LoadLevel(_levelService.CurrentLevel);
        }
    }
}
