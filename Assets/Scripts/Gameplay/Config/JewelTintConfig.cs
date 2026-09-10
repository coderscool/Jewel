using System;
using System.Collections.Generic;
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
        /// Một màu đất được chỉ định thẳng màu ngọc, không đi qua Tint.
        [Serializable]
        public struct ColorOverride
        {
            [Tooltip("Màu ĐẤT — đúng màu có trong ảnh gốc. Khớp chính xác cả ba kênh R, " +
                     "G, B; alpha bỏ qua.")]
            public Color32 ground;

            [Tooltip("Màu viên ngọc dùng thay. Thay HẲN — dòng này không đi qua Tint nữa.")]
            public Color32 jewel;
        }

        [SerializeField] private ColorAdjustment _tint;

        [Tooltip("Ghi đè màu ngọc cho TỪNG màu đất cụ thể.\n\n" +
                 "Dùng khi một màu nào đó ra viên ngọc xấu mà cả bảng còn lại thì đang " +
                 "đẹp. Màu không có tên trong danh sách này không bị đụng tới một byte " +
                 "nào — đó là cả điểm của nó.\n\n" +
                 "Vì sao cần: shader tạo mặt cắt bằng cách ĐẨY ĐỘ BÃO HOÀ. Màu nào đã nằm " +
                 "sát góc gamut (một kênh gần 0, một kênh gần 255) thì không bão hoà thêm " +
                 "được nữa, saturate() kẹp mọi mặt tối về cùng một điểm, và đáy viên ngọc " +
                 "thành một mảng phẳng. Nâng kênh thấp nhất lên vài chục đơn vị là trả lại " +
                 "chỗ xoay cho nó.")]
        [SerializeField] private List<ColorOverride> _overrides = new();

        /// Tra theo màu đất đã gói thành một số nguyên. Dựng ở lần hỏi đầu tiên rồi giữ
        /// lại — bảng màu mỗi màn chỉ vài chục dòng, nhưng lượt nạp màn nào cũng hỏi.
        private Dictionary<int, Color32> _lookup;

        public ColorAdjustment Tint => _tint;

        public bool HasOverrides => _overrides != null && _overrides.Count > 0;

        /// false khi màu này không có dòng ghi đè nào — bên gọi cứ đi tiếp như thường.
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
                // Dòng trùng thì dòng SAU thắng. Không cảnh báo: gõ trùng một màu rồi sửa
                // dòng dưới là chuyện bình thường lúc dò màu, và một Console đầy cảnh báo
                // sẽ dạy người ta bỏ qua cảnh báo.
                _lookup[Pack(entry.ground)] = entry.jewel;
            }
        }

        /// Bỏ ALPHA. Màu đất trong ảnh luôn đục, nhưng Color32 dựng tay trong Inspector
        /// rất dễ ra alpha 0 — và khi đó dòng ghi đè im lặng không bao giờ khớp.
        private static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        /// Sửa danh sách trong Inspector là bảng tra cũ hết đúng. Không có dòng này thì
        /// phải thoát Play Mode rồi vào lại mới thấy thay đổi, mà chẳng ai đoán ra vì sao.
        private void OnValidate() => _lookup = null;

        /// Chỉ dành cho cửa sổ chỉnh màu. Không gọi lúc chạy game.
        public void SetTint(ColorAdjustment tint) => _tint = tint;
#endif
    }
}
