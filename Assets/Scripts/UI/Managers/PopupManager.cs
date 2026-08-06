using System.Collections.Generic;
using JewelPainter.UI.Data;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace JewelPainter.UI.Managers
{
    /// Minh hoạ 3 luật cùng lúc:
    /// 1. Build Dictionary từ List ở Awake (Dictionary không serialize được).
    /// 2. Tạo popup MỘT LẦN rồi bật/tắt — không Instantiate/Destroy mỗi lần mở.
    /// 3. Instantiate qua IObjectResolver để [Inject] trong prefab chạy được.
    public class PopupManager : MonoBehaviour, IPopupService
    {
        [SerializeField] private PopupConfig _config;
        [SerializeField] private Transform _root;

        private readonly Dictionary<PopupKey, PopupView> _prefabs = new();
        private readonly Dictionary<PopupKey, PopupView> _instances = new();

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Awake()
        {
            foreach (var entry in _config.Entries)
            {
                if (entry.prefab == null) continue;
                _prefabs[entry.key] = entry.prefab;
            }
        }

        public PopupView Show(PopupKey key)
        {
            if (!_instances.TryGetValue(key, out var popup))
            {
                if (!_prefabs.TryGetValue(key, out var prefab))
                {
                    Debug.LogError($"Không tìm thấy prefab cho popup {key} trong {nameof(PopupConfig)}");
                    return null;
                }

                // Object.Instantiate không chạy [Inject]; dùng resolver để con nhận được phụ thuộc.
                popup = _resolver.Instantiate(prefab, _root);
                _instances[key] = popup;
            }

            popup.Show();
            return popup;
        }

        public void Hide(PopupKey key)
        {
            if (!_instances.TryGetValue(key, out var popup)) return;

            popup.Hide();   // SetActive(false) — KHÔNG Destroy, giữ lại để tái dùng
        }

        public void HideAll()
        {
            foreach (var popup in _instances.Values)
            {
                if (popup.IsVisible) popup.Hide();
            }
        }
    }
}
