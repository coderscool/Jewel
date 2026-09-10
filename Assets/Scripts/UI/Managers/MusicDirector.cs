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

        public MusicDirector(ISoundService sound, ILevelService levelService, HomeScreenView home)
        {
            _sound = sound;
            _levelService = levelService;
            _home = home;

            if (_levelService != null) _levelService.OnLevelStarted += HandleLevelStarted;
            if (_home != null) _home.OnVisibilityChanged += HandleHomeVisibilityChanged;

            Refresh();
        }

        /// Không có ai gọi hàm này hôm nay — lớp sống suốt phiên chơi. Có nó để khi nào
        /// cần dựng lại (đổi scene chẳng hạn) thì không phải đi tìm chỗ gỡ sự kiện.
        public void Dispose()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_home != null) _home.OnVisibilityChanged -= HandleHomeVisibilityChanged;
        }

        private void HandleLevelStarted(int levelId) => Refresh();

        private void HandleHomeVisibilityChanged(bool visible) => Refresh();

        /// Home che kín màn hình thì nhạc theo Home, còn lại thì theo màn chơi.
        ///
        /// Hỏi lại trạng thái thay vì suy từ sự kiện vừa nhận. Suy từ sự kiện thì lúc vào
        /// game sẽ sai: màn chơi dở được nạp NGAY trong khi Home vẫn đang mở, và
        /// OnLevelStarted đến trước — nhạc màn chơi sẽ chạy sau lưng màn hình Home rồi
        /// một lát sau mới bị đổi lại.
        private void Refresh()
        {
            if (_home != null && _home.IsVisible)
            {
                _sound.PlayMusic(MusicKey.Home);
                return;
            }

            _sound.PlayMusic(MusicKey.Gameplay);
        }
    }
}
