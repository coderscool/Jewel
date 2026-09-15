using JewelPainter.Core.Persistence;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Data;
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
        /// Số lượt gợi ý miễn phí phát cho người chơi mới, đúng một lần trong đời máy.
        /// Hết thì nút gợi ý chuyển sang mở popup mời thêm lượt.
        private const int FreeHintCredits = 3;

        /// Số lượt booster "tô tự do" phát cho người chơi mới, cũng đúng một lần trong
        /// đời máy.
        ///
        /// Một lượt ở đây đáng giá hơn hẳn một lượt gợi ý — 20 giây tô ô nào cũng được,
        /// so với một lần chỉ ra đúng một ô — nên khi lên bảng giá thật thì đây là con số
        /// nên hạ trước. Để 3 vì hai lượt đầu người chơi còn đang đoán xem nút này làm gì.
        private const int FreeFreePaintUses = 3;

        /// Số lượt booster "tô hết màu đang chọn" phát cho người chơi mới.
        ///
        /// Đây là booster MẠNH NHẤT trong ba cái — một lượt xoá sạch cả một màu, tức là
        /// bỏ qua hẳn một phần việc của màn chơi. Vì thế trước đây để 2, thấp hơn hai
        /// booster kia một lượt.
        ///
        /// Nâng lên 3 cho BẰNG hai cái kia. Lý do không nằm ở chỗ booster này mạnh hay
        /// yếu, mà ở chỗ cả ba giờ cùng mở khoá ở màn 3: người chơi gặp ba cái nút cùng
        /// lúc, và một cái mang số nhỏ hơn hai cái còn lại trông như lỗi chứ không đọc ra
        /// là cân bằng. Muốn ghìm booster này lại thì ghìm bằng GIÁ khi lên bảng giá
        /// thật, đừng ghìm bằng con số đập vào mắt ngay lần đầu nhìn thấy nút.
        ///
        /// LƯU Ý khi đổi con số này: nó chỉ phát MỘT LẦN trong đời máy, và cờ
        /// PreferenceKeys.FillColorCreditsGranted nhớ là đã phát rồi. Máy nào đã chạy bản
        /// cũ thì vẫn giữ nguyên mức 2 — phải xoá PlayerPrefs mới thấy con số mới.
        private const int FreeFillColorUses = 3;

        [UnityEngine.Tooltip("Bảng mốc mở khoá booster — cùng asset đã gán cho HudView.\n\n" +
            "Để trống thì không có popup báo mở khoá nào cả. Hợp lý: không có bảng mốc " +
            "thì không booster nào bị khoá, nên cũng chẳng có gì để báo.")]
        [UnityEngine.SerializeField] private BoosterUnlockConfig _boosterUnlock;

        /// Xong bao nhiêu màn thì mời đánh giá một lần. Đếm lại từ đầu sau mỗi lần mời,
        /// và tắt hẳn khi người chơi đã bấm đánh giá.
        private const int LevelsPerRatePrompt = 4;

        protected override void Configure(IContainerBuilder builder)
        {
            // Core — class thuần, container tự dựng
            builder.Register<ISaveService, PlayerPrefsSaveService>(Lifetime.Singleton);

            // Gameplay Domain — thuần C#, nhận ISaveService qua constructor
            builder.Register<PlayerProgress>(Lifetime.Singleton);
            builder.Register<PlayerWallet>(Lifetime.Singleton);
            builder.Register<TutorialState>(Lifetime.Singleton);

            // Số lượt khởi đầu truyền thẳng ở đây chứ không để trong HintCredits: đây là
            // con số cân bằng game, mà composition root mới là nơi mọi con số như thế
            // gặp nhau. Domain chỉ biết đếm và ghi.
            builder.Register(_ => new HintCredits(
                _.Resolve<ISaveService>(), FreeHintCredits), Lifetime.Singleton);

            builder.Register(_ => new FreePaintCredits(
                _.Resolve<ISaveService>(), FreeFreePaintUses), Lifetime.Singleton);

            builder.Register(_ => new FillColorCredits(
                _.Resolve<ISaveService>(), FreeFillColorUses), Lifetime.Singleton);

            builder.Register(_ => new RatePrompt(
                _.Resolve<ISaveService>(), LevelsPerRatePrompt), Lifetime.Singleton);

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

            builder.RegisterComponentInHierarchy<FreePaintController>()
                   .AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<FillColorController>()
                   .AsImplementedInterfaces().AsSelf();

            // Board — mặt sân chơi trong world
            builder.RegisterComponentInHierarchy<BoardView>();
            // Đổi bản hiện số ngay tại dòng này — GameEntryPoint đi qua IBoardNumbers
            // nên không phải sửa gì thêm:
            //   BoardNumberLayer một TextMeshPro mỗi ô trong tầm nhìn (bản cũ)
            //   BoardNumberMesh  cả bảng gộp vào MỘT mesh, một draw call
            // Component tương ứng phải nằm sẵn trên object trong scene.
            builder.RegisterComponentInHierarchy<BoardNumberMesh>()
                   .AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<BoardCamera>();
            builder.RegisterComponentInHierarchy<BoardInput>();
            // Đổi bản kẻ viền ô ngay tại dòng này — GameEntryPoint đi qua
            // IBoardGridLines nên không phải sửa gì thêm:
            //   BoardGridLines       nướng nét vào texture cỡ cả bảng (bản cũ)
            //   BoardGridLinesShaded mask 1 texel mỗi ô, nét do shader kẻ (nhẹ hơn ~3000 lần)
            // Component tương ứng phải nằm sẵn trên object GridLines trong scene.
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

            // Rình lúc màn hình sạch để mời đánh giá. Thuần C# nên không cần object nào
            // trong scene — ITickable của VContainer cấp nhịp Update cho nó.
            builder.RegisterEntryPoint<RatePopupPresenter>();

            // Báo booster vừa mở khoá. Cùng khuôn với lời mời đánh giá: rình lúc màn hình
            // sạch rồi mới mở popup.
            //
            // Chỉ dựng khi có bảng mốc. RegisterInstance không nhận null, mà quan trọng
            // hơn: không có bảng thì không booster nào khoá, nên cũng không có gì để báo.
            if (_boosterUnlock != null)
            {
                builder.RegisterInstance(_boosterUnlock);
                builder.RegisterEntryPoint<BoosterUnlockPresenter>();
            }

            // Điểm khởi động: nối dây rồi bắt đầu màn chơi
            builder.RegisterEntryPoint<GameEntryPoint>();
        }
    }
}
