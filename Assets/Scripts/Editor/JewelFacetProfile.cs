using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Bộ số của từng mặt cắt viên ngọc — nguồn sinh ra Jewel_Params.png.
    public class JewelFacetProfile : ScriptableObject
    {
        public const int FacetCount = 8;

        public static readonly string[] FacetNames =
        {
            "Đỉnh (trên)",
            "Chéo trên-phải",
            "Bên phải",
            "Chéo dưới-phải",
            "Đáy (dưới)",
            "Chéo dưới-trái",
            "Bên trái",
            "Chéo trên-trái",
        };

        [SerializeField] private ColorAdjustment[] _facets = DefaultFacets();
        [SerializeField] private ColorAdjustment _table = new ColorAdjustment(0.55f, -0.28f, 0.14f);
        [SerializeField] private ColorAdjustment _seam = new ColorAdjustment(0.55f, -0.38f, 0.19f);
        [SerializeField] private ColorAdjustment _outline = new ColorAdjustment(1f, -0.32f, -0.19f);

        [Tooltip("Bề rộng viền, tính theo ảnh 256 pixel.")]
        [Range(1f, 24f)]
        [SerializeField] private float _outlineWidth = 12f;

        public ColorAdjustment Table
        {
            get => _table;
            set => _table = value;
        }

        public ColorAdjustment Seam
        {
            get => _seam;
            set => _seam = value;
        }

        public ColorAdjustment Outline
        {
            get => _outline;
            set => _outline = value;
        }

        public float OutlineWidth
        {
            get => _outlineWidth;
            set => _outlineWidth = value;
        }

        public ColorAdjustment GetFacet(int index)
        {
            EnsureSize();
            return _facets[index];
        }

        public void SetFacet(int index, ColorAdjustment value)
        {
            EnsureSize();
            _facets[index] = value;
        }

        /// Đảm bảo mảng mặt cắt đủ độ dài.
        private void EnsureSize()
        {
            if (_facets != null && _facets.Length == FacetCount) return;

            var defaults = DefaultFacets();

            if (_facets != null)
            {
                for (var i = 0; i < Mathf.Min(_facets.Length, FacetCount); i++) defaults[i] = _facets[i];
            }

            _facets = defaults;
        }

        /// Bộ số dò từ ảnh mẫu, riêng hai mặt bên đã dìm nhẹ.
        private static ColorAdjustment[] DefaultFacets()
        {
            return new[]
            {
                new ColorAdjustment(0.98f, -0.885f, 0.4425f),
                new ColorAdjustment(0.55f, -0.28f, 0.14f),
                new ColorAdjustment(0.45f, -0.05f, -0.055f),
                new ColorAdjustment(0.85f, -0.13f, -0.15f),
                new ColorAdjustment(0.55f, -0.08f, -0.095f),
                new ColorAdjustment(0.85f, -0.13f, -0.15f),
                new ColorAdjustment(0.45f, -0.05f, -0.055f),
                new ColorAdjustment(0.55f, -0.28f, 0.14f),
            };
        }
    }
}
