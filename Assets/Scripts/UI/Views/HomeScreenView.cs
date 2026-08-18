using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Components;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Màn hình đầu game: danh sách mọi màn chơi và nút vào chơi.
    ///
    /// Mỗi ô hiện đúng trạng thái của màn đó:
    ///   - đã xong  → tranh hoàn thiện
    ///   - đang chơi → ảnh tiến độ ngay lúc này, giống hệt thứ đang thấy trong game
    ///   - chưa mở  → ô xám
    ///
    /// Home nằm cùng scene với bàn chơi và chỉ bật tắt. Không tách scene riêng vì
    /// LifetimeScope, tiến trình và mọi service đang sống ở đây — tách ra là phải dựng
    /// thêm một scope cha để chúng sống xuyên scene, đổi lấy một thứ chưa cần tới.
    public class HomeScreenView : MonoBehaviour
    {
        [Tooltip("Object bị ẩn khi vào chơi. Để trống thì ẩn chính object này.")]
        [SerializeField] private GameObject _content;

        [Header("Danh sách màn")]
        [SerializeField] private HomeLevelItemView _itemPrefab;

        [Tooltip("Object chứa các ô, thường gắn Vertical Layout Group. Nằm trong Content " +
                 "của Scroll Rect.")]
        [SerializeField] private Transform _itemRoot;

        [Tooltip("Scroll Rect của danh sách. Có gán thì lúc mở Home nó tự cuộn tới màn " +
                 "đang chơi.")]
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Nút")]
        [SerializeField] private Button _playButton;
        [SerializeField] private TMP_Text _playLevelText;
        [SerializeField] private Button _collectionButton;
        [SerializeField] private Button _settingsButton;

        private readonly List<HomeLevelItemView> _items = new();

        /// Ảnh tự dựng lúc chạy, KHÔNG phải asset. Unity không dọn giúp — mỗi lần mở
        /// Home mà không huỷ bản cũ là bộ nhớ lớn thêm một nấc, không bao giờ trả lại.
        private readonly List<Sprite> _thumbnails = new();

        private ILevelService _levelService;
        private IPopupService _popupService;
        private PaintProgressStore _progressStore;
        private RectTransform _currentItemRect;

        public void Init(
            ILevelService levelService,
            IPopupService popupService,
            PaintProgressStore progressStore)
        {
            _levelService = levelService;
            _popupService = popupService;
            _progressStore = progressStore;

            if (_playButton != null) _playButton.onClick.AddListener(HandlePlayClicked);
            if (_collectionButton != null) _collectionButton.onClick.AddListener(HandleCollectionClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettingsClicked);

            // KHÔNG tự mở. Vào game là màn hình chờ chạy rồi vào thẳng màn đang chơi dở;
            // Home chỉ mở khi người chơi bấm nút Home trên HUD.
            Hide();
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(HandlePlayClicked);
            if (_collectionButton != null) _collectionButton.onClick.RemoveListener(HandleCollectionClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettingsClicked);

            ReleaseThumbnails();
        }

        public void Show()
        {
            SetVisible(true);
            Rebuild();
        }

        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private void Rebuild()
        {
            ReleaseThumbnails();

            _currentItemRect = null;

            if (_itemPrefab == null || _levelService == null) return;

            var currentLevel = _levelService.CurrentLevel;
            var slot = 0;

            foreach (var config in _levelService.Levels)
            {
                if (config == null) continue;

                var levelId = config.LevelId;
                var isUnlocked = _levelService.IsUnlocked(levelId);
                var isCurrent = levelId == currentLevel;

                var item = GetItem(slot++);

                item.Bind(levelId, BuildThumbnail(config.GridData, levelId, isUnlocked, isCurrent), isUnlocked, isCurrent);
                item.gameObject.SetActive(true);

                if (isCurrent) _currentItemRect = (RectTransform)item.transform;
            }

            HideFrom(slot);

            if (_playLevelText != null) _playLevelText.SetText("Level {0}", currentLevel);

            ScrollToCurrent();
        }

        /// Màn đã xong thì vẽ tô kín mà không cần bản lưu: bản lưu của nó bị xoá ngay
        /// lúc nó xong, và "đã xong" thì theo định nghĩa là mọi ô đều đã tô.
        private Sprite BuildThumbnail(
            Gameplay.Data.LevelGridData gridData, int levelId, bool isUnlocked, bool isCurrent)
        {
            if (!isUnlocked || gridData == null) return null;

            var paintAll = !isCurrent;
            var bits = isCurrent && _progressStore != null ? _progressStore.LoadBits(levelId) : null;

            var sprite = LevelThumbnailBuilder.Build(gridData, bits, paintAll, levelId);
            if (sprite != null) _thumbnails.Add(sprite);

            return sprite;
        }

        /// Cuộn sao cho ô của màn đang chơi nằm giữa khung nhìn.
        ///
        /// Phải đợi hết frame: Layout Group tính lại vị trí các ô ở cuối frame, hỏi toạ
        /// độ ngay bây giờ là đọc vị trí của lần dựng trước.
        private void ScrollToCurrent()
        {
            if (_scrollRect == null || _currentItemRect == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_itemRoot);

            var content = _scrollRect.content;
            if (content == null) return;

            var contentHeight = content.rect.height;
            var viewportHeight = _scrollRect.viewport != null
                ? _scrollRect.viewport.rect.height
                : contentHeight;

            var scrollable = contentHeight - viewportHeight;
            if (scrollable <= 0f)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            // anchoredPosition.y của ô là số ÂM tính từ đỉnh content, nên đảo dấu để ra
            // khoảng cách từ đỉnh xuống.
            var offsetFromTop = -_currentItemRect.anchoredPosition.y - viewportHeight * 0.5f;
            var normalized = 1f - Mathf.Clamp01(offsetFromTop / scrollable);

            _scrollRect.verticalNormalizedPosition = normalized;
        }

        private void HandlePlayClicked()
        {
            Hide();

            _levelService.LoadLevel(_levelService.CurrentLevel);
        }

        private void HandleCollectionClicked() => _popupService.Show(PopupKey.Collection);

        private void HandleSettingsClicked() => _popupService.Show(PopupKey.Settings);

        /// Tạo một lần rồi bật tắt để tái dùng. Ô mới sinh ra ở trạng thái TẮT vì prefab
        /// vốn đang bật — ô nào tạo ra mà chưa kịp Bind sẽ hiện nguyên nội dung prefab.
        private HomeLevelItemView GetItem(int slot)
        {
            while (_items.Count <= slot)
            {
                var created = Instantiate(_itemPrefab, _itemRoot);
                created.gameObject.SetActive(false);

                _items.Add(created);
            }

            return _items[slot];
        }

        private void HideFrom(int slot)
        {
            for (var i = slot; i < _items.Count; i++) _items[i].gameObject.SetActive(false);
        }

        /// Huỷ cả Sprite lẫn Texture. Destroy(sprite) một mình để lại texture mồ côi —
        /// Sprite.Create không sở hữu texture nó trỏ tới.
        private void ReleaseThumbnails()
        {
            foreach (var sprite in _thumbnails)
            {
                if (sprite == null) continue;

                if (sprite.texture != null) Destroy(sprite.texture);

                Destroy(sprite);
            }

            _thumbnails.Clear();
        }
    }
}
