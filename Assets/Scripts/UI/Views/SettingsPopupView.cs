using JewelPainter.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Bảng cài đặt: bật tắt nhạc, bật tắt âm thanh, và đường về Home.
    ///
    /// MỘT class dùng cho hai prefab. Bản mở trong game gán nút Home, bản mở từ chính
    /// màn hình Home thì để trống ô đó — đang đứng ở Home rồi thì không có gì để về.
    /// Khác nhau chỉ có thế, không đáng tách thành hai class.
    public class SettingsPopupView : PopupView
    {
        [Header("Âm thanh")]
        [Tooltip("Bấm để bật/tắt nhạc nền.")]
        [SerializeField] private Button _musicButton;

        [Tooltip("Hiện khi nhạc ĐANG BẬT.")]
        [SerializeField] private GameObject _musicOnIcon;

        [Tooltip("Hiện khi nhạc ĐANG TẮT.")]
        [SerializeField] private GameObject _musicOffIcon;

        [SerializeField] private Button _soundButton;
        [SerializeField] private GameObject _soundOnIcon;
        [SerializeField] private GameObject _soundOffIcon;

        [Header("Điều hướng")]
        [Tooltip("Về màn hình Home. ĐỂ TRỐNG ở bản popup mở từ chính Home.")]
        [SerializeField] private Button _homeButton;

        [SerializeField] private Button _closeButton;

        private ISoundService _sound;
        private HomeScreenView _home;
        private HudView _hud;

        [Inject]
        public void Construct(ISoundService sound, HomeScreenView home, HudView hud)
        {
            _sound = sound;
            _home = home;
            _hud = hud;
        }

        private void Awake()
        {
            if (_musicButton != null) _musicButton.onClick.AddListener(ToggleMusic);
            if (_soundButton != null) _soundButton.onClick.AddListener(ToggleSound);
            if (_homeButton != null) _homeButton.onClick.AddListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_musicButton != null) _musicButton.onClick.RemoveListener(ToggleMusic);
            if (_soundButton != null) _soundButton.onClick.RemoveListener(ToggleSound);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// Đọc lại trạng thái ở MỖI lần mở, không phải ở Awake: popup sống suốt phiên
        /// chơi, mà hai bản popup lại chỉnh chung một cặp công tắc.
        public override void Show()
        {
            base.Show();

            RefreshIcons();
        }

        private void ToggleMusic()
        {
            if (_sound == null) return;

            _sound.SetMusicEnabled(!_sound.IsMusicEnabled);
            RefreshIcons();
        }

        private void ToggleSound()
        {
            if (_sound == null) return;

            _sound.SetSoundEnabled(!_sound.IsSoundEnabled);
            RefreshIcons();
        }

        private void RefreshIcons()
        {
            var music = _sound != null && _sound.IsMusicEnabled;
            var sound = _sound != null && _sound.IsSoundEnabled;

            if (_musicOnIcon != null) _musicOnIcon.SetActive(music);
            if (_musicOffIcon != null) _musicOffIcon.SetActive(!music);

            if (_soundOnIcon != null) _soundOnIcon.SetActive(sound);
            if (_soundOffIcon != null) _soundOffIcon.SetActive(!sound);
        }

        /// Ẩn cả popup lẫn HUD trước khi mở Home. Home phủ kín màn hình nhưng nút của
        /// HUD vẫn nhận được cú chạm nếu Canvas của nó nằm trên — tắt hẳn thì không phải
        /// đi đoán thứ tự Sort Order giữa các Canvas.
        private void HandleHomeClicked()
        {
            Hide();

            if (_hud != null) _hud.SetVisible(false);
            if (_home != null) _home.Show();
        }
    }
}
