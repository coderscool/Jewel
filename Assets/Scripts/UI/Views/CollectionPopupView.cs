using System.Collections.Generic;
using BookCurlPro;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace JewelPainter.UI.Views
{
    /// Popup bộ sưu tập: bày ảnh của mọi màn chơi, màn chưa tới thì khoá lại.
    public class CollectionPopupView : PopupView
    {
        public override bool BlocksBackground => false;

        [SerializeField] private CollectionItemView _itemPrefab;

        [SerializeField] private Button _closeButton;

        [Header("Tiến độ sưu tập")]
        [Tooltip("Dòng chữ tiến trình dạng '5/12'.")]
        [SerializeField] private Text _progressText;

        [Tooltip("Ảnh làm thanh tiến trình.")]
        [SerializeField] private Image _progressFill;

        [Header("Quyển sách")]
        [Tooltip("Component BookPro của quyển sách trong popup này.")]
        [SerializeField] private BookPro _book;

        [Tooltip("Các object chứa ô, theo thứ tự đọc.")]
        [SerializeField] private Transform[] _pageRoots;

        [Tooltip("Mỗi mặt giấy bày bao nhiêu ô.")]
        [SerializeField] private int _itemsPerPage = 15;

        [Tooltip("Thời gian lật một trang, tính bằng giây.")]
        [SerializeField] private float _flipDuration = 0.8f;

        [Header("Nút lật")]
        [SerializeField] private Button _prevButton;

        [SerializeField] private Button _nextButton;

        [Tooltip("Dòng chữ số trang dạng '1/3'.")]
        [SerializeField] private Text _pageText;

        [Tooltip("Ẩn nút lật khi hết trang thay vì làm xám.")]
        [SerializeField] private bool _hideNavAtEnds = true;

        private List<CollectionItemView>[] _items;

        private readonly List<LevelConfig> _visible = new();

        private bool _isFlipping;

        private ILevelService _levelService;

        [Inject]
        public void Construct(ILevelService levelService)
        {
            _levelService = levelService;
        }

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_prevButton != null) _prevButton.onClick.AddListener(ShowPrevPage);
            if (_nextButton != null) _nextButton.onClick.AddListener(ShowNextPage);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
            if (_prevButton != null) _prevButton.onClick.RemoveListener(ShowPrevPage);
            if (_nextButton != null) _nextButton.onClick.RemoveListener(ShowNextPage);
        }

        public override void Show()
        {
            base.Show();

            _isFlipping = false;

            if (_book != null) _book.interactable = false;

            Rebuild();
            ResetToFirstSpread();
        }

        public void ShowPrevPage()
        {
            if (!CanFlip) return;
            if (_book.CurrentPaper <= _book.StartFlippingPaper) return;

            BeginFlip(FlipMode.LeftToRight);
        }

        public void ShowNextPage()
        {
            if (!CanFlip) return;
            if (_book.CurrentPaper > _book.EndFlippingPaper) return;

            BeginFlip(FlipMode.RightToLeft);
        }

        private bool CanFlip => _book != null && !_isFlipping;

        private void BeginFlip(FlipMode mode)
        {
            _isFlipping = true;

            RefreshNav();

            PageFlipper.FlipPage(_book, _flipDuration, mode, () =>
            {
                _isFlipping = false;
                RefreshNav();
            });
        }

        private void ResetToFirstSpread()
        {
            if (_book == null) return;

            _book.CurrentPaper = 0;

            _book.UpdatePages();

            RefreshNav();
        }

        private void Rebuild()
        {
            if (_levelService == null || _itemPrefab == null || _pageRoots == null || _pageRoots.Length == 0)
            {
                Debug.LogWarning($"{nameof(CollectionPopupView)} thiếu Item Prefab, Page Roots hoặc " +
                                 "chưa được inject — popup sẽ trống.");
                return;
            }

            _visible.Clear();

            var collected = 0;

            foreach (var config in _levelService.Levels)
            {
                if (config == null) continue;

                _visible.Add(config);

                if (_levelService.IsCompleted(config.LevelId)) collected++;
            }

            var perPage = Mathf.Max(1, _itemsPerPage);
            var faces = Mathf.CeilToInt(_visible.Count / (float)perPage);

            if (faces > _pageRoots.Length)
            {
                Debug.LogWarning(
                    $"{nameof(CollectionPopupView)}: cần {faces} mặt giấy cho {_visible.Count} màn " +
                    $"nhưng chỉ có {_pageRoots.Length}. {(faces - _pageRoots.Length) * perPage} màn cuối " +
                    "sẽ không hiện. Thêm paper cho quyển sách trong Inspector.");

                faces = _pageRoots.Length;
            }

            for (var face = 0; face < _pageRoots.Length; face++)
            {
                var first = face * perPage;
                var last = Mathf.Min(first + perPage, _visible.Count);
                var slot = 0;

                for (var i = first; i < last; i++)
                {
                    var config = _visible[i];
                    var item = GetItem(face, slot++);

                    item.Bind(config.LevelId, config.TargetImage,
                        _levelService.IsCompleted(config.LevelId), config.CollectionFit);
                    item.gameObject.SetActive(true);
                }

                HideFrom(face, slot);
            }

            SetProgress(collected, _visible.Count);

            ApplyFlippingRange(faces);
        }

        /// Giới hạn phạm vi lật trang.
        private void ApplyFlippingRange(int faces)
        {
            if (_book == null) return;

            var maxSpread = 0;

            for (var i = 0; i < faces; i++)
            {
                var spread = ResolveSpread(_pageRoots[i]);

                if (spread < 0)
                {
                    Debug.LogWarning(
                        $"{nameof(CollectionPopupView)}: Page Roots[{i}] không nằm trong trang nào " +
                        "của quyển sách. Kéo object Items nằm BÊN TRONG một Page, và kiểm lại " +
                        "xem quyển sách đã gán đúng component BookPro chưa.");
                    continue;
                }

                if (spread > maxSpread) maxSpread = spread;
            }

            _book.StartFlippingPaper = 0;

            _book.EndFlippingPaper = Mathf.Max(0, maxSpread - 1);
        }

        /// Mặt giấy này hiện ra ở cặp trang thứ mấy.
        private int ResolveSpread(Transform root)
        {
            if (root == null || _book == null || _book.papers == null) return -1;

            for (var p = 0; p < _book.papers.Length; p++)
            {
                var paper = _book.papers[p];
                if (paper == null) continue;

                if (paper.Front != null && root.IsChildOf(paper.Front.transform)) return p;
                if (paper.Back != null && root.IsChildOf(paper.Back.transform)) return p + 1;
            }

            return -1;
        }

        private void RefreshNav()
        {
            var spread = _book != null ? _book.CurrentPaper : 0;
            var spreadCount = _book != null ? _book.EndFlippingPaper + 2 : 1;

            if (_pageText != null) _pageText.text = $"{spread + 1}/{spreadCount}";

            SetNav(_prevButton, !_isFlipping && spread > 0, spreadCount);
            SetNav(_nextButton, !_isFlipping && spread < spreadCount - 1, spreadCount);
        }

        private void SetNav(Button button, bool usable, int spreadCount)
        {
            if (button == null) return;

            if (spreadCount <= 1 || _hideNavAtEnds)
            {
                var visible = spreadCount > 1 && usable;

                if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
                return;
            }

            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

            button.interactable = usable;
        }

        private void SetProgress(int collected, int total)
        {
            if (_progressText != null) _progressText.text = $"{collected}/{total}";

            if (_progressFill == null) return;

            _progressFill.fillAmount = total > 0 ? (float)collected / total : 0f;
        }

        /// Lấy hoặc tạo ô tranh để tái dùng.
        private CollectionItemView GetItem(int face, int slot)
        {
            if (_items == null) _items = new List<CollectionItemView>[_pageRoots.Length];
            if (_items[face] == null) _items[face] = new List<CollectionItemView>();

            var pool = _items[face];

            while (pool.Count <= slot) pool.Add(Instantiate(_itemPrefab, _pageRoots[face]));

            return pool[slot];
        }

        private void HideFrom(int face, int slot)
        {
            if (_items == null || _items[face] == null) return;

            var pool = _items[face];

            for (var i = slot; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
    }
}
