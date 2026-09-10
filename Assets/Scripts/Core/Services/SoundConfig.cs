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

            [Tooltip("Hai tiếng CÙNG key phải cách nhau ít nhất ngần này giây. 0 là không " +
                     "chặn gì cả.\n\n" +
                     "Đây là núm cứu tiếng Pop: booster tô hết màu đặt hàng chục viên ngọc " +
                     "mỗi frame, mà mấy chục bản sao của cùng một clip chồng lên nhau trong " +
                     "một phần trăm giây thì không đọc ra thành tiếng nào cả — chỉ là một " +
                     "cú vỡ âm. 0.05 cho Pop là chừng 20 tiếng mỗi giây, đủ dày để nghe " +
                     "thành một tràng mà vẫn tách ra được từng tiếng.")]
            public float minInterval;

            [Tooltip("Lệch cao độ ngẫu nhiên mỗi lần phát, theo tỉ lệ. 0.08 là ±8%.\n\n" +
                     "Một clip phát đi phát lại y hệt nhau mấy chục lần liền là thứ tai " +
                     "người nhận ra ngay và thấy rẻ tiền. Xê dịch một chút thì cùng một " +
                     "file nghe ra thành nhiều tiếng khác nhau.")]
            [Range(0f, 0.5f)]
            public float pitchVariance;

            [Tooltip("Âm lượng riêng của tiếng này, 0..1.\n\n" +
                     "ĐỂ 0 nghĩa là dùng 1 (to hết cỡ) — không phải im lặng. Quy ước hơi " +
                     "ngược đời nhưng cố ý: ô này thêm vào sau, mà mọi dòng đã gán từ " +
                     "trước đều mang sẵn số 0, và diễn 0 thành 'im' sẽ làm cả bảng câm " +
                     "ngay lần mở đầu tiên.")]
            [Range(0f, 1f)]
            public float volume;
        }

        [Serializable]
        public struct MusicEntry
        {
            public MusicKey key;
            public AudioClip clip;

            [Tooltip("Âm lượng của bản nhạc này. Cùng quy ước với Entry: để 0 là dùng 1.")]
            [Range(0f, 1f)]
            public float volume;
        }

        [SerializeField] private List<Entry> _entries = new();

        [Tooltip("Nhạc nền. Mỗi lúc chỉ có một bản chạy; SoundService lo phần chuyển qua lại.")]
        [SerializeField] private List<MusicEntry> _music = new();

        public IReadOnlyList<Entry> Entries => _entries;

        public IReadOnlyList<MusicEntry> Music => _music;
    }
}
