using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Phép chỉnh màu đưa từ màu ĐẤT sang màu VIÊN NGỌC, dùng chung cho mọi màn.
    ///
    /// Một asset cho cả game chứ không phải mỗi màn một bộ số: viên ngọc là một loại
    /// vật liệu, và vật liệu thì không đổi theo bức tranh đang tô. Nếu về sau cần một
    /// màn lệch khỏi mặc định thì thêm ô ghi đè ở LevelConfig — KHÔNG phải ở
    /// LevelGridData, vì tool sinh ảnh ghi đè trọn asset đó mỗi lần chạy.
    ///
    /// Bảng chỉ vài màu nên phép chỉnh chạy một lần lúc vào màn, không phải mỗi viên.
    [CreateAssetMenu(
        fileName = "JewelTintConfig",
        menuName = "JewelPainter/Gameplay/Jewel Tint Config")]
    public class JewelTintConfig : ScriptableObject
    {
        [SerializeField] private ColorAdjustment _tint;

        public ColorAdjustment Tint => _tint;

#if UNITY_EDITOR
        /// Chỉ dành cho cửa sổ chỉnh màu. Không gọi lúc chạy game.
        public void SetTint(ColorAdjustment tint) => _tint = tint;
#endif
    }
}
