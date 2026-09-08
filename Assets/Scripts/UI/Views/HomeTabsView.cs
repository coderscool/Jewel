using System;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Hai thẻ ở đáy màn hình Home: một bên là danh sách màn chơi, một bên là cửa hàng.
    ///
    /// KHÔNG có Init và không nhận phụ thuộc nào — khác mọi view còn lại trong project,
    /// và đó là chủ ý. Lớp này chỉ bật một object và tắt object kia; nó không hỏi tiến
    /// trình, không hỏi ví, không hỏi màn chơi. Bắt nó đi qua GameEntryPoint chỉ để giữ
    /// cho giống nhau là thêm một mắt xích phải nhớ nối, đổi lấy con số không.
    ///
    /// Nội dung bên trong mỗi thẻ tự lo phần của mình. Cửa hàng cần ví tiền thì component
    /// của cửa hàng nhận ví, không phải lớp này.
    ///
    /// Hai cái nút LUÔN hiện; thứ đổi là bộ ảnh bên trong chúng. Bấm vào thẻ đang mở thì
    /// không có gì xảy ra — Apply thoát ngay ở phép so đầu tiên.
    public class HomeTabsView : MonoBehaviour
    {
        public enum Tab
        {
            Home = 0,
            Shop = 1,
        }

        [Header("Hai khung nội dung")]
        [Tooltip("Khung danh sách màn chơi. Đây là thứ bị TẮT khi chuyển sang cửa hàng, " +
                 "nên nó KHÔNG được là object mà Home View đang dùng làm Content — bật tắt " +
                 "cùng một object cho hai việc khác nhau là hai bên giành nhau.")]
        [SerializeField] private GameObject _homePanel;

        [SerializeField] private GameObject _shopPanel;

        [Header("Hai nút")]
        [Tooltip("Cả hai nút LUÔN hiện. Cái đổi là bộ ảnh bên trong chúng — xem mục dưới.")]
        [SerializeField] private Button _homeButton;

        [SerializeField] private Button _shopButton;

        [Header("Hai trạng thái của mỗi nút")]
        [Tooltip("Phần hiện khi thẻ Home ĐANG mở — bản 'choose' của nút Home.\n\n" +
                 "Bốn ô này bật tắt xen kẽ nhau: mỗi nút lúc nào cũng có đúng một bản đang " +
                 "hiện. Để trống cả bốn thì hai nút trông như nhau ở mọi lúc, chức năng " +
                 "chuyển thẻ vẫn chạy.")]
        [SerializeField] private GameObject _homeChosen;

        [Tooltip("Phần hiện khi thẻ Home đang ĐÓNG — bản 'unchoose' của nút Home.")]
        [SerializeField] private GameObject _homeUnchosen;

        [SerializeField] private GameObject _shopChosen;

        [SerializeField] private GameObject _shopUnchosen;

        [Tooltip("Mỗi lần màn hình Home mở lại thì về thẻ Home.\n\n" +
                 "Bỏ tick là nhớ thẻ cũ: chơi xong một màn quay ra sẽ thấy cửa hàng nếu " +
                 "lần trước đang ở đó. Tick vào vì người chơi vừa xong một màn thì thứ họ " +
                 "muốn thấy là bức tranh kế tiếp, không phải quầy hàng.\n\n" +
                 "Chỉ chạy khi object mang script này bị bật tắt cùng màn hình Home — xem " +
                 "chú thích ở OnEnable.")]
        [SerializeField] private bool _resetToHomeOnEnable = true;

        /// Thẻ đang mở. -1 nghĩa là chưa đặt lần nào, để lần đặt đầu tiên không bị bỏ qua.
        private int _current = -1;

        public Tab Current => _current == (int)Tab.Shop ? Tab.Shop : Tab.Home;

        /// Bắn khi đổi thẻ. Nội dung cửa hàng nghe cái này để nạp dữ liệu đúng lúc mở,
        /// thay vì nạp sẵn từ đầu game.
        public event Action<Tab> OnTabChanged;

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

        /// OnEnable chứ không phải một hàm để Home gọi vào.
        ///
        /// Đổi lại lớp này không cần ai biết tới nó, mà vẫn về đúng thẻ mỗi lần Home mở
        /// — với điều kiện object mang script nằm TRONG phần mà HomeScreenView bật tắt.
        /// Nằm ngoài thì nó không bao giờ bị tắt, OnEnable chỉ chạy đúng một lần lúc vào
        /// game, và thanh thẻ sẽ nằm chình ình trên màn chơi.
        private void OnEnable()
        {
            if (_resetToHomeOnEnable) Apply(Tab.Home);
        }

        /// Gọi được từ OnClick trong Inspector, nếu bạn thích nối tay hơn.
        public void ShowHome() => Apply(Tab.Home);

        public void ShowShop() => Apply(Tab.Shop);

        public void Show(Tab tab) => Apply(tab);

        private void Apply(Tab tab)
        {
            if ((int)tab == _current) return;

            _current = (int)tab;

            var isHome = tab == Tab.Home;

            SetActive(_homePanel, isHome);
            SetActive(_shopPanel, !isHome);

            // Hai nút đứng nguyên, chỉ đổi bộ ảnh bên trong. Bốn ô bật tắt xen kẽ nên
            // mỗi nút lúc nào cũng có đúng một bản đang hiện.
            //
            // Bật tắt object chứ không đổi sprite trên một Image: hai trạng thái thường
            // khác nhau nhiều hơn một cái ảnh — khác cả cỡ chữ, có thêm icon, có nền
            // riêng. Dựng sẵn hai bộ rồi bật tắt thì đổi mỹ thuật không phải sửa code.
            SetActive(_homeChosen, isHome);
            SetActive(_homeUnchosen, !isHome);

            SetActive(_shopChosen, !isHome);
            SetActive(_shopUnchosen, isHome);

            OnTabChanged?.Invoke(tab);
        }

        /// Bỏ qua ô để trống, và bỏ qua luôn khi giá trị không đổi: SetActive chạy qua cả
        /// cây con và gửi thông điệp vòng đời, đắt hơn hẳn một phép so.
        private static void SetActive(GameObject target, bool active)
        {
            if (target == null) return;
            if (target.activeSelf == active) return;

            target.SetActive(active);
        }

    }
}
