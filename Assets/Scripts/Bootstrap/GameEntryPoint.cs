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
        private readonly PaintProgressStore _paintProgressStore;
        private readonly PaintManager _paintManager;
        private readonly IPaintService _paintService;
        private readonly HudView _hud;
        private readonly BoardView _boardView;
        private readonly BoardNumberLayer _numberLayer;
        private readonly BoardCamera _boardCamera;
        private readonly BoardInput _boardInput;
        private readonly BoardGridLines _gridLines;
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
            BoardNumberLayer numberLayer,
            BoardCamera boardCamera,
            BoardInput boardInput,
            BoardGridLines gridLines,
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

            // Kho tiến độ Init trước PaintManager: PaintManager nạp lại tiến độ qua nó
            // ngay trong handler OnLevelStarted đầu tiên.
            _paintProgressStore.Init(_save, _levelService);

            // PaintManager phải Init TRƯỚC BoardView: cả hai nghe OnLevelStarted, và
            // BoardView hỏi trạng thái tô ngay lúc dựng lại bảng.
            _paintManager.Init(_levelService, _paintProgressStore);

            _boardView.Init(_levelService, _paintService);
            _numberLayer.Init(_boardView);
            _gridLines.Init(_boardView);

            // BoardInput quyết định mỗi nét kéo là tô hay di chuyển; camera đọc lại
            // quyết định đó nên phải Init sau nó.
            _boardInput.Init(_boardView, _paintService);
            _boardCamera.Init(_boardView, _levelService, _boardInput);

            // Nút gợi ý cần cả trạng thái tô lẫn camera. HudView hỏi nó "bấm được chưa"
            // ngay trong Init của mình, nên nó phải xong trước HUD.
            _hintMarker.Init(_boardView);
            _hintFocus.Init(_paintService, _boardCamera, _hintMarker);
            _hud.Init(_levelService, _hintFocus, _levelFlow, _popupService, _home);

            // PaletteBar Init trước: hiệu ứng ngọc bay hỏi nó vị trí xuất phát.
            _paletteBar.Init(_paintService, _levelService);

            // Hướng dẫn Init SAU PaletteBar: cả hai nghe OnBoardReady, mà ngón tay chỉ
            // biết đứng ở đâu sau khi thanh màu đã dựng xong các ô.
            _tutorial.Init(_levelService, _paintService, _paletteBar);

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
            _notificationPresenter.Init(_paintService, _popupService);

            // Home dựng sẵn nhưng không tự mở — nút Home trong popup Cài đặt mới mở nó.
            _home.Init(_levelService, _popupService, _paintProgressStore);

            // KHÔNG gọi LoadLevel thẳng ở đây. Màn hình chờ nhường một frame cho Canvas
            // kịp vẽ rồi mới nạp; gọi thẳng thì cả việc hiện màn chờ lẫn việc dựng bàn
            // rơi vào cùng một frame và người chơi không bao giờ thấy màn chờ.
            _loading.Begin(_levelService);
        }
    }
}
