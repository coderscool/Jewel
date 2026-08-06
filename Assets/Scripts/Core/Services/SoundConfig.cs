using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Core.Services
{
    /// Dictionary KHÔNG serialize được trong Unity — khai bằng List<Entry>,
    /// SoundService sẽ build Dictionary ở Awake để tra cứu O(1).
    [CreateAssetMenu(fileName = "SoundConfig", menuName = "JewelPainter/Core/Sound Config")]
    public class SoundConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SoundKey key;
            public AudioClip clip;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
