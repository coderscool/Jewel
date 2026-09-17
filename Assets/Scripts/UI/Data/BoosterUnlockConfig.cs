using System;
using System.Collections.Generic;
using JewelPainter.UI.Definitions;
using UnityEngine;

namespace JewelPainter.UI.Data
{
    /// Mỗi booster mở khoá ở màn nào.
    [CreateAssetMenu(
        fileName = "BoosterUnlockConfig",
        menuName = "JewelPainter/UI/Booster Unlock Config")]
    public class BoosterUnlockConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Booster nào.")]
            public CreditPoolKind booster;

            [Tooltip("Mở khoá khi tiến trình người chơi đạt tới màn này.")]
            [Min(1)]
            public int unlockLevel;

            [Tooltip("Popup báo booster này vừa mở khoá.")]
            public PopupKey unlockPopup;
        }

        [Tooltip("Danh sách mốc mở khoá; booster không có trong danh sách thì mở sẵn.")]
        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<CreditPoolKind, Entry> _lookup;

        /// Màn mà booster này mở khoá.
        public int UnlockLevelFor(CreditPoolKind booster)
        {
            if (_lookup == null) BuildLookup();

            return _lookup.TryGetValue(booster, out var entry) ? entry.unlockLevel : 1;
        }

        /// Popup báo booster này mở khoá.
        public PopupKey UnlockPopupFor(CreditPoolKind booster)
        {
            if (_lookup == null) BuildLookup();

            return _lookup.TryGetValue(booster, out var entry) ? entry.unlockPopup : PopupKey.None;
        }

        /// Booster đã tới mốc mở khoá chưa.
        public bool IsUnlocked(CreditPoolKind booster, int playerLevel)
        {
            return playerLevel >= UnlockLevelFor(booster);
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<CreditPoolKind, Entry>(_entries.Count);

            foreach (var entry in _entries)
            {
                var clamped = entry;
                clamped.unlockLevel = Mathf.Max(1, entry.unlockLevel);

                _lookup[entry.booster] = clamped;
            }
        }

        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        /// Dựng lại bảng tra khi danh sách đổi.
        private void OnValidate() => _lookup = null;
#endif
    }
}
