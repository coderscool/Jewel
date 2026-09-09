using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
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

        [Header("Chia trang")]
        [Tooltip("Mỗi trang bày bao nhiêu ô.\n\n" +
                 "Để 0 hoặc âm là TẮT chia trang — bày hết trong một trang như bản cũ, và " +
                 "hai cái nút bên dưới tự ẩn đi.")]
        [SerializeField] private int _itemsPerPage = 15;

        [Tooltip("Nút về trang trước. Để trống thì không có nút.")]
        [SerializeField] private Button _prevButton;

        [SerializeField] private Button _nextButton;

        [Tooltip("Dòng chữ dạng '1/3'. Để trống thì bỏ qua.")]
        [SerializeField] private Text _pageText;

        [Tooltip("Hết trang thì ẨN HẲN nút thay vì chỉ làm xám.\n\n" +
                 "Bỏ tick là nút vẫn nằm đó nhưng bấm không ăn. Hai kiểu đều dùng được, " +
                 "chỉ đừng để nút sáng mà bấm không làm gì — đó là lời nói dối nhỏ mà " +
                 "người chơi phải bấm vài lần mới nhận ra.")]
        [SerializeField] private bool _hideNavAtEnds = true;

        [Tooltip("TẠM THỜI. In ra Console mỗi cú bấm lật trang và trạng thái hai cái nút. " +
                 "Tắt lại sau khi đã tìm ra nguyên nhân.")]
        [SerializeField] private bool _logPaging;

        private readonly List<CollectionItemView> _items = new();

        /// Các màn THẬT SỰ bày ra, đã lọc bỏ ô trống trong Inspector.
        ///
        /// Giữ lại thành danh sách riêng thay vì lọc lại mỗi lần lật trang: chỉ số trang
        /// phải trỏ vào một dãy liên tục, mà mảng Levels thì có thể thủng lỗ ở giữa.
        private readonly List<LevelConfig> _visible = new();

        private int _page;
        private int _pageCount = 1;

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

            // Mở lại là về trang đầu. Nhớ trang cũ nghe thì tử tế hơn, nhưng người chơi
            // mở bộ sưu tập sau khi xong một màn thì thứ họ tìm nằm ở trang họ đang xem
            // dở lần trước chỉ là tình cờ — còn trang đầu thì luôn là chỗ bắt đầu đọc.
            _page = 0;

            Rebuild();
        }

        public void ShowPrevPage()
        {
            if (_logPaging) Debug.Log($"[Collection] bấm PREV — đang ở trang {_page + 1}/{_pageCount}");

            GoToPage(_page - 1);
        }

        public void ShowNextPage()
        {
            if (_logPaging) Debug.Log($"[Collection] bấm NEXT — đang ở trang {_page + 1}/{_pageCount}");

            GoToPage(_page + 1);
        }

        private void GoToPage(int page)
        {
            var clamped = Mathf.Clamp(page, 0, Mathf.Max(0, _pageCount - 1));

            if (_logPaging) Debug.Log($"[Collection] xin trang {page + 1}, kẹp thành {clamped + 1}");

            if (clamped == _page) return;

            _page = clamped;

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

            _visible.Clear();

            var collected = 0;

            foreach (var config in _levelService.Levels)
            {
                // Ô bỏ trống trong Inspector không được chiếm chỗ, không thì bộ sưu tập
                // thủng một lỗ ở giữa mà không ai hiểu vì sao.
                if (config == null) continue;

                _visible.Add(config);

                // IsCompleted chứ không phải IsUnlocked. Bộ sưu tập là chỗ bày thứ đã LÀM
                // XONG; màn đang tô dở tuy đã mở khoá nhưng bức tranh chưa có, bày ra là
                // hứa nhầm với người chơi.
                if (_levelService.IsCompleted(config.LevelId)) collected++;
            }

            var perPage = _itemsPerPage > 0 ? _itemsPerPage : Mathf.Max(1, _visible.Count);

            // Luôn có ÍT NHẤT một trang, kể cả khi chưa có màn nào: 0 trang thì dòng chữ
            // hiện ra "1/0" và hai cái nút không biết mình đang ở đâu.
            _pageCount = Mathf.Max(1, Mathf.CeilToInt(_visible.Count / (float)perPage));
            _page = Mathf.Clamp(_page, 0, _pageCount - 1);

            var first = _page * perPage;
            var last = Mathf.Min(first + perPage, _visible.Count);
            var slot = 0;

            for (var i = first; i < last; i++)
            {
                var config = _visible[i];
                var item = GetItem(slot++);

                item.Bind(config.LevelId, config.TargetImage, _levelService.IsCompleted(config.LevelId));
                item.gameObject.SetActive(true);
            }

            HideFrom(slot);

            // Tiến độ đếm trên TOÀN BỘ bộ sưu tập, không riêng trang đang xem. Người chơi
            // hỏi "tôi có bao nhiêu tranh rồi", không hỏi "trang này có bao nhiêu".
            SetProgress(collected, _visible.Count);

            RefreshNav();
        }

        private void RefreshNav()
        {
            if (_pageText != null) _pageText.text = $"{_page + 1}/{_pageCount}";

            SetNav(_prevButton, _page > 0);
            SetNav(_nextButton, _page < _pageCount - 1);

            if (!_logPaging) return;

            Debug.Log($"[Collection] trang {_page + 1}/{_pageCount} — " +
                      $"prev gán={_prevButton != null} " +
                      $"bật={(_prevButton != null && _prevButton.gameObject.activeInHierarchy)} " +
                      $"bấm được={(_prevButton != null && _prevButton.interactable)} | " +
                      $"next gán={_nextButton != null} " +
                      $"bật={(_nextButton != null && _nextButton.gameObject.activeInHierarchy)} " +
                      $"bấm được={(_nextButton != null && _nextButton.interactable)}");
        }

        private void SetNav(Button button, bool usable)
        {
            if (button == null) return;

            // Chỉ có MỘT trang thì giấu cả hai nút bất kể kiểu nào: một cặp nút xám ngắt
            // nằm dưới bộ sưu tập chỉ nói với người chơi rằng còn trang khác mà họ không
            // với tới được.
            if (_pageCount <= 1 || _hideNavAtEnds)
            {
                var visible = _pageCount > 1 && usable;

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

            // Chia cho 0 khi chưa có màn nào — trong Inspector rất dễ gặp lúc mảng Levels
            // còn trống, và NaN thì Image vẽ ra một thanh trống trơn không ai lần được vì sao.
            _progressFill.fillAmount = total > 0 ? (float)collected / total : 0f;
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
