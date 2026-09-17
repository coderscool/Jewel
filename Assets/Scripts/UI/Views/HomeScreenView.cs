using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Components;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Màn hình đầu game: danh sách mọi màn chơi và nút vào chơi.
    public class HomeScreenView : MonoBehaviour
    {
        [Tooltip("Object bị ẩn khi vào chơi.")]
        [SerializeField] private GameObject _content;

        [Header("Danh sách màn")]
        [SerializeField] private HomeLevelItemView _itemPrefab;

        [Tooltip("Object chứa các ô, thường gắn Vertical Layout Group.")]
        [SerializeField] private Transform _itemRoot;

        [Tooltip("Scroll Rect của danh sách.")]
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Vị trí dừng của ô màn đang chơi trong khung nhìn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusAlignment = 1f;

        [Tooltip("Khoảng cách thêm giữa ô và mép khung nhìn, tính bằng pixel.")]
        [SerializeField] private float _focusPadding;

        [Tooltip("Phóng to ô đang ở vị trí tiêu điểm.")]
        [SerializeField] private ScrollFocusScaler _focusScaler;

        [Tooltip("Bật phóng to ô ở vị trí tiêu điểm.")]
        [SerializeField] private bool _useFocusScaler;

        [Header("Dây nối giữa các ô")]
        [Tooltip("Prefab một đoạn dây nối giữa hai ô.")]
        [SerializeField] private Image _linePrefab;

        [Tooltip("Cha của các đoạn dây.")]
        [SerializeField] private RectTransform _lineRoot;

        [Tooltip("Bề dày dây, tính bằng pixel.")]
        [SerializeField] private float _lineWidth = 14f;

        [Tooltip("Độ rút ngắn mỗi đầu dây, tính bằng pixel.")]
        [SerializeField] private float _lineInset = 24f;

        [Header("Ăn mừng sau khi thắng màn")]
        [Tooltip("Hiệu ứng đưa bức tranh vừa hoàn thành bay vào icon bộ sưu tập.")]
        [SerializeField] private CollectionFlyEffect _collectionFly;

        [Tooltip("Thời gian cuộn sang ô của màn kế tiếp sau khi tranh bay đi.")]
        [SerializeField] private float _celebrateScrollSeconds = 0.6f;

        [Header("Vào màn hình")]
        [Tooltip("CanvasGroup dùng để mờ dần khi Home hiện ra.")]
        [SerializeField] private CanvasGroup _fadeGroup;

        [Tooltip("Chờ ngần này giây rồi mới bắt đầu mờ dần vào.")]
        [SerializeField] private float _enterDelay = 0.25f;

        [Tooltip("Thời gian mờ dần vào.")]
        [SerializeField] private float _enterDuration = 0.25f;

        [Tooltip("Màn che chuyển cảnh dùng shader.")]
        [SerializeField] private ScreenTransition _screenTransition;

        [Tooltip("Thời gian chờ sau khi màn che quét ra rồi mới thả tranh bay.")]
        [SerializeField] private float _celebrateDelayAfterTransition = 0.2f;

        [Tooltip("Điểm xuất phát của bức tranh bay vào bộ sưu tập.")]
        [SerializeField] private RectTransform _celebrateFromRect;

        [Header("Tiền")]
        [Tooltip("Số tiền người chơi đang có.")]
        [SerializeField] private Text _coinsText;

        [Header("Nút")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Text _playLevelText;

        [Tooltip("Phần trăm đã tô của màn đang chọn.")]
        [SerializeField] private Text _percentText;
        [SerializeField] private Button _collectionButton;
        [SerializeField] private Button _settingsButton;

        private readonly List<HomeLevelItemView> _items = new();

        private readonly List<RectTransform> _activeItemRects = new();

        private readonly List<RectTransform> _lines = new();

        private readonly List<Sprite> _thumbnails = new();

        private ILevelService _levelService;
        private IPopupService _popupService;
        private PaintProgressStore _progressStore;
        private PlayerWallet _wallet;

        private BoardView _boardView;
        private ISoundService _sound;

        public bool IsVisible { get; private set; }
        private RectTransform _currentItemRect;

        private int _displayedCoins = -1;

        private Tween _enterTween;
        private bool _isEntering;


        public float EnterDelaySeconds =>
            _screenTransition == null && _fadeGroup != null ? Mathf.Max(0f, _enterDelay) : 0f;

        public ScreenTransition Transition => _screenTransition;

        private int _pendingCelebrationLevel = -1;

        private HomeLevelItemView _celebrateItem;

        private int _selectedLevel = -1;

        public void Init(
            ILevelService levelService,
            IPopupService popupService,
            PaintProgressStore progressStore,
            PlayerWallet wallet,
            BoardView boardView,
            ISoundService sound)
        {
            _levelService = levelService;
            _popupService = popupService;
            _progressStore = progressStore;
            _wallet = wallet;
            _boardView = boardView;
            _sound = sound;

            if (_wallet != null)
            {
                _wallet.OnCoinsChanged += SetCoins;
                SetCoins(_wallet.Coins);
            }

            if (_playButton != null) _playButton.onClick.AddListener(HandlePlayClicked);
            if (_collectionButton != null) _collectionButton.onClick.AddListener(HandleCollectionClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettingsClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(HandlePlayClicked);
            if (_collectionButton != null) _collectionButton.onClick.RemoveListener(HandleCollectionClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettingsClicked);

            if (_wallet != null) _wallet.OnCoinsChanged -= SetCoins;

            ReleaseThumbnails();
        }

        public void Show()
        {
            SetVisible(true);
            Rebuild();
        }

        /// Mở Home kèm màn ăn mừng màn vừa xong.
        public void ShowCelebrating(int clearedLevelId)
        {
            _pendingCelebrationLevel = clearedLevelId;
            Show();
        }

        public void Hide()
        {
            StopAllCoroutines();

            SetVisible(false);
        }

        public event System.Action<bool> OnVisibilityChanged;

        private void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);

            if (_boardView != null) _boardView.SetCovered(visible);

            if (visible) BeginEnter();
            else KillEnter();

            if (IsVisible == visible) return;

            IsVisible = visible;
            OnVisibilityChanged?.Invoke(visible);
        }

        /// Mở đầu bằng alpha 0, chờ hết Enter Delay rồi mờ dần lên 1.
        private void BeginEnter()
        {
            KillEnter();

            if (_fadeGroup == null) return;

            if (_screenTransition != null)
            {
                EndEnter();
                return;
            }

            var delay = Mathf.Max(0f, _enterDelay);
            var duration = Mathf.Max(0f, _enterDuration);

            if (delay <= 0f && duration <= 0f) return;

            _isEntering = true;

            _fadeGroup.alpha = 0f;

            _fadeGroup.blocksRaycasts = false;

            _enterTween = DOVirtual.Float(0f, 1f, duration, value =>
                {
                    if (_fadeGroup != null) _fadeGroup.alpha = value;
                })
                .SetDelay(delay)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(EndEnter);
        }

        /// Trả CanvasGroup về trạng thái hiện đủ.
        private void EndEnter()
        {
            _enterTween = null;
            _isEntering = false;

            if (_fadeGroup == null) return;

            _fadeGroup.alpha = 1f;
            _fadeGroup.blocksRaycasts = true;
        }

        private void KillEnter()
        {
            if (_enterTween != null && _enterTween.IsActive()) _enterTween.Kill();

            EndEnter();
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

                var isCompleted = _levelService.IsCompleted(levelId);

                var item = GetItem(slot++);

                item.Bind(
                    levelId,
                    BuildThumbnail(config.GridData, levelId, isUnlocked, isCompleted),
                    isUnlocked,
                    isCurrent,
                    HandleItemClicked);

                item.gameObject.SetActive(true);

                var rect = (RectTransform)item.transform;
                _activeItemRects.Add(rect);

                if (isCurrent) _currentItemRect = rect;
                if (levelId == _pendingCelebrationLevel) _celebrateItem = item;
            }

            HideFrom(slot);

            SelectLevel(currentLevel);

            if (_useFocusScaler && _focusScaler != null) _focusScaler.SetTargets(_activeItemRects);

            BeginOpeningFlow();
        }

        /// Có màn cần ăn mừng thì chạy màn ăn mừng, không thì mở như mọi lần.
        private void BeginOpeningFlow()
        {
            var celebrateItem = _celebrateItem;
            var celebrateLevel = _pendingCelebrationLevel;
            _pendingCelebrationLevel = -1;

            if (!isActiveAndEnabled) return;

            StopAllCoroutines();
            StartCoroutine(OpeningRoutine(celebrateItem, celebrateLevel));
        }

        /// Dựng dây nối rồi chạy ăn mừng hoặc cuộn tới màn hiện tại.
        private IEnumerator OpeningRoutine(HomeLevelItemView celebrateItem, int celebrateLevel)
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            RebuildLines();

            if (TryResolveCelebration(celebrateItem, celebrateLevel, out var fromRect, out var sprite))
            {
                yield return WaitBeforeCelebrate();

                yield return CelebrateRoutine(fromRect, sprite, celebrateItem);
                yield break;
            }

            if (_scrollRect == null) yield break;

            if (_currentItemRect != null && TryGetScrollPosition(_currentItemRect, out var position))
            {
                _scrollRect.verticalNormalizedPosition = position;
            }
        }

        /// Chờ tới khi màn hình thật sự nhìn thấy được, rồi mới thả tranh bay.
        private IEnumerator WaitBeforeCelebrate()
        {
            var underTransition = _screenTransition != null && _screenTransition.IsPlaying;

            if (underTransition)
            {
                while (_screenTransition.IsPlaying) yield return null;

                if (_celebrateDelayAfterTransition > 0f)
                {
                    yield return new WaitForSecondsRealtime(_celebrateDelayAfterTransition);
                }

                yield break;
            }

            while (_isEntering) yield return null;
        }

        /// Xác định điểm xuất phát và ảnh cho đoạn ăn mừng.
        private bool TryResolveCelebration(
            HomeLevelItemView item, int levelId, out RectTransform fromRect, out Sprite sprite)
        {
            fromRect = null;
            sprite = null;

            if (levelId < 0) return false;
            if (_collectionFly == null || _collectionButton == null) return false;

            fromRect = _celebrateFromRect != null
                ? _celebrateFromRect
                : item != null ? item.ThumbnailRect : null;

            if (fromRect == null) return false;

            sprite = item != null ? item.ThumbnailSprite : null;

            if (sprite == null)
            {
                var gridData = FindGridData(levelId);
                sprite = BuildThumbnail(gridData, levelId, isUnlocked: true, isCompleted: true);
            }

            return sprite != null;
        }

        /// Cho tranh bay từ fromRect sang nút bộ sưu tập.
        private IEnumerator CelebrateRoutine(RectTransform fromRect, Sprite sprite, HomeLevelItemView item)
        {
            var scrollsWithList = _celebrateFromRect == null && item != null && IsScrollUsable();

            if (scrollsWithList)
            {
                if (TryGetScrollPosition((RectTransform)item.transform, out var startPosition))
                {
                    _scrollRect.verticalNormalizedPosition = startPosition;
                }

                yield return null;

                Canvas.ForceUpdateCanvases();
            }

            var finished = false;

            _collectionFly.Play(
                fromRect,
                sprite,
                (RectTransform)_collectionButton.transform,
                () => finished = true);

            while (!finished) yield return null;

            if (IsScrollUsable()) yield return ScrollToCurrentRoutine(_celebrateScrollSeconds);
        }

        /// Danh sách cuộn có đang thật sự hiện không.
        private bool IsScrollUsable()
        {
            return _scrollRect != null && _scrollRect.gameObject.activeInHierarchy;
        }

        /// Dựng ảnh thu nhỏ của một màn.
        private Sprite BuildThumbnail(
            Gameplay.Data.LevelGridData gridData, int levelId, bool isUnlocked, bool isCompleted)
        {
            if (!isUnlocked || gridData == null) return null;

            var bits = _progressStore != null ? _progressStore.LoadBits(levelId) : null;
            var paintAll = bits == null && isCompleted;

            var sprite = LevelThumbnailBuilder.Build(gridData, bits, paintAll, levelId);
            if (sprite != null) _thumbnails.Add(sprite);

            return sprite;
        }

        /// Dựng lại dây nối giữa các ô liền nhau.
        private void RebuildLines()
        {
            if (_linePrefab == null || _lineRoot == null) return;

            var used = 0;

            for (var i = 0; i + 1 < _activeItemRects.Count; i++)
            {
                var line = GetLine(used++);

                PlaceLine(line, _activeItemRects[i].position, _activeItemRects[i + 1].position);
                line.gameObject.SetActive(true);
            }

            for (var i = used; i < _lines.Count; i++) _lines[i].gameObject.SetActive(false);
        }

        /// Đặt một đoạn dây nối hai điểm, cho trước bằng toạ độ thế giới.
        private void PlaceLine(RectTransform line, Vector3 fromWorld, Vector3 toWorld)
        {
            var from = _lineRoot.InverseTransformPoint(fromWorld);
            var to = _lineRoot.InverseTransformPoint(toWorld);

            var delta = (Vector2)(to - from);
            var length = Mathf.Max(0f, delta.magnitude - _lineInset * 2f);

            line.localPosition = (from + to) * 0.5f;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.sizeDelta = new Vector2(length, _lineWidth);
        }

        private RectTransform GetLine(int slot)
        {
            while (_lines.Count <= slot)
            {
                var created = Instantiate(_linePrefab, _lineRoot);
                created.gameObject.SetActive(false);

                _lines.Add((RectTransform)created.transform);
            }

            return _lines[slot];
        }

        /// Cuộn sao cho ô của màn đang chơi nằm giữa khung nhìn.
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

            if (scrollable <= 0f) return true;

            var itemLocalY = content.InverseTransformPoint(target.position).y;
            var distanceFromTop = content.rect.yMax - itemLocalY;

            var alignment = Mathf.Clamp01(_focusAlignment);
            var itemHeight = target.rect.height;

            var desired = distanceFromTop
                          + itemHeight * (alignment - 0.5f)
                          - viewportHeight * alignment
                          + _focusPadding;

            var offsetFromTop = Mathf.Clamp(desired, 0f, scrollable);

            normalizedPosition = 1f - offsetFromTop / scrollable;
            return true;
        }

        private void HandleItemClicked(int levelId) => SelectLevel(levelId);

        /// Đổi ô đang chọn: bật viền ở đúng một ô, tắt ở mọi ô còn lại, và đổi chữ trên nút Play theo.
        private void SelectLevel(int levelId)
        {
            _selectedLevel = levelId;

            foreach (var item in _items)
            {
                if (item == null || !item.gameObject.activeSelf) continue;

                item.SetSelected(item.LevelId == levelId);
            }

            if (_playLevelText != null) _playLevelText.text = $"Level {levelId}";

            SetPercent(levelId);
        }

        /// Phần trăm đã tô của một màn.
        private void SetPercent(int levelId)
        {
            if (_percentText == null) return;

            var fraction = ResolveProgress(levelId);

            var percent = fraction >= 1f
                ? 100
                : Mathf.Clamp(Mathf.FloorToInt(fraction * 100f), 0, 99);

            _percentText.text = $"{percent}%";
        }

        /// Tỉ lệ đã tô của một màn, thang 0..1.
        private float ResolveProgress(int levelId)
        {
            if (_levelService == null) return 0f;
            if (_levelService.IsCompleted(levelId)) return 1f;
            if (!_levelService.IsUnlocked(levelId)) return 0f;

            var gridData = FindGridData(levelId);
            if (gridData == null) return 0f;

            var bits = _progressStore != null ? _progressStore.LoadBits(levelId) : null;
            if (bits == null) return 0f;

            return Gameplay.Domain.PaintState.FractionPainted(gridData.ToGrid(), bits);
        }

        /// Tìm dữ liệu lưới của một màn.
        private Gameplay.Data.LevelGridData FindGridData(int levelId)
        {
            foreach (var config in _levelService.Levels)
            {
                if (config != null && config.LevelId == levelId) return config.GridData;
            }

            return null;
        }

        /// Hiện số tiền.
        private void SetCoins(int coins)
        {
            if (_coinsText == null) return;
            if (coins == _displayedCoins) return;

            _displayedCoins = coins;
            _coinsText.text = coins.ToString();
        }

        private void HandlePlayClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.Direction);

            Hide();

            var level = _selectedLevel >= 0 ? _selectedLevel : _levelService.CurrentLevel;

            _levelService.LoadLevel(level);
        }

        /// Mở popup bộ sưu tập.
        private void HandleCollectionClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.ButtonClick);

            _popupService.Show(PopupKey.Collection);
        }

        private void HandleSettingsClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.ButtonClick);

            _popupService.Show(PopupKey.SettingsHome);
        }

        /// Lấy hoặc tạo ô màn chơi để tái dùng.
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

        /// Huỷ các ảnh thu nhỏ đã tạo.
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
