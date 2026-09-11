using System;
using System.Collections.Generic;
using JewelPainter.UI.Definitions;
using UnityEngine;

namespace JewelPainter.UI.Data
{
    /// Mỗi booster mở khoá ở màn nào.
    ///
    /// Một asset cho cả game: mốc mở khoá là nhịp DẠY NGƯỜI CHƠI, không phải thuộc tính
    /// của từng màn. Người chơi cần vài màn đầu chỉ để hiểu luật tô, rồi mới đón thêm
    /// một nút mới. Nhét mốc đó vào LevelConfig là rải một quyết định sang bốn mươi file
    /// và không ai còn nhìn thấy cái nhịp nữa.
    ///
    /// Nằm ở UI vì nó nói về cái NÚT chứ không về luật chơi: booster đã mở khoá hay chưa
    /// không đổi một dòng nào trong cách tô, cách tính điểm hay cách lưu. Và CreditPoolKind
    /// vốn ở UI — kéo nó xuống Gameplay chỉ để chứa bảng này là làm nặng chiều phụ thuộc
    /// mà không đổi được gì.
    ///
    /// Vì thế đây là khoá MỀM: nó tắt cái nút, không chặn service. Cheat gọi thẳng vào
    /// IFreePaintService vẫn chạy — đúng ý, vì cheat tồn tại để bỏ qua mấy thứ này.
    [CreateAssetMenu(
        fileName = "BoosterUnlockConfig",
        menuName = "JewelPainter/UI/Booster Unlock Config")]
    public class BoosterUnlockConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Booster nào. Trùng tên thì dòng SAU thắng.")]
            public CreditPoolKind booster;

            [Tooltip("Mở khoá khi tiến trình người chơi đạt tới màn này. 1 là mở sẵn từ " +
                     "đầu.")]
            [Min(1)]
            public int unlockLevel;

            [Tooltip("Popup báo booster này vừa mở khoá. Để None thì mở khoá im lặng — " +
                     "nút chỉ đơn giản hết ổ khoá.\n\n" +
                     "Nằm ở đây chứ không nằm trong code: thêm booster thứ tư sau này chỉ " +
                     "phải thêm một dòng vào bảng, không phải mở presenter ra sửa.")]
            public PopupKey unlockPopup;
        }

        [Tooltip("Booster KHÔNG có tên trong danh sách này thì mở sẵn từ màn 1.\n\n" +
                 "Mặc định như vậy chứ không phải khoá sẵn, vì đây là hướng hỏng an toàn " +
                 "hơn: quên điền một dòng thì người chơi được dùng sớm một booster — khó " +
                 "chịu nhưng chơi được. Còn khoá sẵn thì một booster biến mất khỏi game " +
                 "mà không ai báo gì, và phải tới lúc có người hỏi mới biết.")]
        [SerializeField] private List<Entry> _entries = new();

        /// Dựng ở lần hỏi đầu tiên rồi giữ lại. Dictionary không serialize được nên bảng
        /// gốc phải là List — cùng khuôn đã dùng ở PopupConfig và SoundConfig.
        private Dictionary<CreditPoolKind, Entry> _lookup;

        /// Màn mà booster này mở khoá. Không có trong bảng thì trả 1 — mở sẵn.
        public int UnlockLevelFor(CreditPoolKind booster)
        {
            if (_lookup == null) BuildLookup();

            return _lookup.TryGetValue(booster, out var entry) ? entry.unlockLevel : 1;
        }

        /// Popup báo booster này mở khoá. PopupKey.None nghĩa là mở khoá im lặng.
        public PopupKey UnlockPopupFor(CreditPoolKind booster)
        {
            if (_lookup == null) BuildLookup();

            return _lookup.TryGetValue(booster, out var entry) ? entry.unlockPopup : PopupKey.None;
        }

        /// Tiến trình đã tới mốc chưa.
        ///
        /// So với TIẾN TRÌNH CAO NHẤT chứ không phải màn đang chơi — bên gọi truyền vào
        /// PlayerProgress.Level. Mở khoá là vĩnh viễn: chơi lại màn 1 sau khi đã tới màn
        /// 20 thì booster vẫn còn đó. Lấy lại một thứ đã cho là cách nhanh nhất để người
        /// chơi tin rằng game hỏng.
        public bool IsUnlocked(CreditPoolKind booster, int playerLevel)
        {
            return playerLevel >= UnlockLevelFor(booster);
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<CreditPoolKind, Entry>(_entries.Count);

            foreach (var entry in _entries)
            {
                // Dòng trùng thì dòng SAU thắng, không cảnh báo. Cùng lý do đã ghi ở
                // JewelTintConfig: gõ trùng rồi sửa dòng dưới là chuyện bình thường lúc
                // cân nhịp, và một Console đầy cảnh báo sẽ dạy người ta bỏ qua cảnh báo.
                var clamped = entry;
                clamped.unlockLevel = Mathf.Max(1, entry.unlockLevel);

                _lookup[entry.booster] = clamped;
            }
        }

        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        /// Sửa danh sách trong Inspector là bảng tra cũ hết đúng. Không có dòng này thì
        /// phải thoát Play Mode rồi vào lại mới thấy thay đổi, mà chẳng ai đoán ra vì sao.
        private void OnValidate() => _lookup = null;
#endif
    }
}
