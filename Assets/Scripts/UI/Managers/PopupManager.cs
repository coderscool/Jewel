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
    /// Minh hoạ 3 luật cùng lúc:
    /// 1. Build Dictionary từ List ở Awake (Dictionary không serialize được).
    /// 2. Tạo popup MỘT LẦN rồi bật/tắt — không Instantiate/Destroy mỗi lần mở.
    /// 3. Instantiate qua IObjectResolver để [Inject] trong prefab chạy được.
    public class PopupManager : MonoBehaviour, IPopupService
    {
        [SerializeField] private PopupConfig _config;
        [SerializeField] private Transform _root;

        [Tooltip("Tấm CHẶN CHẠM phủ kín màn hình, nằm sau popup. Tự bật khi có popup nào " +
                 "đang mở có tick Blocks Background.\n\n" +
                 "Image của nó phải để Raycast Target BẬT — đó là toàn bộ công dụng. Còn " +
                 "tối hay không là ALPHA của nó: để 0 thì nó chặn chạm mà không che gì, " +
                 "để 0.85 thì thành lớp tối kiểu cũ.\n\n" +
                 "Đặt nó làm con ĐẦU TIÊN của Root: popup sinh ra sau nên đứng sau nó " +
                 "trong danh sách con, và UI vẽ theo đúng thứ tự đó.\n\n" +
                 "Để trống thì bỏ qua — nhưng lúc đó chạm sẽ lọt xuống bức tranh phía sau " +
                 "mọi popup.")]
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

                // Object.Instantiate không chạy [Inject]; dùng resolver để con nhận được phụ thuộc.
                popup = _resolver.Instantiate(prefab, _root);
                _instances[key] = popup;

                // Trao ngay lúc tạo, một lần cho cả đời popup. Đây là chỗ DUY NHẤT sinh
                // ra popup, nên không có instance nào lọt lưới.
                popup.SetSoundService(_sound);
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

        /// Duyệt lại mỗi lần hỏi chứ không giữ bộ đếm — cùng lý do đã ghi ở LateUpdate:
        /// popup tự đóng bằng nút của chính nó, không đi qua manager, nên bộ đếm nào ở
        /// đây rồi cũng lệch.
        public bool IsAnyVisible()
        {
            foreach (var popup in _instances.Values)
            {
                if (popup.IsVisible) return true;
            }

            return false;
        }

        /// Đọc LẠI trạng thái thật mỗi frame thay vì đếm lượt bật/tắt.
        ///
        /// Popup tự đóng bằng nút đóng của chính nó — nó gọi thẳng PopupView.Hide chứ
        /// không đi qua manager. Một bộ đếm ở đây sẽ không bao giờ nghe được cú đóng đó,
        /// và tấm chặn kẹt lại trên màn hình — kẹt ở đây nghĩa là người chơi không tô
        /// được nữa mà không hiểu vì sao. Vài popup thì vòng lặp này không đáng gì, mà nó
        /// đúng trong mọi đường đóng — kể cả những đường thêm sau này.
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
