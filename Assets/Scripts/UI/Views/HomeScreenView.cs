using System.Collections;
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

        [Tooltip("Ô của màn đang chơi dừng ở đâu trong khung nhìn.\n\n" +
                 "0 = sát MÉP TRÊN, 0.5 = giữa, 1 = sát MÉP DƯỚI.\n\n" +
                 "Tính theo cạnh của ô chứ không theo tâm, nên 0 và 1 vẫn thấy trọn ô " +
                 "chứ không bị cắt mất một nửa.")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusAlignment = 1f;

        [Tooltip("Phóng to ô đang ở vị trí tiêu điểm. Để trống thì mọi ô giữ nguyên cỡ. " +
                 "Nhớ đặt Focus Alignment của nó TRÙNG với ô ngay trên.")]
        [SerializeField] private ScrollFocusScaler _focusScaler;

        [Header("Nút")]
        [SerializeField] private Button _playButton;
        [SerializeField] private TMP_Text _playLevelText;
        [SerializeField] private Button _collectionButton;
        [SerializeField] private Button _settingsButton;

        private readonly List<HomeLevelItemView> _items = new();

        /// RectTransform của những ô ĐANG hiện, đưa cho ScrollFocusScaler. Giữ riêng
        /// thay vì để nó tự đi tìm: chỉ ở đây mới biết ô nào đang dùng, ô nào đang tắt
        /// chờ tái dùng.
        private readonly List<RectTransform> _activeItemRects = new();

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

        public void Hide()
        {
            // Huỷ luôn lần cuộn đang chờ tới frame sau: đóng Home rồi mà nó vẫn chạy thì
            // lần mở kế tiếp bắt đầu bằng một cú nhảy vị trí không ai gọi.
            StopAllCoroutines();

            SetVisible(false);
        }

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

            _activeItemRects.Clear();

            foreach (var config in _levelService.Levels)
            {
                if (config == null) continue;

                var levelId = config.LevelId;
                var isUnlocked = _levelService.IsUnlocked(levelId);
                var isCurrent = levelId == currentLevel;

                var item = GetItem(slot++);

                item.Bind(levelId, BuildThumbnail(config.GridData, levelId, isUnlocked, isCurrent), isUnlocked, isCurrent);
                item.gameObject.SetActive(true);

                var rect = (RectTransform)item.transform;
                _activeItemRects.Add(rect);

                if (isCurrent) _currentItemRect = rect;
            }

            HideFrom(slot);

            if (_playLevelText != null) _playLevelText.SetText("Level {0}", currentLevel);

            if (_focusScaler != null) _focusScaler.SetTargets(_activeItemRects);

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

        private void ScrollToCurrent()
        {
            if (_scrollRect == null || _currentItemRect == null) return;
            if (!isActiveAndEnabled) return;

            StopAllCoroutines();
            StartCoroutine(ScrollToCurrentRoutine());
        }

        /// Cuộn sao cho ô của màn đang chơi nằm giữa khung nhìn.
        ///
        /// Phải đợi HẾT MỘT FRAME. Layout Group và Content Size Fitter tính lại kích
        /// thước ở cuối frame, và ngay sau đó ScrollRect tự kẹp lại vị trí cuộn theo
        /// kích thước mới. Đặt vị trí trong cùng frame với lúc dựng danh sách là đặt
        /// xong bị ghi đè — đó là lý do bản trước không nhúc nhích.
        private IEnumerator ScrollToCurrentRoutine()
        {
            yield return null;

            if (_scrollRect == null || _currentItemRect == null) yield break;

            Canvas.ForceUpdateCanvases();

            var content = _scrollRect.content;
            if (content == null) yield break;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var viewportHeight = viewport.rect.height;
            var scrollable = content.rect.height - viewportHeight;

            if (scrollable <= 0f)
            {
                // Danh sách ngắn hơn khung nhìn: không có gì để cuộn.
                _scrollRect.verticalNormalizedPosition = 1f;
                yield break;
            }

            // Đo khoảng cách từ ĐỈNH content xuống tâm ô, trong hệ toạ độ của chính
            // content. Cách này không quan tâm padding, spacing, pivot, anchor hay thứ tự
            // đảo ngược — nó đọc chỗ ô đang thật sự đứng, còn mấy thứ kia chỉ là nguyên
            // nhân đưa nó tới đó.
            var itemLocalY = content.InverseTransformPoint(_currentItemRect.position).y;
            var distanceFromTop = content.rect.yMax - itemLocalY;

            // Đưa ô về đúng chỗ đã đặt trong khung nhìn.
            //
            // Số hạng giữa là phần tính theo CẠNH ô thay vì tâm ô: căn sát mép dưới mà
            // chỉ đưa tâm ô tới đó thì nửa dưới của ô nằm ngoài màn. Cộng thêm nửa chiều
            // cao ô đúng bằng lượng cần để cạnh dưới của nó chạm mép dưới khung nhìn.
            //   alignment 0   → cạnh TRÊN ô chạm mép trên
            //   alignment 0.5 → tâm ô ở giữa (số hạng giữa triệt tiêu)
            //   alignment 1   → cạnh DƯỚI ô chạm mép dưới
            var alignment = Mathf.Clamp01(_focusAlignment);
            var itemHeight = _currentItemRect.rect.height;

            var desired = distanceFromTop
                          + itemHeight * (alignment - 0.5f)
                          - viewportHeight * alignment;

            var offsetFromTop = Mathf.Clamp(desired, 0f, scrollable);

            // Ghi qua verticalNormalizedPosition chứ không ghi thẳng anchoredPosition:
            // anchoredPosition đo từ điểm neo, mà Content của ScrollRect mặc định neo ở
            // MÉP TRÊN viewport chứ không phải tâm. Bản trước gán thẳng vào đó nên lệch
            // đúng nửa chiều cao khung nhìn — và lệch như nhau ở mọi giá trị padding.
            _scrollRect.verticalNormalizedPosition = 1f - offsetFromTop / scrollable;
        }

        private void HandlePlayClicked()
        {
            Hide();

            _levelService.LoadLevel(_levelService.CurrentLevel);
        }

        private void HandleCollectionClicked() => _popupService.Show(PopupKey.Collection);

        private void HandleSettingsClicked() => _popupService.Show(PopupKey.SettingsHome);

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
