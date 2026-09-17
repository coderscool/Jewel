using System;
using System.Collections.Generic;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Views;
using UnityEngine;

namespace JewelPainter.UI.Data
{
    /// Bảng ánh xạ PopupKey sang prefab popup.
    [CreateAssetMenu(fileName = "PopupConfig", menuName = "JewelPainter/UI/Popup Config")]
    public class PopupConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public PopupKey key;
            public PopupView prefab;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
