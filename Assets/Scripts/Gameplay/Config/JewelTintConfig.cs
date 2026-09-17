using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Phép chỉnh màu đưa từ màu đất sang màu viên ngọc, dùng chung cho mọi màn.
    [CreateAssetMenu(
        fileName = "JewelTintConfig",
        menuName = "JewelPainter/Gameplay/Jewel Tint Config")]
    public class JewelTintConfig : ScriptableObject
    {
        /// Một màu đất được chỉ định thẳng màu ngọc, không đi qua Tint.
        [Serializable]
        public struct ColorOverride
        {
            [Tooltip("Màu đất trong ảnh gốc.")]
            public Color32 ground;

            [Tooltip("Màu viên ngọc dùng thay.")]
            public Color32 jewel;
        }

        [SerializeField] private ColorAdjustment _tint;

        [Tooltip("Ghi đè màu ngọc cho từng màu đất cụ thể.")]
        [SerializeField] private List<ColorOverride> _overrides = new();

        private Dictionary<int, Color32> _lookup;

        public ColorAdjustment Tint => _tint;

        public bool HasOverrides => _overrides != null && _overrides.Count > 0;

        /// Tìm màu ngọc chỉ định cho một màu đất.
        public bool TryGetOverride(Color32 ground, out Color32 jewel)
        {
            if (!HasOverrides)
            {
                jewel = ground;
                return false;
            }

            if (_lookup == null) BuildLookup();

            return _lookup.TryGetValue(Pack(ground), out jewel);
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<int, Color32>(_overrides.Count);

            foreach (var entry in _overrides)
            {
                _lookup[Pack(entry.ground)] = entry.jewel;
            }
        }

        /// Đổi màu thành khoá tra, bỏ alpha.
        private static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        /// Dựng lại bảng tra khi danh sách đổi.
        private void OnValidate() => _lookup = null;

        /// Đặt phép chỉnh màu từ cửa sổ chỉnh màu.
        public void SetTint(ColorAdjustment tint) => _tint = tint;
#endif
    }
}
