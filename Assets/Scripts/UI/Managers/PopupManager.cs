using System.Collections.Generic;
using JewelPainter.Core.Services;
using JewelPainter.UI.Data;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using JewelPainter.UI.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace JewelPainter.UI.Managers
{
    /// Mở, đóng và tái dùng các popup.
    public class PopupManager : MonoBehaviour, IPopupService
    {
        [SerializeField] private PopupConfig _config;
        [SerializeField] private Transform _root;

        [Tooltip("Tấm chặn chạm phủ kín màn hình nằm sau popup.")]
        [SerializeField] private GameObject _backdrop;

        private readonly Dictionary<PopupKey, PopupView> _prefabs = new();
        private readonly Dictionary<PopupKey, PopupView> _instances = new();

        private IObjectResolver _resolver;
        private ISoundService _sound;

        [Inject]
        public void Construct(IObjectResolver resolver, ISoundService sound)
        {
            _resolver = resolver;
            _sound = sound;
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

                popup = _resolver.Instantiate(prefab, _root);
                _instances[key] = popup;

                popup.SetSoundService(_sound);
            }

            popup.Show();
            return popup;
        }

        public void Hide(PopupKey key)
        {
            if (!_instances.TryGetValue(key, out var popup)) return;

            popup.Hide();
        }

        public void HideAll()
        {
            foreach (var popup in _instances.Values)
            {
                if (popup.IsVisible) popup.Hide();
            }
        }

        /// Có popup nào đang mở không.
        public bool IsAnyVisible()
        {
            foreach (var popup in _instances.Values)
            {
                if (popup.IsVisible) return true;
            }

            return false;
        }

        /// Bật tắt tấm chặn chạm theo trạng thái popup.
        private void LateUpdate()
        {
            if (_backdrop == null) return;

            var needed = false;

            foreach (var popup in _instances.Values)
            {
                if (!popup.IsVisible || !popup.BlocksBackground) continue;

                needed = true;
                break;
            }

            if (_backdrop.activeSelf != needed) _backdrop.SetActive(needed);
        }
    }
}
