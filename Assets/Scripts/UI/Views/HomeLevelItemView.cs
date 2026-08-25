using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô màn chơi trong danh sách ở màn hình Home.
    ///
    /// Ba trạng thái, và ô này không tự suy ra trạng thái nào — bên gọi truyền vào.
    /// Nó chỉ biết bật tắt đúng thứ.
    public class HomeLevelItemView : MonoBehaviour
    {
        [Tooltip("Ảnh của màn. Với màn đã xong là tranh hoàn thiện, với màn đang chơi " +
                 "là ảnh tiến độ ngay lúc này.")]
        [SerializeField] private Image _thumbnail;

        [Tooltip("Hiện khi màn chưa mở khoá — ô xám trơn, hoặc ổ khoá tuỳ bạn dựng.")]
        [SerializeField] private GameObject _lockedPlaceholder;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Số màn. CHỈ hiện ở màn chưa mở khoá — màn đã mở thì chính bức tranh " +
                 "đã nói nó là màn nào, thêm con số vào chỉ che mất tranh.")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Viền hoặc dấu hiệu cho màn ĐANG chơi dở, để nó nổi lên giữa danh sách.")]
        [SerializeField] private GameObject _currentHighlight;

        [Header("Chọn")]
        [Tooltip("Viền hiện khi ô này ĐANG ĐƯỢC CHỌN. Khác Current Highlight: cái kia nói " +
                 "'đây là màn đang chơi dở', cái này nói 'bạn vừa bấm vào đây'.")]
        [SerializeField] private GameObject _outline;

        [Tooltip("Vùng bấm để chọn ô. Thường là một Button phủ kín ô. Màn chưa mở khoá thì " +
                 "nó tự bị tắt.")]
        [SerializeField] private Button _button;

        private Action<int> _onClicked;
        private bool _hasWarned;

        /// Màn mà ô này đang hiện. Danh sách tái dùng ô nên chỉ số trong mảng không nói
        /// lên màn nào — phải hỏi chính ô.
        public int LevelId { get; private set; }

        /// Chỗ bức tranh đang đứng, để hiệu ứng bay xuất phát đúng từ đó thay vì từ tâm ô.
        public RectTransform ThumbnailRect =>
            _thumbnail != null ? (RectTransform)_thumbnail.transform : null;

        public Sprite ThumbnailSprite => _thumbnail != null ? _thumbnail.sprite : null;

        /// Đăng ký MỘT LẦN ở Awake, không phải mỗi lần Bind.
        ///
        /// Danh sách tái dùng ô nên Bind chạy lại mỗi lần mở Home; thêm listener ở đó thì
        /// một cú bấm sẽ nổ nhiều lần. Callback thì thay được vì nó chỉ là một trường.
        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(int levelId, Sprite thumbnail, bool isUnlocked, bool isCurrent, Action<int> onClicked)
        {
            WarnOnMissingReferences();

            LevelId = levelId;
            _onClicked = onClicked;

            // Màn chưa mở khoá thì không bấm được. Cho bấm rồi lại không cho chơi là kiểu
            // phản hồi tệ nhất: người chơi tưởng máy lỗi chứ không hiểu là mình chưa tới.
            if (_button != null) _button.interactable = isUnlocked;

            if (_levelText != null)
            {
                _levelText.gameObject.SetActive(!isUnlocked);

                // Chỉ đổ chữ khi thật sự hiện: SetText trên object đang tắt vẫn tốn một
                // lần dựng lại lưới chữ ở lần bật kế tiếp, mà lần đó có thể không bao
                // giờ tới.
                if (!isUnlocked) _levelText.SetText("{0}", levelId);
            }

            if (_thumbnail != null)
            {
                // Tắt component thay vì để sprite null: Image không sprite vẫn vẽ một ô
                // trắng đặc, trông như lỗi hiển thị chứ không như ô trống.
                _thumbnail.enabled = isUnlocked && thumbnail != null;
                _thumbnail.sprite = thumbnail;
            }

            if (_lockedPlaceholder != null) _lockedPlaceholder.SetActive(!isUnlocked);
            if (_currentHighlight != null) _currentHighlight.SetActive(isCurrent);
        }

        public void SetSelected(bool selected)
        {
            if (_outline == null) return;

            if (_outline.activeSelf != selected) _outline.SetActive(selected);
        }

        private void HandleClicked() => _onClicked?.Invoke(LevelId);

        /// Quên kéo ô vào Inspector là lỗi im lặng: ô trên Home chỉ còn mỗi số màn, mà
        /// không có gì trong Console để lần ra. Báo một lần rồi thôi — hàm này chạy cho
        /// mọi ô, mỗi lần mở Home.
        private void WarnOnMissingReferences()
        {
            if (_hasWarned) return;
            if (_thumbnail != null && _lockedPlaceholder != null) return;

            _hasWarned = true;

            Debug.LogWarning($"{nameof(HomeLevelItemView)} trên '{name}' còn ô chưa gán: " +
                             $"Thumbnail={(_thumbnail != null ? "ok" : "TRỐNG")}, " +
                             $"Locked Placeholder={(_lockedPlaceholder != null ? "ok" : "TRỐNG")}.",
                this);
        }
    }
}
