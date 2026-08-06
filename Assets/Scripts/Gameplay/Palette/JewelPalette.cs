using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Palette
{
    /// Bảng màu dùng chung cho toàn game. Mọi màn chơi tham chiếu cùng một asset,
    /// nhờ vậy thanh chọn màu ở UI cố định và sprite viên ngọc tái dùng được.
    ///
    /// Dictionary không serialize được trong Unity nên dùng List&lt;Entry&gt;.
    [CreateAssetMenu(fileName = "JewelPalette", menuName = "JewelPainter/Gameplay/Jewel Palette")]
    public class JewelPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public Color32 color;

            [Tooltip("Viên ngọc đặt lên ô khi người chơi tô đúng màu này. " +
                     "Vẽ ở 128x128 hoặc 256x256, Pixels Per Unit đặt đúng bằng cạnh ảnh, " +
                     "tắt Mip Maps — nếu không sẽ mờ khi phóng to.")]
            public GameObject jewelPrefab;
        }

        [SerializeField]
        private List<Entry> _entries = new()
        {
            new Entry { name = "Đen",             color = new Color32(26, 26, 26, 255) },
            new Entry { name = "Xám đậm",         color = new Color32(90, 90, 90, 255) },
            new Entry { name = "Xám nhạt",        color = new Color32(175, 175, 175, 255) },
            new Entry { name = "Trắng",           color = new Color32(250, 250, 250, 255) },
            new Entry { name = "Đỏ",              color = new Color32(220, 50, 50, 255) },
            new Entry { name = "Hồng",            color = new Color32(240, 130, 170, 255) },
            new Entry { name = "Cam",             color = new Color32(240, 140, 50, 255) },
            new Entry { name = "Vàng",            color = new Color32(245, 210, 70, 255) },
            new Entry { name = "Xanh lá đậm",     color = new Color32(45, 120, 60, 255) },
            new Entry { name = "Xanh lá nhạt",    color = new Color32(120, 195, 90, 255) },
            new Entry { name = "Xanh ngọc",       color = new Color32(60, 190, 180, 255) },
            new Entry { name = "Xanh dương đậm",  color = new Color32(40, 80, 170, 255) },
            new Entry { name = "Xanh dương nhạt", color = new Color32(95, 160, 230, 255) },
            new Entry { name = "Tím",             color = new Color32(140, 80, 190, 255) },
            new Entry { name = "Nâu",             color = new Color32(120, 80, 50, 255) },
            new Entry { name = "Be",              color = new Color32(225, 200, 165, 255) },
        };

        private List<Color32> _colorCache;

        public IReadOnlyList<Entry> Entries => _entries;

        /// Danh sách màu phẳng để PaletteMatcher dùng. Dựng lại khi số lượng đổi
        /// (người dùng thêm hoặc bớt màu trong Inspector).
        public IReadOnlyList<Color32> Colors
        {
            get
            {
                if (_colorCache != null && _colorCache.Count == _entries.Count) return _colorCache;

                _colorCache = new List<Color32>(_entries.Count);
                foreach (var entry in _entries) _colorCache.Add(entry.color);

                return _colorCache;
            }
        }

        /// null nếu chỉ số nằm ngoài bảng hoặc dòng đó chưa gán prefab.
        public GameObject GetJewelPrefab(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex >= _entries.Count) return null;

            return _entries[paletteIndex].jewelPrefab;
        }
    }
}
