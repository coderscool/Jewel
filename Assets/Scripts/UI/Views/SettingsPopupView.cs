using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Bảng cài đặt: bật tắt nhạc, tiếng động, rung, và đường về Home.
    public class SettingsPopupView : PopupView
    {
        [Header("Âm thanh")]
        [Tooltip("Toggle bật/tắt nhạc nền.")]
        [SerializeField] private Toggle _musicToggle;

        [Tooltip("Toggle bật/tắt tiếng động.")]
        [SerializeField] private Toggle _soundToggle;

        [Tooltip("Toggle bật/tắt rung.")]
        [SerializeField] private Toggle _vibrationToggle;

        [Header("Icon đổi theo trạng thái — để trống cũng chạy")]
        [Tooltip("Icon hiện khi nhạc đang bật.")]
        [SerializeField] private GameObject _musicOnIcon;

        [Tooltip("Icon hiện khi nhạc đang tắt.")]
        [SerializeField] private GameObject _musicOffIcon;

        [SerializeField] private GameObject _soundOnIcon;
        [SerializeField] private GameObject _soundOffIcon;

        [SerializeField] private GameObject _vibrationOnIcon;
        [SerializeField] private GameObject _vibrationOffIcon;

        [Header("Điều hướng")]
        [Tooltip("Nút về màn hình Home.")]
        [SerializeField] private Button _homeButton;

        [SerializeField] private Button _closeButton;

        private ISoundService _sound;
        private IVibrationService _vibration;
        private HomeScreenView _home;
        private HudView _hud;
        private IFreePaintService _freePaint;

        [Inject]
        public void Construct(ISoundService sound, IVibrationService vibration,
            HomeScreenView home, HudView hud, IFreePaintService freePaint)
        {
            _sound = sound;
            _vibration = vibration;
            _home = home;
            _hud = hud;
            _freePaint = freePaint;
        }

        private void Awake()
        {
            if (_musicToggle != null) _musicToggle.onValueChanged.AddListener(HandleMusicToggled);
            if (_soundToggle != null) _soundToggle.onValueChanged.AddListener(HandleSoundToggled);
            if (_vibrationToggle != null) _vibrationToggle.onValueChanged.AddListener(HandleVibrationToggled);
            if (_homeButton != null) _homeButton.onClick.AddListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_musicToggle != null) _musicToggle.onValueChanged.RemoveListener(HandleMusicToggled);
            if (_soundToggle != null) _soundToggle.onValueChanged.RemoveListener(HandleSoundToggled);
            if (_vibrationToggle != null) _vibrationToggle.onValueChanged.RemoveListener(HandleVibrationToggled);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// Mở popup và đọc lại trạng thái cài đặt.
        public override void Show()
        {
            base.Show();

            if (_freePaint != null) _freePaint.SetPaused(true);

            RefreshControls();
        }

        /// Đóng popup và thả đồng hồ booster.
        public override void Hide()
        {
            if (_freePaint != null) _freePaint.SetPaused(false);

            base.Hide();
        }

        private void HandleMusicToggled(bool isOn)
        {
            if (_sound == null) return;

            _sound.Play(SoundKey.Direction);

            _sound.SetMusicEnabled(isOn);
            RefreshIcons();
        }

        private void HandleSoundToggled(bool isOn)
        {
            if (_sound == null) return;

            _sound.Play(SoundKey.Direction);

            _sound.SetSoundEnabled(isOn);
            RefreshIcons();
        }

        private void HandleVibrationToggled(bool isOn)
        {
            if (_vibration == null) return;

            _sound?.Play(SoundKey.Direction);

            _vibration.SetEnabled(isOn);
            RefreshIcons();
        }

        /// Cập nhật toggle và icon theo trạng thái thật.
        private void RefreshControls()
        {
            var music = _sound != null && _sound.IsMusicEnabled;
            var sound = _sound != null && _sound.IsSoundEnabled;
            var vibration = _vibration != null && _vibration.IsEnabled;

            if (_musicToggle != null) _musicToggle.SetIsOnWithoutNotify(music);
            if (_soundToggle != null) _soundToggle.SetIsOnWithoutNotify(sound);
            if (_vibrationToggle != null) _vibrationToggle.SetIsOnWithoutNotify(vibration);

            RefreshIcons();
        }

        private void RefreshIcons()
        {
            var music = _sound != null && _sound.IsMusicEnabled;
            var sound = _sound != null && _sound.IsSoundEnabled;
            var vibration = _vibration != null && _vibration.IsEnabled;

            if (_musicOnIcon != null) _musicOnIcon.SetActive(music);
            if (_musicOffIcon != null) _musicOffIcon.SetActive(!music);

            if (_soundOnIcon != null) _soundOnIcon.SetActive(sound);
            if (_soundOffIcon != null) _soundOffIcon.SetActive(!sound);

            if (_vibrationOnIcon != null) _vibrationOnIcon.SetActive(vibration);
            if (_vibrationOffIcon != null) _vibrationOffIcon.SetActive(!vibration);
        }

        /// Ẩn popup và HUD rồi về Home.
        private void HandleHomeClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.Direction);

            if (_freePaint != null) _freePaint.SetPaused(true);

            var transition = _home != null ? _home.Transition : null;

            if (transition != null)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;

                transition.Play(GoHome);
                return;
            }

            GoHome();
        }

        /// Đổi sang Home.
        private void GoHome()
        {
            HideSilently();

            if (_hud != null) _hud.SetVisible(false);
            if (_home != null) _home.Show();
        }
    }
}
