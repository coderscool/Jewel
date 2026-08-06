using System.Collections.Generic;
using JewelPainter.Core.Persistence;
using UnityEngine;

namespace JewelPainter.Core.Services
{
    /// MonoBehaviour mỏng: giữ AudioSource, tra clip theo key, phát.
    /// Trạng thái bật/tắt uỷ cho ISaveService — service này không biết PlayerPrefs.
    [RequireComponent(typeof(AudioSource))]
    public class SoundService : MonoBehaviour, ISoundService
    {
        [SerializeField] private SoundConfig _config;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _musicSource;

        private readonly Dictionary<SoundKey, AudioClip> _clips = new();
        private ISaveService _save;
        private bool _isSoundEnabled;
        private bool _isMusicEnabled;

        public bool IsSoundEnabled => _isSoundEnabled;
        public bool IsMusicEnabled => _isMusicEnabled;

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
            if (_config == null) return;

            foreach (var entry in _config.Entries)
            {
                if (entry.clip == null) continue;
                _clips[entry.key] = entry.clip;
            }
        }

        public void Play(SoundKey key)
        {
            if (!_isSoundEnabled) return;
            if (!_clips.TryGetValue(key, out var clip)) return;

            _sfxSource.PlayOneShot(clip);
        }

        public void SetSoundEnabled(bool enabled)
        {
            _isSoundEnabled = enabled;
            _save.SetBool(PreferenceKeys.SoundEnabled, enabled);
        }

        public void SetMusicEnabled(bool enabled)
        {
            _isMusicEnabled = enabled;
            _save.SetBool(PreferenceKeys.MusicEnabled, enabled);
            ApplyMusicState();
        }

        private void ApplyMusicState()
        {
            if (_musicSource == null) return;

            if (_isMusicEnabled) _musicSource.Play();
            else _musicSource.Stop();
        }
    }
}
