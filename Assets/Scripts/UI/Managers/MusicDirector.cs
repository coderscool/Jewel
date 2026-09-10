using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Views;

namespace JewelPainter.UI.Managers
{
    /// Quyết định lúc nào chạy bản nhạc nào. Một chỗ duy nhất, thuần C# — không
    /// MonoBehaviour, không object nào trong scene.
    ///
    /// Gom lại đây thay vì để mỗi màn hình tự bật nhạc của mình vì luật thật sự là một
    /// luật về HAI bên: "đang ở Home thì nhạc Home, ngoài ra thì nhạc màn chơi". Rải ra
    /// hai lớp thì mỗi lớp chỉ biết nửa luật, và cái nửa còn thiếu chính là chỗ sinh ra
    /// những lần nhạc chồng lên nhau hoặc tắt ngóm không ai hiểu vì sao.
    ///
    /// SoundService tự bỏ qua khi được yêu cầu đúng bản đang chạy, nên lớp này cứ khai
    /// thoải mái mà không phải nhớ mình đã khai gì.
    public class MusicDirector
    {
        private readonly ISoundService _sound;
        private readonly ILevelService _levelService;
        private readonly HomeScreenView _home;
        private readonly LoadingScreenView _loading;

        public MusicDirector(ISoundService sound, ILevelService levelService, HomeScreenView home,
            LoadingScreenView loading)
        {
            _sound = sound;
            _levelService = levelService;
            _home = home;
            _loading = loading;

            if (_levelService != null) _levelService.OnLevelStarted += HandleLevelStarted;
            if (_home != null) _home.OnVisibilityChanged += HandleHomeVisibilityChanged;
            if (_loading != null) _loading.OnVisibilityChanged += HandleLoadingVisibilityChanged;

            // KHÔNG Refresh ở đây.
            //
            // Lớp này dựng xong TRƯỚC lời gọi nạp màn đầu tiên, nên lúc này màn chờ chưa
            // hiện và Refresh sẽ thấy "không Home, không loading" rồi bật nhạc màn chơi —
            // đúng một nhịp trước khi màn chờ che lên và tắt nó đi. Người chơi nghe thấy
            // một tiếng nhạc cụt ngay lúc mở game.
            //
            // Để im thì bản nhạc đầu tiên bắt đầu ở đúng chỗ nó nên bắt đầu: lúc màn chờ
            // tắt đi.
        }

        /// Không có ai gọi hàm này hôm nay — lớp sống suốt phiên chơi. Có nó để khi nào
        /// cần dựng lại (đổi scene chẳng hạn) thì không phải đi tìm chỗ gỡ sự kiện.
        public void Dispose()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_home != null) _home.OnVisibilityChanged -= HandleHomeVisibilityChanged;
            if (_loading != null) _loading.OnVisibilityChanged -= HandleLoadingVisibilityChanged;
        }

        private void HandleLevelStarted(int levelId) => Refresh();

        private void HandleHomeVisibilityChanged(bool visible) => Refresh();

        private void HandleLoadingVisibilityChanged(bool visible) => Refresh();

        /// Màn chờ che thì IM; Home che thì nhạc Home; còn lại thì nhạc màn chơi.
        ///
        /// Thứ tự ba nhánh chính là thứ tự các lớp nằm chồng lên nhau trên màn hình, nên
        /// đọc từ trên xuống là ra ngay lớp nào đang thắng.
        ///
        /// Hỏi lại trạng thái thay vì suy từ sự kiện vừa nhận. Suy từ sự kiện thì lúc vào
        /// game sẽ sai: màn chơi dở được nạp NGAY trong khi Home vẫn đang mở, và
        /// OnLevelStarted đến trước — nhạc màn chơi sẽ chạy sau lưng màn hình Home rồi
        /// một lát sau mới bị đổi lại.
        private void Refresh()
        {
            // Màn chờ vẫn còn che một lúc SAU khi bàn đã dựng xong (nó giữ thêm
            // Minimum Seconds), nên OnLevelStarted một mình không đủ để biết đã vào màn.
            if (_loading != null && _loading.IsShowing)
            {
                _sound.StopMusic();
                return;
            }

            if (_home != null && _home.IsVisible)
            {
                _sound.PlayMusic(MusicKey.Home);
                return;
            }

            _sound.PlayMusic(MusicKey.Gameplay);
        }
    }
}
