using System.Collections.Generic;
using JewelPainter.Core.Persistence;
using UnityEngine;

namespace JewelPainter.Core.Services
{
    /// Giữ AudioSource, tra clip theo key, phát. Trạng thái bật/tắt uỷ cho ISaveService —
    /// service này không biết PlayerPrefs.
    ///
    /// Hai phần tách hẳn nhau:
    ///
    /// TIẾNG ĐỘNG đi qua một DÀN AudioSource quay vòng, không phải PlayOneShot trên một
    /// nguồn duy nhất. PlayOneShot dùng chung pitch của cái nguồn đó, nên đổi cao độ cho
    /// một tiếng là đổi luôn cho mọi tiếng đang ngân — với tiếng Pop bắn liên tục thì đó
    /// là cả một dàn ngọc cùng méo giọng một lúc. Mỗi nguồn riêng thì mỗi tiếng giữ đúng
    /// cao độ của nó, và số nguồn cũng chính là trần số tiếng chồng nhau.
    ///
    /// NHẠC NỀN đi qua hai nguồn để fade chéo được: bản cũ nhỏ dần trong khi bản mới to
    /// dần, thay vì cắt phựt.
    public class SoundService : MonoBehaviour, ISoundService
    {
        [SerializeField] private SoundConfig _config;

        [Tooltip("Nguồn phát tiếng động. Dàn nguồn quay vòng được nhân bản TỪ nó lúc chạy, " +
                 "nên mọi thiết lập (Output Mixer, Spatial Blend...) đặt ở đây là đủ.")]
        [SerializeField] private AudioSource _sfxSource;

        [Tooltip("Nguồn nhạc nền thứ nhất.")]
        [SerializeField] private AudioSource _musicSource;

        [Tooltip("Nguồn nhạc nền thứ hai, dùng để fade chéo. ĐỂ TRỐNG thì nhạc vẫn chuyển " +
                 "được, chỉ là cắt thẳng sang bản mới không có quãng giao.")]
        [SerializeField] private AudioSource _musicSourceB;

        [Tooltip("Số tiếng động được phép chồng lên nhau. Vượt quá thì tiếng CŨ NHẤT bị " +
                 "cắt để nhường chỗ.\n\n" +
                 "12 là thoải mái cho đợt tô của booster: nhịp bắn đã bị Min Interval của " +
                 "tiếng Pop ghìm lại từ trước rồi.")]
        [Range(1, 32)]
        [SerializeField] private int _sfxVoices = 12;

        [Tooltip("Thời gian fade chéo giữa hai bản nhạc, tính bằng giây.")]
        [SerializeField] private float _musicFadeDuration = 0.6f;

        private readonly Dictionary<SoundKey, SoundConfig.Entry> _clips = new();
        private readonly Dictionary<MusicKey, SoundConfig.MusicEntry> _musicClips = new();

        /// Lần cuối mỗi key được phát, theo đồng hồ KHÔNG phụ thuộc timeScale. Dùng
        /// unscaled vì tiếng động vẫn phải kêu bình thường khi game bị dừng bằng
        /// timeScale = 0 — mở popup chẳng hạn.
        private readonly Dictionary<SoundKey, float> _lastPlayed = new();

        private AudioSource[] _voices;
        private int _nextVoice;

        private ISaveService _save;
        private bool _isSoundEnabled;
        private bool _isMusicEnabled;

        private AudioSource _activeMusic;
        private AudioSource _fadingMusic;
        private float _fadeElapsed;
        private float _fadeFromVolume;
        private float _fadeToVolume;
        private bool _isFading;

        public bool IsSoundEnabled => _isSoundEnabled;
        public bool IsMusicEnabled => _isMusicEnabled;

        public MusicKey CurrentMusic { get; private set; } = MusicKey.None;

        /// Bootstrap gọi trước khi dùng. Không tự đi tìm phụ thuộc.
        public void Init(ISaveService save)
        {
            _save = save;
            _isSoundEnabled = _save.GetBool(PreferenceKeys.SoundEnabled, true);
            _isMusicEnabled = _save.GetBool(PreferenceKeys.MusicEnabled, true);

            ApplyMusicState();
        }

        private void Awake()
        {
            BuildVoices();

            if (_config == null) return;

            foreach (var entry in _config.Entries)
            {
                if (entry.clip == null) continue;

                _clips[entry.key] = entry;
            }

            foreach (var entry in _config.Music)
            {
                if (entry.clip == null) continue;

                _musicClips[entry.key] = entry;
            }
        }

        /// Nhân bản nguồn mẫu thay vì tạo AudioSource trắng: người dựng chỉnh Output
        /// Mixer, Spatial Blend, Bypass Effects... trên nguồn mẫu, và một nguồn trắng
        /// sẽ bỏ qua sạch những thiết lập đó mà không báo gì.
        private void BuildVoices()
        {
            if (_sfxSource == null) return;

            var count = Mathf.Max(1, _sfxVoices);
            _voices = new AudioSource[count];
            _voices[0] = _sfxSource;

            for (var i = 1; i < count; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();

                voice.clip = null;
                voice.playOnAwake = false;
                voice.loop = false;
                voice.outputAudioMixerGroup = _sfxSource.outputAudioMixerGroup;
                voice.spatialBlend = _sfxSource.spatialBlend;
                voice.bypassEffects = _sfxSource.bypassEffects;
                voice.bypassListenerEffects = _sfxSource.bypassListenerEffects;
                voice.ignoreListenerPause = _sfxSource.ignoreListenerPause;

                _voices[i] = voice;
            }
        }

        public void Play(SoundKey key)
        {
            if (!_isSoundEnabled) return;
            if (_voices == null || _voices.Length == 0) return;
            if (!_clips.TryGetValue(key, out var entry)) return;

            var now = Time.unscaledTime;

            // Chặn theo TỪNG key, không phải chặn chung. Tiếng Pop dày đặc không được
            // phép nuốt mất tiếng bấm nút xảy ra cùng lúc.
            if (entry.minInterval > 0f
                && _lastPlayed.TryGetValue(key, out var last)
                && now - last < entry.minInterval)
            {
                return;
            }

            _lastPlayed[key] = now;

            var voice = NextVoice();

            voice.clip = entry.clip;
            voice.volume = entry.volume > 0f ? entry.volume : 1f;
            voice.pitch = entry.pitchVariance > 0f
                ? 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance)
                : 1f;

            voice.Play();
        }

        /// Ưu tiên một nguồn đang RẢNH; hết rảnh thì cướp nguồn kế tiếp trong vòng quay.
        ///
        /// Quay vòng chứ không đi tìm "nguồn kêu lâu nhất": muốn biết cái nào cũ nhất thì
        /// phải nhớ thêm mốc thời gian cho từng nguồn, mà kết quả gần như trùng với vòng
        /// quay — nguồn được cướp cũng chính là nguồn đã thuê từ lâu nhất.
        private AudioSource NextVoice()
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                var index = (_nextVoice + i) % _voices.Length;
                if (_voices[index].isPlaying) continue;

                _nextVoice = (index + 1) % _voices.Length;
                return _voices[index];
            }

            var stolen = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            return stolen;
        }

        public void PlayMusic(MusicKey key)
        {
            // Gọi lại đúng bản đang chạy thì im lặng bỏ qua. Nhờ vậy bên gọi cứ gọi vô tư
            // ở mỗi lần mở màn hình mà không phải tự nhớ mình đang phát bản nào — và
            // nhạc không bị giật lại từ đầu mỗi lần mở popup rồi đóng.
            if (key == CurrentMusic) return;

            CurrentMusic = key;

            ApplyMusicState();
        }

        public void StopMusic() => PlayMusic(MusicKey.None);

        public void SetSoundEnabled(bool enabled)
        {
            _isSoundEnabled = enabled;
            _save.SetBool(PreferenceKeys.SoundEnabled, enabled);

            if (enabled || _voices == null) return;

            // Cắt luôn những tiếng đang ngân. Tắt tiếng mà vẫn nghe nốt một tiếng Pop dài
            // thì người chơi tưởng công tắc hỏng.
            foreach (var voice in _voices)
            {
                if (voice != null) voice.Stop();
            }
        }

        public void SetMusicEnabled(bool enabled)
        {
            _isMusicEnabled = enabled;
            _save.SetBool(PreferenceKeys.MusicEnabled, enabled);

            ApplyMusicState();
        }

        /// Đưa phần nhạc về đúng trạng thái nó PHẢI ở: đúng bản, đúng bật/tắt.
        ///
        /// Một hàm duy nhất cho cả ba đường vào (đổi bản, bật/tắt công tắc, nạp thiết lập
        /// lúc khởi động) thay vì mỗi đường tự xử. Ba đường tự xử là ba chỗ phải nhớ sửa
        /// mỗi lần thêm một luật, và chỗ bị quên thì im lặng chạy sai.
        private void ApplyMusicState()
        {
            if (_musicSource == null) return;

            if (!_isMusicEnabled)
            {
                _isFading = false;

                _musicSource.Stop();
                if (_musicSourceB != null) _musicSourceB.Stop();

                return;
            }

            if (CurrentMusic == MusicKey.None || !_musicClips.TryGetValue(CurrentMusic, out var entry))
            {
                _isFading = false;

                _musicSource.Stop();
                if (_musicSourceB != null) _musicSourceB.Stop();

                return;
            }

            var target = entry.volume > 0f ? entry.volume : 1f;

            if (_activeMusic == null) _activeMusic = _musicSource;

            // Đang phát đúng clip đó rồi thì chỉ chỉnh âm lượng. Xảy ra khi người chơi
            // tắt nhạc rồi bật lại mà không đi đâu cả.
            if (_activeMusic.clip == entry.clip && _activeMusic.isPlaying)
            {
                _activeMusic.volume = target;
                return;
            }

            var next = PickIdleSource();

            next.clip = entry.clip;
            next.loop = true;
            next.volume = _musicSourceB != null ? 0f : target;
            next.Play();

            _fadingMusic = _activeMusic != next ? _activeMusic : null;
            _activeMusic = next;

            if (_musicSourceB == null || _musicFadeDuration <= 0f)
            {
                _activeMusic.volume = target;
                if (_fadingMusic != null) _fadingMusic.Stop();

                _isFading = false;
                return;
            }

            _fadeElapsed = 0f;
            _fadeFromVolume = _fadingMusic != null ? _fadingMusic.volume : 0f;
            _fadeToVolume = target;
            _isFading = true;
        }

        private AudioSource PickIdleSource()
        {
            if (_musicSourceB == null) return _musicSource;

            return _activeMusic == _musicSource ? _musicSourceB : _musicSource;
        }

        private void Update()
        {
            if (!_isFading) return;

            // Thời gian KHÔNG phụ thuộc timeScale: nhạc phải chuyển bình thường kể cả khi
            // game đang bị dừng — mà đổi màn hình thì rất hay đi kèm một popup đang mở.
            _fadeElapsed += Time.unscaledDeltaTime;

            var t = Mathf.Clamp01(_fadeElapsed / Mathf.Max(0.01f, _musicFadeDuration));

            if (_activeMusic != null) _activeMusic.volume = Mathf.Lerp(0f, _fadeToVolume, t);
            if (_fadingMusic != null) _fadingMusic.volume = Mathf.Lerp(_fadeFromVolume, 0f, t);

            if (t < 1f) return;

            _isFading = false;

            if (_fadingMusic == null) return;

            _fadingMusic.Stop();
            _fadingMusic = null;
        }
    }
}
