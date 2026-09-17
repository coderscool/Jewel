using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Views;

namespace JewelPainter.UI.Managers
{
    /// Quyết định lúc nào chạy bản nhạc nào.
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
        }

        /// Huỷ đăng ký sự kiện.
        public void Dispose()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_home != null) _home.OnVisibilityChanged -= HandleHomeVisibilityChanged;
            if (_loading != null) _loading.OnVisibilityChanged -= HandleLoadingVisibilityChanged;
        }

        private void HandleLevelStarted(int levelId) => Refresh();

        private void HandleHomeVisibilityChanged(bool visible) => Refresh();

        private void HandleLoadingVisibilityChanged(bool visible) => Refresh();

        /// Chọn bản nhạc phù hợp với màn hình hiện tại.
        private void Refresh()
        {
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
