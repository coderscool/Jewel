using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Bộ số của từng mặt cắt viên ngọc — NGUỒN sinh ra Jewel_Params.png.
    ///
    /// Vì sao cần asset này: ảnh tham số là thứ đã nướng chín, mở ra chỉ thấy ba kênh
    /// số chứ không đọc ngược ra được "mặt đỉnh pha trắng bao nhiêu". Muốn kéo lại một
    /// mặt thì phải còn giữ bộ số gốc ở đâu đó. Để trong asset thì nó đi cùng project,
    /// vào được git, và người sau mở ra vẫn nướng lại được.
    ///
    /// Asset này CHỈ SỐNG LÚC THIẾT KẾ. Game không đọc nó — game chỉ đọc ảnh đã nướng.
    /// Nên sửa ở đây mà quên bấm nướng thì trong game không đổi gì.
    public class JewelFacetProfile : ScriptableObject
    {
        public const int FacetCount = 8;

        /// Tên tám mặt ngoài, theo đúng thứ tự cạnh của bát giác tính từ cạnh trên.
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

        [Tooltip("Bề rộng viền, tính theo ảnh 256 pixel. Đổi số này phải nướng lại ảnh.")]
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

        /// Viền cố ý mang RỰC CAO chứ không chỉ dìm tối: dìm không thôi cho ra một
        /// vòng nâu đen, nhìn như bóng đổ dính vào chứ không như chính viên ngọc sẫm
        /// lại ở mép. Đẩy rực lên thì ô cam ra viền cam sẫm, ô lục ra viền lục sẫm.
        ///
        /// Bề rộng viền theo thang ảnh 256 pixel. Bộ sinh ảnh tự quy về tỉ lệ khung,
        /// nên đổi độ phân giải ảnh thì viền vẫn dày đúng bằng ngần ấy phần khung.
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

        /// Mảng serialize có thể ngắn đi khi ai đó sửa tay file asset. Vá lại thay vì
        /// để IndexOutOfRange nổ giữa lúc vẽ cửa sổ.
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
        ///
        /// Theo ảnh mẫu thì hai mặt bên đúng bằng màu ô — đó là mốc của cả bộ số. Nhưng
        /// trong game viên ngọc NẰM TRÊN chính màu ô đó, nên hai mặt ấy tan vào nền và
        /// viên ngọc mất hẳn cạnh trái phải.
        ///
        /// Bộ số của hai mặt này CỐ Ý KHÔNG thuần tỉ lệ như các mặt khác: nó là
        /// nhân 0.94 rồi trừ thẳng 0.035. Phần trừ thẳng mới là chỗ quan trọng —
        /// phép nhân cho độ lệch tỉ lệ với màu ô, nên ô sẫm gần như không lệch gì.
        /// Mặt đỉnh cần thuần tỉ lệ vì đích của nó là CHẠM TRẮNG; hai mặt bên thì
        /// đích là LỆCH ĐỦ THẤY, mà lệch đủ thấy là một lượng tuyệt đối.
        ///
        /// Hai mặt bên còn kèm một lượng TĂNG RỰC. Trừ thẳng làm màu tối đi mà không
        /// đậm thêm, nhìn ra đục chứ không ra tối; tăng rực bù lại phần chất màu bị
        /// mất, nên hai mặt ấy vẫn là cùng một màu chứ không thành màu xám bẩn.
        ///
        /// Hai mặt chéo dưới đã hạ khỏi bộ số dò từ ảnh mẫu (rực 2.22, pha đen 0.335).
        /// Ảnh mẫu chỉ có một viên trên nền giấy, đậm cỡ nào cũng đọc được; cả bảng
        /// bảy màu cạnh nhau thì hai mảng ấy nhảy ra khỏi phần còn lại của viên.
        ///
        /// Mặt bàn và hai mặt chéo trên cũng đã bớt pha trắng (0.445 xuống 0.28) và
        /// thêm rực. Pha trắng nhiều thì màu sẫm hoá phấn: ô xanh lá đậm cho ra mặt
        /// bàn xám xanh, không còn nhận ra là cùng một màu với phần dưới.
        ///
        /// Ba mặt phía dưới (bên, đáy, chéo dưới) đều mang một lượng TRỪ THẲNG tăng
        /// dần, không chỉ nhân. Nhân thôi thì trên màu sẫm ba mặt xích lại gần nhau
        /// tới mức nhìn ra cùng một màu — phần trừ thẳng mới giữ được bậc thang.
        ///
        /// Mọi mặt đều là phép PHA THEO TỈ LỆ quanh màu ô:
        /// pha về trắng lượng t thì (tương phản, sáng) = (-t, +t/2), về đen thì (-t, -t/2).
        /// Giữ đúng quan hệ này thì màu tối và màu sáng co giãn cùng tỉ lệ, và núm
        /// "Độ trắng mặt đỉnh" trong shader mới tách được mặt loé ra khỏi mặt bàn.
        private static ColorAdjustment[] DefaultFacets()
        {
            return new[]
            {
                new ColorAdjustment(0.98f, -0.885f, 0.4425f),    // đỉnh
                new ColorAdjustment(0.55f, -0.28f, 0.14f),       // chéo trên-phải
                new ColorAdjustment(0.45f, -0.05f, -0.055f),     // bên phải
                new ColorAdjustment(0.85f, -0.13f, -0.15f),      // chéo dưới-phải
                new ColorAdjustment(0.55f, -0.08f, -0.095f),     // đáy
                new ColorAdjustment(0.85f, -0.13f, -0.15f),      // chéo dưới-trái
                new ColorAdjustment(0.45f, -0.05f, -0.055f),     // bên trái
                new ColorAdjustment(0.55f, -0.28f, 0.14f),       // chéo trên-trái
            };
        }
    }
}
