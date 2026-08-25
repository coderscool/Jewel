using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup bộ sưu tập: bày ảnh của mọi màn chơi, màn chưa tới thì khoá lại.
    ///
    /// Popup được PopupManager tạo qua IObjectResolver nên [Inject] ở đây chạy được —
    /// Object.Instantiate thường thì không.
    ///
    /// Dựng lại danh sách ở mỗi lần Show chứ không ở Awake: qua được một màn là ổ khoá
    /// phải rơi ra, mà popup thì sống suốt phiên chơi.
    public class CollectionPopupView : PopupView
    {
        [SerializeField] private CollectionItemView _itemPrefab;

        [Tooltip("Object chứa các ô, thường gắn Grid Layout Group. Nằm trong Content " +
                 "của Scroll Rect nếu danh sách dài.")]
        [SerializeField] private Transform _itemRoot;

        [SerializeField] private Button _closeButton;

        [Header("Tiến độ sưu tập")]
        [Tooltip("Dòng chữ dạng '5/12'. Để trống thì bỏ qua.")]
        [SerializeField] private Text _progressText;

        [Tooltip("Ảnh làm thanh tiến trình. Image Type phải để FILLED, không thì fillAmount " +
                 "không có tác dụng gì và thanh lúc nào cũng đầy.")]
        [SerializeField] private Image _progressFill;

        private readonly List<CollectionItemView> _items = new();

        private ILevelService _levelService;

        [Inject]
        public void Construct(ILevelService levelService)
        {
            _levelService = levelService;
        }

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        public override void Show()
        {
            base.Show();

            Rebuild();
        }

        private void Rebuild()
        {
            if (_levelService == null || _itemPrefab == null)
            {
                Debug.LogWarning($"{nameof(CollectionPopupView)} thiếu Item Prefab hoặc chưa " +
                                 "được inject — popup sẽ trống.");
                return;
            }

            var levels = _levelService.Levels;
            var slot = 0;
            var unlocked = 0;

            foreach (var config in levels)
            {
                // Ô bỏ trống trong Inspector không được chiếm chỗ, không thì bộ sưu tập
                // thủng một lỗ ở giữa mà không ai hiểu vì sao.
                if (config == null) continue;

                var isUnlocked = _levelService.IsUnlocked(config.LevelId);
                if (isUnlocked) unlocked++;

                var item = GetItem(slot++);

                item.Bind(config.LevelId, config.TargetImage, isUnlocked);
                item.gameObject.SetActive(true);
            }

            HideFrom(slot);

            // Đếm theo số ô THẬT SỰ bày ra, không theo độ dài mảng Levels: ô null đã bị
            // bỏ qua ở trên, nên lấy Levels.Count sẽ ra mẫu số lớn hơn thứ người chơi thấy.
            SetProgress(unlocked, slot);
        }

        private void SetProgress(int unlocked, int total)
        {
            if (_progressText != null) _progressText.text = $"{unlocked}/{total}";

            if (_progressFill == null) return;

            // Chia cho 0 khi chưa có màn nào — trong Inspector rất dễ gặp lúc mảng Levels
            // còn trống, và NaN thì Image vẽ ra một thanh trống trơn không ai lần được vì sao.
            _progressFill.fillAmount = total > 0 ? (float)unlocked / total : 0f;
        }

        /// Tạo một lần rồi bật/tắt để tái dùng — không Instantiate/Destroy mỗi lần mở.
        private CollectionItemView GetItem(int slot)
        {
            while (_items.Count <= slot)
            {
                _items.Add(Instantiate(_itemPrefab, _itemRoot));
            }

            return _items[slot];
        }

        private void HideFrom(int slot)
        {
            for (var i = slot; i < _items.Count; i++) _items[i].gameObject.SetActive(false);
        }
    }
}
