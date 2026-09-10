using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
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
    ///
    /// HAI BẢN LUÔN KHỚP NHAU mà không cần đồng bộ gì cả: cả hai đọc thẳng từ
    /// ISoundService — một instance duy nhất cho cả game — ở MỖI lần Show. Không bản nào
    /// giữ trạng thái riêng, nên không có gì để lệch. Tắt nhạc ở bảng trong game rồi mở
    /// bảng ở Home là thấy ngay công tắc đã tắt.
    public class SettingsPopupView : PopupView
    {
        [Header("Âm thanh")]
        [Tooltip("Toggle bật/tắt NHẠC NỀN — kéo tglMusic vào đây.\n\n" +
                 "Đừng nối gì vào ô On Value Changed của Toggle trong Inspector: script " +
                 "này tự đăng ký, nối thêm tay là mỗi cú bấm chạy hai lần.")]
        [SerializeField] private Toggle _musicToggle;

        [Tooltip("Toggle bật/tắt TIẾNG ĐỘNG — kéo tglSound vào đây.")]
        [SerializeField] private Toggle _soundToggle;

        [Header("Icon đổi theo trạng thái — để trống cũng chạy")]
        [Tooltip("Hiện khi nhạc ĐANG BẬT. Chỉ cần khi bạn muốn đổi hẳn hình thay vì dùng " +
                 "dấu tick sẵn có của Toggle.")]
        [SerializeField] private GameObject _musicOnIcon;

        [Tooltip("Hiện khi nhạc ĐANG TẮT.")]
        [SerializeField] private GameObject _musicOffIcon;

        [SerializeField] private GameObject _soundOnIcon;
        [SerializeField] private GameObject _soundOffIcon;

        [Header("Điều hướng")]
        [Tooltip("Về màn hình Home. ĐỂ TRỐNG ở bản popup mở từ chính Home.")]
        [SerializeField] private Button _homeButton;

        [SerializeField] private Button _closeButton;

        private ISoundService _sound;
        private HomeScreenView _home;
        private HudView _hud;
        private IFreePaintService _freePaint;

        [Inject]
        public void Construct(ISoundService sound, HomeScreenView home, HudView hud,
            IFreePaintService freePaint)
        {
            _sound = sound;
            _home = home;
            _hud = hud;
            _freePaint = freePaint;
        }

        private void Awake()
        {
            if (_musicToggle != null) _musicToggle.onValueChanged.AddListener(HandleMusicToggled);
            if (_soundToggle != null) _soundToggle.onValueChanged.AddListener(HandleSoundToggled);
            if (_homeButton != null) _homeButton.onClick.AddListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_musicToggle != null) _musicToggle.onValueChanged.RemoveListener(HandleMusicToggled);
            if (_soundToggle != null) _soundToggle.onValueChanged.RemoveListener(HandleSoundToggled);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(HandleHomeClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// Đọc lại trạng thái ở MỖI lần mở, không phải ở Awake: popup sống suốt phiên
        /// chơi, mà hai bản popup lại chỉnh chung một cặp công tắc.
        public override void Show()
        {
            base.Show();

            // Giữ đồng hồ của booster tô tự do lại. Bảng cài đặt che kín bàn chơi, nên
            // mỗi giây trôi qua sau lưng nó là một giây người chơi đã trả tiền mà không
            // tô được ô nào.
            //
            // Giữ chứ không huỷ: họ mở bảng này để tắt nhạc rồi chơi tiếp, không phải để
            // vứt lượt booster đi.
            if (_freePaint != null) _freePaint.SetPaused(true);

            RefreshControls();
        }

        /// Thả đồng hồ ở ĐÂY chứ không ở nút đóng.
        ///
        /// Popup này có ba đường ra — nút đóng, nút Home, và HideAll gọi từ chỗ khác —
        /// và cả ba đều đi qua Hide. Móc vào riêng nút đóng thì hai đường kia để đồng hồ
        /// đứng nguyên, và người chơi quay lại thấy booster treo ở một con số không bao
        /// giờ nhúc nhích.
        public override void Hide()
        {
            if (_freePaint != null) _freePaint.SetPaused(false);

            base.Hide();
        }

        private void HandleMusicToggled(bool isOn)
        {
            if (_sound == null) return;

            // Phát TRƯỚC khi đổi công tắc: đây là công tắc NHẠC nên tiếng động không bị
            // nó tắt, nhưng phát trước vẫn đúng hơn về nhịp — người chơi nghe cú bấm rồi
            // mới nghe nhạc đổi.
            _sound.Play(SoundKey.Direction);

            _sound.SetMusicEnabled(isOn);
            RefreshIcons();
        }

        private void HandleSoundToggled(bool isOn)
        {
            // Phát TRƯỚC khi đổi công tắc, và ở đây thì bắt buộc: tắt tiếng xong mới gọi
            // Play là không ai nghe thấy gì, nên cú bấm TẮT sẽ im lặng còn cú bấm BẬT thì
            // kêu — lệch nhau một cách khó hiểu. Phát trước thì cả hai chiều đều kêu đúng
            // một tiếng, và tiếng đó cũng chính là thứ xác nhận âm thanh vừa được bật.
            if (_sound == null) return;

            _sound.Play(SoundKey.Direction);

            _sound.SetSoundEnabled(isOn);
            RefreshIcons();
        }

        /// Kéo hai cái Toggle về đúng trạng thái thật, rồi mới tới phần icon.
        ///
        /// SetIsOnWithoutNotify chứ KHÔNG gán isOn.
        ///
        /// Gán isOn sẽ bắn onValueChanged, mà handler của nó lại đi ghi đĩa và phát tiếng
        /// — nên mỗi lần MỞ bảng cài đặt sẽ kêu một tiếng Direction không ai bấm, và bản
        /// lưu bị viết lại bằng chính giá trị vừa đọc ra. Đây là vòng lặp kinh điển của
        /// Toggle: đọc trạng thái để hiện, hoá ra lại ghi trạng thái.
        private void RefreshControls()
        {
            var music = _sound != null && _sound.IsMusicEnabled;
            var sound = _sound != null && _sound.IsSoundEnabled;

            if (_musicToggle != null) _musicToggle.SetIsOnWithoutNotify(music);
            if (_soundToggle != null) _soundToggle.SetIsOnWithoutNotify(sound);

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
            if (_sound != null) _sound.Play(SoundKey.Direction);

            // HideSilently: cú bấm này đã có tiếng Direction rồi, kêu thêm Cancel là hai
            // tiếng chồng lên nhau trong cùng một khoảnh khắc.
            HideSilently();

            // Hide vừa thả đồng hồ ra — giữ lại. Về Home là rời hẳn bàn chơi: booster sẽ
            // bị huỷ ở lần nạp màn kế tiếp, nên để nó đếm tiếp sau lưng màn hình Home chỉ
            // tổ đốt nốt mấy giây cuối vào chỗ không ai nhìn.
            if (_freePaint != null) _freePaint.SetPaused(true);

            if (_hud != null) _hud.SetVisible(false);
            if (_home != null) _home.Show();
        }
    }
}
