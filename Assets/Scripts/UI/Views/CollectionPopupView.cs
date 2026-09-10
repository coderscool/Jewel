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
    ///
    /// Trình bày như một QUYỂN SÁCH (Book-Page Curl Pro). Vài điều của package đó quyết
    /// định hẳn cách lớp này viết, nên ghi lại ở đây:
    ///
    /// - Một "paper" có HAI mặt: Front nằm bên phải, Back nằm bên trái. Thứ tự đọc là
    ///   Front0 → Back0 → Front1 → Back1… tức là mặt giấy thứ f nằm ở object "Page{f}".
    ///
    /// - Cặp trang đang nhìn thấy ở CurrentPaper = c là Back của paper c−1 (trái) và
    ///   Front của paper c (phải). Nên MỌI mặt phải được dựng sẵn nội dung, không thể chỉ
    ///   dựng riêng trang đang xem — hai mặt hiện cùng lúc.
    ///
    /// - Sách ẩn trang bằng CanvasGroup.alpha = 0 chứ không SetActive, nên mọi ô của mọi
    ///   trang đều sống cùng lúc. Vài chục Image thì không sao; đừng nhét việc nặng vào
    ///   Start của ô sưu tập.
    ///
    /// Popup được PopupManager tạo qua IObjectResolver nên [Inject] ở đây chạy được —
    /// Object.Instantiate thường thì không.
    ///
    /// Dựng lại danh sách ở mỗi lần Show chứ không ở Awake: qua được một màn là ổ khoá
    /// phải rơi ra, mà popup thì sống suốt phiên chơi.
    public class CollectionPopupView : PopupView
    {
        [SerializeField] private CollectionItemView _itemPrefab;

        [SerializeField] private Button _closeButton;

        [Header("Tiến độ sưu tập")]
        [Tooltip("Dòng chữ dạng '5/12'. Để trống thì bỏ qua.")]
        [SerializeField] private Text _progressText;

        [Tooltip("Ảnh làm thanh tiến trình. Image Type phải để FILLED, không thì fillAmount " +
                 "không có tác dụng gì và thanh lúc nào cũng đầy.")]
        [SerializeField] private Image _progressFill;

        [Header("Quyển sách")]
        [Tooltip("Component BookPro của quyển sách trong popup này.")]
        [SerializeField] private BookPro _book;

        [Tooltip("Object chứa các ô, xếp đúng THỨ TỰ ĐỌC. Phần tử 0 là mặt giấy hiện ra " +
                 "trước nhất.\n\n" +
                 "KHÔNG bắt buộc phải là các mặt liên tiếp. Muốn lưới chỉ nằm ở trang PHẢI " +
                 "và chừa cặp đầu làm bìa thì cứ kéo Items của Page2, Page4, Page6 — code " +
                 "tự dò xem mỗi mặt thuộc paper nào để đặt tầm lật cho đúng.\n\n" +
                 "Kéo Items chứ KHÔNG kéo Page: sách reparent chính object Page về book " +
                 "panel ở mỗi lần UpdatePages, nên đổ ô thẳng vào đó là trộn với đồ của sách.\n\n" +
                 "Lưu ý prefab BookPro xuất xưởng ĐÃ CÓ SẴN 2 paper, nên Page4 trở đi mới là " +
                 "mấy paper bạn tự thêm.")]
        [SerializeField] private Transform[] _pageRoots;

        [Tooltip("Mỗi MẶT GIẤY bày bao nhiêu ô.")]
        [SerializeField] private int _itemsPerPage = 15;

        [Tooltip("Thời gian lật một trang, tính bằng giây.")]
        [SerializeField] private float _flipDuration = 0.8f;

        [Header("Nút lật")]
        [SerializeField] private Button _prevButton;

        [SerializeField] private Button _nextButton;

        [Tooltip("Dòng chữ dạng '1/3'. Đếm theo CẶP TRANG đang mở, không phải theo mặt giấy.")]
        [SerializeField] private Text _pageText;

        [Tooltip("Hết trang thì ẨN HẲN nút thay vì chỉ làm xám.")]
        [SerializeField] private bool _hideNavAtEnds = true;

        /// Pool riêng cho TỪNG mặt giấy. Một pool chung không dùng được: ô phải nằm đúng
        /// object của mặt chứa nó, mà mỗi mặt lại giữ nguyên nội dung suốt lúc popup mở.
        private List<CollectionItemView>[] _items;

        /// Các màn THẬT SỰ bày ra, đã lọc bỏ ô trống trong Inspector.
        private readonly List<LevelConfig> _visible = new();

        /// Đang có một cú lật chạy dở. PageFlipper KHÔNG tự chặn lời gọi chồng — AutoFlip
        /// của package cũng phải tự giữ một cờ y hệt.
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

            // Tắt vuốt tay. Sách vẫn nghe chạm qua EventTrigger trong prefab của nó, và
            // interactable là cái công tắc duy nhất chặn được đường đó — thêm nữa,
            // BookPro.OnMouseDragRightPage đọc Input.mousePosition của Input System CŨ,
            // thứ mà project này không bật.
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

            // Khoá nút NGAY, không đợi lật xong. Bấm chồng trong lúc trang đang bay thì
            // BookPro nhảy hai paper một lúc và cặp trang hiện ra không khớp với số trang.
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

            // Gọi thẳng UpdatePages: setter của CurrentPaper chỉ dựng lại khi giá trị ĐỔI,
            // nên mở popup lần thứ hai (vốn đã ở trang 0) sẽ không dựng lại gì cả.
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
                // Ô bỏ trống trong Inspector không được chiếm chỗ, không thì bộ sưu tập
                // thủng một lỗ ở giữa mà không ai hiểu vì sao.
                if (config == null) continue;

                _visible.Add(config);

                // IsCompleted chứ không phải IsUnlocked. Bộ sưu tập là chỗ bày thứ đã LÀM
                // XONG; màn đang tô dở tuy đã mở khoá nhưng bức tranh chưa có.
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

            // Dựng HẾT mọi mặt, không riêng mặt đang xem: hai mặt hiện cùng lúc, và mặt
            // bên trái chính là mặt vừa lật qua.
            for (var face = 0; face < _pageRoots.Length; face++)
            {
                var first = face * perPage;
                var last = Mathf.Min(first + perPage, _visible.Count);
                var slot = 0;

                for (var i = first; i < last; i++)
                {
                    var config = _visible[i];
                    var item = GetItem(face, slot++);

                    item.Bind(config.LevelId, config.TargetImage, _levelService.IsCompleted(config.LevelId));
                    item.gameObject.SetActive(true);
                }

                HideFrom(face, slot);
            }

            // Tiến độ đếm trên TOÀN BỘ bộ sưu tập, không riêng trang đang xem.
            SetProgress(collected, _visible.Count);

            ApplyFlippingRange(faces);
        }

        /// Chặn không cho lật quá mặt giấy cuối cùng có nội dung.
        ///
        /// DÒ từ chính các object đã gán chứ không suy từ số lượng. Bản trước suy ra, và
        /// nó chỉ đúng khi các mặt nằm liên tiếp từ Page0 — gán vào Page2, Page4, Page6
        /// (lưới chỉ ở trang phải) là nó tính hụt và người chơi lật tới trang hai thì kẹt.
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

            // CurrentPaper bị setter kẹp ở EndFlippingPaper + 1, mà cặp trang cuối cần tới
            // chính là maxSpread — nên trừ 1.
            _book.EndFlippingPaper = Mathf.Max(0, maxSpread - 1);
        }

        /// Mặt giấy này hiện ra ở cặp trang thứ mấy. -1 khi nó không thuộc trang nào.
        ///
        /// Front của paper p là trang PHẢI của cặp p; Back của paper p là trang TRÁI của
        /// cặp p+1. Đó là toàn bộ luật, đọc thẳng ra từ BookPro.UpdatePages.
        ///
        /// IsChildOf chứ không so parent trực tiếp: bạn có quyền lồng Items sâu mấy tầng
        /// trong trang cũng được.
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

            // Trong lúc trang đang bay thì cả hai nút đều tắt, dù còn trang để đi.
            SetNav(_prevButton, !_isFlipping && spread > 0, spreadCount);
            SetNav(_nextButton, !_isFlipping && spread < spreadCount - 1, spreadCount);
        }

        private void SetNav(Button button, bool usable, int spreadCount)
        {
            if (button == null) return;

            // Chỉ có MỘT cặp trang thì giấu cả hai nút bất kể kiểu nào: một cặp nút xám
            // ngắt chỉ nói với người chơi rằng còn trang khác mà họ không với tới được.
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

            // Chia cho 0 khi chưa có màn nào — NaN thì Image vẽ ra một thanh trống trơn
            // không ai lần được vì sao.
            _progressFill.fillAmount = total > 0 ? (float)collected / total : 0f;
        }

        /// Tạo một lần rồi bật/tắt để tái dùng — không Instantiate/Destroy mỗi lần mở.
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
