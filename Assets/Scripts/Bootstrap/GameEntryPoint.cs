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
    /// Chạy một lần sau khi container dựng xong: đưa phụ thuộc cho các
    /// MonoBehaviour trong scene rồi mở màn chơi hiện tại.
    /// Class thuần C# — không phải MonoBehaviour.
    ///
    /// Constructor dài là bình thường ở composition root: đây đúng là nơi mọi thứ
    /// gặp nhau, và thà thấy hết ở một chỗ còn hơn để từng object tự đi tìm.
    public class GameEntryPoint : IStartable
    {
        private readonly ISaveService _save;
        private readonly PlayerProgress _progress;
        private readonly SoundService _sound;
        private readonly LevelManager _levelManager;
        private readonly ILevelService _levelService;
        private readonly PaintManager _paintManager;
        private readonly IPaintService _paintService;
        private readonly HudView _hud;
        private readonly BoardView _boardView;
        private readonly BoardNumberLayer _numberLayer;
        private readonly BoardCamera _boardCamera;
        private readonly BoardInput _boardInput;
        private readonly BoardGridLines _gridLines;
        private readonly HintLayer _hintLayer;
        private readonly LevelFlowController _levelFlow;
        private readonly JewelFlyEffect _jewelFlyEffect;
        private readonly JewelLayer _jewelLayer;
        private readonly JewelLandSparkle _jewelLandSparkle;
        private readonly ColorCompleteSparkle _colorCompleteSparkle;
        private readonly WinCelebration _winCelebration;
        private readonly ColorPaletteBar _paletteBar;
        private readonly HintFocusController _hintFocus;
        private readonly IPopupService _popupService;
        private readonly WinPopupPresenter _winPopupPresenter;

        public GameEntryPoint(
            ISaveService save,
            PlayerProgress progress,
            SoundService sound,
            LevelManager levelManager,
            ILevelService levelService,
            PaintManager paintManager,
            IPaintService paintService,
            HudView hud,
            BoardView boardView,
            BoardNumberLayer numberLayer,
            BoardCamera boardCamera,
            BoardInput boardInput,
            BoardGridLines gridLines,
            HintLayer hintLayer,
            LevelFlowController levelFlow,
            JewelFlyEffect jewelFlyEffect,
            JewelLayer jewelLayer,
            JewelLandSparkle jewelLandSparkle,
            ColorCompleteSparkle colorCompleteSparkle,
            WinCelebration winCelebration,
            ColorPaletteBar paletteBar,
            HintFocusController hintFocus,
            IPopupService popupService,
            WinPopupPresenter winPopupPresenter)
        {
            _save = save;
            _progress = progress;
            _sound = sound;
            _levelManager = levelManager;
            _levelService = levelService;
            _paintManager = paintManager;
            _paintService = paintService;
            _hud = hud;
            _boardView = boardView;
            _numberLayer = numberLayer;
            _boardCamera = boardCamera;
            _boardInput = boardInput;
            _gridLines = gridLines;
            _hintLayer = hintLayer;
            _levelFlow = levelFlow;
            _jewelFlyEffect = jewelFlyEffect;
            _jewelLayer = jewelLayer;
            _jewelLandSparkle = jewelLandSparkle;
            _colorCompleteSparkle = colorCompleteSparkle;
            _winCelebration = winCelebration;
            _paletteBar = paletteBar;
            _hintFocus = hintFocus;
            _popupService = popupService;
            _winPopupPresenter = winPopupPresenter;
        }

        public void Start()
        {
            _sound.Init(_save);
            _levelManager.Init(_progress);

            // PaintManager phải Init TRƯỚC BoardView: cả hai nghe OnLevelStarted, và
            // BoardView hỏi trạng thái tô ngay lúc dựng lại bảng.
            _paintManager.Init(_levelService);

            _boardView.Init(_levelService);
            _numberLayer.Init(_boardView);
            _gridLines.Init(_boardView);

            // BoardInput quyết định mỗi nét kéo là tô hay di chuyển; camera đọc lại
            // quyết định đó nên phải Init sau nó.
            _boardInput.Init(_boardView, _paintService);
            _boardCamera.Init(_boardView, _levelService, _boardInput);

            // Nút gợi ý cần cả trạng thái tô lẫn camera. HudView hỏi nó "bấm được chưa"
            // ngay trong Init của mình, nên nó phải xong trước HUD.
            _hintFocus.Init(_paintService, _boardCamera);
            _hud.Init(_levelService, _hintFocus, _popupService, _levelFlow);

            // PaletteBar Init trước: hiệu ứng ngọc bay hỏi nó vị trí xuất phát.
            _paletteBar.Init(_paintService, _levelService);

            // JewelFlyEffect quyết định lúc nào một ô coi như "xong": nó đổi màu ô,
            // gỡ marker gợi ý và cho hiện ngọc. Hai lớp dưới đều chờ tín hiệu của nó.
            _jewelFlyEffect.Init(_boardView, _paintService, _paletteBar);
            _hintLayer.Init(_boardView, _paintService, _jewelFlyEffect);
            _jewelLayer.Init(_boardView, _paintService, _jewelFlyEffect);
            _jewelLandSparkle.Init(_boardView, _jewelFlyEffect);
            _colorCompleteSparkle.Init(_boardView, _paintService, _jewelFlyEffect);
            _winCelebration.Init(_boardView, _boardCamera);
            _levelFlow.Init(_levelService, _paintService, _jewelFlyEffect, _winCelebration);

            // Init sau LevelFlow: nó đăng ký nghe sự kiện thắng màn của LevelFlow.
            _winPopupPresenter.Init(_levelFlow, _popupService);

            _levelService.LoadLevel(_progress.Level);
        }
    }
}
