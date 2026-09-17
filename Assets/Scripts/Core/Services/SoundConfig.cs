using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Core.Services
{
    /// Bảng cấu hình tiếng động và nhạc nền.
    [CreateAssetMenu(fileName = "SoundConfig", menuName = "JewelPainter/Core/Sound Config")]
    public class SoundConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SoundKey key;
            public AudioClip clip;

            [Tooltip("Khoảng cách tối thiểu giữa hai lần phát cùng key, tính bằng giây.")]
            public float minInterval;

            [Tooltip("Lệch cao độ ngẫu nhiên mỗi lần phát, theo tỉ lệ.")]
            [Range(0f, 0.5f)]
            public float pitchVariance;

            [Tooltip("Âm lượng riêng của tiếng này, 0..1.")]
            [Range(0f, 1f)]
            public float volume;
        }

        [Serializable]
        public struct MusicEntry
        {
            public MusicKey key;
            public AudioClip clip;

            [Tooltip("Âm lượng của bản nhạc này.")]
            [Range(0f, 1f)]
            public float volume;
        }

        [SerializeField] private List<Entry> _entries = new();

        [Tooltip("Danh sách nhạc nền.")]
        [SerializeField] private List<MusicEntry> _music = new();

        public IReadOnlyList<Entry> Entries => _entries;

        public IReadOnlyList<MusicEntry> Music => _music;
    }
}
