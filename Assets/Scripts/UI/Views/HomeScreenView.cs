using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

        [Header("Ăn mừng sau khi thắng màn")]
        [Tooltip("Hiệu ứng đưa bức tranh vừa hoàn thành bay vào icon bộ sưu tập. " +
                 "Để trống thì bỏ qua phần ăn mừng, Home mở ra như bình thường.")]
        [SerializeField] private CollectionFlyEffect _collectionFly;

        [Tooltip("Thời gian cuộn từ ô vừa xong sang ô của màn kế tiếp, sau khi tranh đã " +
                 "bay đi. Cuộn có thời gian chứ không nhảy cóc, để người chơi thấy mình " +
                 "đang đi tiếp trong danh sách.")]
        [SerializeField] private float _celebrateScrollSeconds = 0.6f;

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

        /// Màn vừa hoàn thành, chờ được ăn mừng ở lần dựng danh sách kế tiếp. -1 là không có.
        ///
        /// Phải nhớ riêng chứ không suy ra từ CurrentLevel: tiến trình đã nhích ngay lúc
        /// tô xong, nên "màn hiện tại" là màn KẾ TIẾP chứ không phải màn vừa xong.
        private int _pendingCelebrationLevel = -1;

        private HomeLevelItemView _celebrateItem;

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

        /// Mở Home kèm màn ăn mừng: dừng ở ô của màn vừa xong, cho bức tranh bay vào bộ
        /// sưu tập, rồi mới cuộn sang màn kế tiếp.
        ///
        /// Nhận levelId thay vì tự đọc: bên gọi phải chụp lại con số TRƯỚC khi đẩy tiến
        /// trình, vì sau đó không còn cách nào biết màn nào vừa xong.
        public void ShowCelebrating(int clearedLevelId)
        {
            _pendingCelebrationLevel = clearedLevelId;
            Show();
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
            _celebrateItem = null;

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
                if (levelId == _pendingCelebrationLevel) _celebrateItem = item;
            }

            HideFrom(slot);

            if (_playLevelText != null) _playLevelText.SetText("Level {0}", currentLevel);

            if (_focusScaler != null) _focusScaler.SetTargets(_activeItemRects);

            BeginOpeningFlow();
        }

        /// Có màn cần ăn mừng thì chạy màn ăn mừng, không thì mở như mọi lần.
        ///
        /// Tiêu luôn _pendingCelebrationLevel ở đây, kể cả khi không chạy được: để sót là
        /// lần mở Home sau lại ăn mừng một màn đã cũ.
        private void BeginOpeningFlow()
        {
            var celebrateItem = _celebrateItem;
            _pendingCelebrationLevel = -1;

            var canCelebrate = celebrateItem != null
                               && _collectionFly != null
                               && _collectionButton != null
                               && _scrollRect != null
                               && celebrateItem.ThumbnailRect != null
                               && celebrateItem.ThumbnailSprite != null
                               && isActiveAndEnabled;

            if (!canCelebrate)
            {
                ScrollToCurrent();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(CelebrateRoutine(celebrateItem));
        }

        /// Ba nhịp: đưa ô vừa xong vào khung nhìn, cho tranh bay đi, rồi cuộn sang màn mới.
        private IEnumerator CelebrateRoutine(HomeLevelItemView item)
        {
            // Đợi hết một frame vì cùng lý do đã ghi ở ScrollToCurrentRoutine: Layout Group
            // và Content Size Fitter tính lại kích thước ở cuối frame.
            yield return null;

            Canvas.ForceUpdateCanvases();

            var itemRect = (RectTransform)item.transform;
            if (TryGetScrollPosition(itemRect, out var startPosition))
            {
                _scrollRect.verticalNormalizedPosition = startPosition;
            }

            // Đợi thêm một frame để vị trí cuộn vừa đặt được áp vào toạ độ thật. Không có
            // nhịp này thì hiệu ứng đo chỗ ô ở lần cuộn TRƯỚC, và tranh bay ra từ chỗ khác.
            yield return null;

            Canvas.ForceUpdateCanvases();

            var finished = false;

            _collectionFly.Play(
                item.ThumbnailRect,
                item.ThumbnailSprite,
                (RectTransform)_collectionButton.transform,
                () => finished = true);

            while (!finished) yield return null;

            yield return ScrollToCurrentRoutine(_celebrateScrollSeconds);
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
            StartCoroutine(ScrollToCurrentRoutine(0f));
        }

        /// Cuộn sao cho ô của màn đang chơi nằm giữa khung nhìn.
        ///
        /// Phải đợi HẾT MỘT FRAME. Layout Group và Content Size Fitter tính lại kích
        /// thước ở cuối frame, và ngay sau đó ScrollRect tự kẹp lại vị trí cuộn theo
        /// kích thước mới. Đặt vị trí trong cùng frame với lúc dựng danh sách là đặt
        /// xong bị ghi đè — đó là lý do bản trước không nhúc nhích.
        /// duration 0 là nhảy thẳng tới nơi; lớn hơn 0 thì cuộn có thời gian.
        private IEnumerator ScrollToCurrentRoutine(float duration)
        {
            yield return null;

            if (_scrollRect == null || _currentItemRect == null) yield break;

            Canvas.ForceUpdateCanvases();

            if (!TryGetScrollPosition(_currentItemRect, out var target)) yield break;

            if (duration <= 0f)
            {
                _scrollRect.verticalNormalizedPosition = target;
                yield break;
            }

            var from = _scrollRect.verticalNormalizedPosition;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = DOVirtual.EasedValue(
                    0f, 1f, Mathf.Clamp01(elapsed / duration), Ease.InOutCubic);

                _scrollRect.verticalNormalizedPosition = Mathf.Lerp(from, target, t);
                yield return null;
            }

            _scrollRect.verticalNormalizedPosition = target;
        }

        /// Vị trí cuộn (0..1) để đưa `target` về đúng chỗ đã đặt trong khung nhìn.
        ///
        /// false khi chưa đủ dữ kiện để tính. Gọi hàm này SAU Canvas.ForceUpdateCanvases,
        /// không thì nó đọc kích thước của lần bố cục trước.
        private bool TryGetScrollPosition(RectTransform target, out float normalizedPosition)
        {
            normalizedPosition = 1f;

            var content = _scrollRect.content;
            if (content == null || target == null) return false;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var viewportHeight = viewport.rect.height;
            var scrollable = content.rect.height - viewportHeight;

            // Danh sách ngắn hơn khung nhìn: không có gì để cuộn.
            if (scrollable <= 0f) return true;

            // Đo khoảng cách từ ĐỈNH content xuống tâm ô, trong hệ toạ độ của chính
            // content. Cách này không quan tâm padding, spacing, pivot, anchor hay thứ tự
            // đảo ngược — nó đọc chỗ ô đang thật sự đứng, còn mấy thứ kia chỉ là nguyên
            // nhân đưa nó tới đó.
            var itemLocalY = content.InverseTransformPoint(target.position).y;
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
            var itemHeight = target.rect.height;

            var desired = distanceFromTop
                          + itemHeight * (alignment - 0.5f)
                          - viewportHeight * alignment;

            var offsetFromTop = Mathf.Clamp(desired, 0f, scrollable);

            // Trả về theo verticalNormalizedPosition chứ không theo anchoredPosition:
            // anchoredPosition đo từ điểm neo, mà Content của ScrollRect mặc định neo ở
            // MÉP TRÊN viewport chứ không phải tâm. Bản trước gán thẳng vào đó nên lệch
            // đúng nửa chiều cao khung nhìn — và lệch như nhau ở mọi giá trị padding.
            normalizedPosition = 1f - offsetFromTop / scrollable;
            return true;
        }

        /// Chạy lại màn ăn mừng với màn ngay TRƯỚC màn hiện tại, để chỉnh nhịp hiệu ứng
        /// mà không phải tô hết một màn mỗi lần.
        ///
        /// Dùng màn trước chứ không dùng màn hiện tại: đúng bằng thứ người chơi thấy thật
        /// sau khi thắng, vì lúc đó tiến trình đã nhảy sang màn kế rồi. Ảnh của màn trước
        /// cũng được vẽ tô kín, còn màn hiện tại thì vẽ theo tiến độ dở dang.
        ///
        /// Gọi từ CelebrationCheat bằng phím tắt, hoặc chuột phải lên tiêu đề component
        /// này trong lúc Play.
        [ContextMenu("Chạy thử hiệu ứng ăn mừng")]
        public void ReplayCelebration()
        {
            if (!isActiveAndEnabled || _levelService == null)
            {
                Debug.LogWarning("Home đang đóng nên chưa chạy thử được. Mở Home rồi bấm lại.", this);
                return;
            }

            var previous = PreviousUnlockedLevel();

            if (previous < 0)
            {
                Debug.LogWarning("Chưa có màn nào hoàn thành trước màn hiện tại, không có " +
                                 "bức tranh nào để bay. Qua một màn rồi thử lại.", this);
                return;
            }

            ShowCelebrating(previous);
        }

        /// Màn đã mở khoá đứng ngay trước màn hiện tại trong danh sách. -1 nếu không có.
        ///
        /// Đi theo THỨ TỰ của Levels chứ không lấy CurrentLevel trừ một: id không bắt buộc
        /// liên tiếp, và mảng Levels mới là thứ quyết định ô nào đứng cạnh ô nào.
        private int PreviousUnlockedLevel()
        {
            var previous = -1;

            foreach (var config in _levelService.Levels)
            {
                if (config == null) continue;
                if (config.LevelId == _levelService.CurrentLevel) return previous;

                if (_levelService.IsUnlocked(config.LevelId)) previous = config.LevelId;
            }

            return previous;
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
