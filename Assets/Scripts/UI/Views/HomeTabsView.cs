using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Chuyển giữa thẻ danh sách màn chơi và thẻ cửa hàng ở Home.
    public class HomeTabsView : MonoBehaviour
    {
        public enum Tab
        {
            Home = 0,
            Shop = 1,
        }

        [Header("Hai khung nội dung")]
        [Tooltip("Khung danh sách màn chơi.")]
        [SerializeField] private GameObject _homePanel;

        [SerializeField] private GameObject _shopPanel;

        [Header("Hai nút")]
        [Tooltip("Nút thẻ Home.")]
        [SerializeField] private Button _homeButton;

        [SerializeField] private Button _shopButton;

        [Header("Hai trạng thái của mỗi nút")]
        [Tooltip("Phần hiện khi thẻ Home đang mở.")]
        [SerializeField] private GameObject _homeChosen;

        [Tooltip("Phần hiện khi thẻ Home đang đóng.")]
        [SerializeField] private GameObject _homeUnchosen;

        [SerializeField] private GameObject _shopChosen;

        [SerializeField] private GameObject _shopUnchosen;

        [Header("Icon nhô lên khi thẻ được chọn")]
        [Tooltip("Icon của thẻ Home.")]
        [SerializeField] private RectTransform _homeIcon;

        [Tooltip("Icon của thẻ cửa hàng.")]
        [SerializeField] private RectTransform _shopIcon;

        [Tooltip("Độ nhô lên của icon thẻ đang mở, tính bằng pixel.")]
        [SerializeField] private float _iconRise = 24f;

        [Tooltip("Về thẻ Home mỗi lần màn hình Home mở lại.")]
        [SerializeField] private bool _resetToHomeOnEnable = true;

        private int _current = -1;

        private Vector2 _homeIconBase;
        private bool _hasHomeIconBase;

        private Vector2 _shopIconBase;
        private bool _hasShopIconBase;

        private void Awake()
        {
            if (_homeButton != null) _homeButton.onClick.AddListener(ShowHome);
            if (_shopButton != null) _shopButton.onClick.AddListener(ShowShop);

            Apply(Tab.Home);
        }

        private void OnDestroy()
        {
            if (_homeButton != null) _homeButton.onClick.RemoveListener(ShowHome);
            if (_shopButton != null) _shopButton.onClick.RemoveListener(ShowShop);
        }

        /// Về thẻ Home mỗi lần bật.
        private void OnEnable()
        {
            if (_resetToHomeOnEnable) Apply(Tab.Home);
        }

        /// Mở thẻ Home.
        public void ShowHome() => Apply(Tab.Home);

        public void ShowShop() => Apply(Tab.Shop);

        private void Apply(Tab tab)
        {
            if ((int)tab == _current) return;

            _current = (int)tab;

            var isHome = tab == Tab.Home;

            SetActive(_homePanel, isHome);
            SetActive(_shopPanel, !isHome);

            SetActive(_homeChosen, isHome);
            SetActive(_homeUnchosen, !isHome);

            SetActive(_shopChosen, !isHome);
            SetActive(_shopUnchosen, isHome);

            ApplyRise(_homeIcon, isHome, ref _homeIconBase, ref _hasHomeIconBase);
            ApplyRise(_shopIcon, !isHome, ref _shopIconBase, ref _hasShopIconBase);
        }

        /// Nâng icon lên hoặc trả nó về chỗ cũ.
        private void ApplyRise(RectTransform icon, bool up, ref Vector2 basePosition, ref bool hasBase)
        {
            if (icon == null) return;

            if (!hasBase)
            {
                basePosition = icon.anchoredPosition;
                hasBase = true;
            }

            icon.anchoredPosition = up
                ? basePosition + new Vector2(0f, _iconRise)
                : basePosition;
        }

        /// Bật tắt object, bỏ qua khi null hoặc không đổi.
        private static void SetActive(GameObject target, bool active)
        {
            if (target == null) return;
            if (target.activeSelf == active) return;

            target.SetActive(active);
        }
    }
}
