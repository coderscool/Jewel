using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// View thuần trình bày: nghe ILevelService, đổ chữ ra màn hình.
    /// Chỉ SetText khi giá trị thật sự đổi — SetText mỗi frame sinh rác GC.
    ///
    /// Nút gợi ý ở đây chỉ làm hai việc: bấm thì gọi IHintService, và tự bật/tắt theo
    /// tín hiệu của nó. Tìm ô nào, đưa camera đi đâu là chuyện của Gameplay.
    ///
    /// Cả HUD tự ẩn khi thắng màn và hiện lại khi màn mới bắt đầu, để popup thắng màn
    /// đứng một mình trên bức tranh vừa hoàn thành.
    public class HudView : MonoBehaviour
    {
        [Tooltip("Nút gợi ý. Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _hintButton;

        [Tooltip("Số lượt gợi ý miễn phí còn lại. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _hintCreditsText;

        [Tooltip("Huy hiệu bọc con số. Tự ẩn khi hết lượt — lúc đó nút chuyển sang mời " +
                 "xem quảng cáo, mà một số 0 nằm cạnh lời mời chỉ gây nhiễu.")]
        [SerializeField] private GameObject _hintCreditsBadge;

        [Tooltip("Nút Tô lại: xoá sạch tiến độ tô của màn đang chơi rồi nạp lại từ đầu.\n\n" +
                 "Tự xám đi khi chưa tô ô nào — bấm vào lúc đó không có gì xảy ra, mà nút " +
                 "bấm được nhưng không làm gì là lời nói dối nhỏ người chơi phải mất một " +
                 "lúc mới nhận ra.\n\n" +
                 "Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _resetButton;

        [Tooltip("Nút bánh răng: mở popup Cài đặt. Đường về Home nằm TRONG popup đó.")]
        [FormerlySerializedAs("_collectionButton")]
        [FormerlySerializedAs("_homeButton")]
        [SerializeField] private Button _settingsButton;

        [Tooltip("Object bị ẩn khi thắng màn. Để TRỐNG thì ẩn chính object này — cách " +
                 "đó vẫn chạy đúng, chỉ là không tách được phần nào của HUD ở lại.")]
        [SerializeField] private GameObject _content;
        private HomeScreenView _home;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private IHintService _hintService;
        private ILevelFlowService _levelFlow;
        private IPopupService _popupService;
        private int _displayedLevel = -1;
        private int _displayedCredits = -1;

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            IHintService hintService,
            ILevelFlowService levelFlow,
            IPopupService popupService,
            HomeScreenView home)
        {
            _levelService = levelService;
            _paintService = paintService;
            _hintService = hintService;
            _levelFlow = levelFlow;
            _popupService = popupService;
            _home = home;

            _levelService.OnLevelStarted += HandleLevelStarted;
            _paintService.OnCellPainted += HandleCellPainted;
            _hintService.OnHintAvailabilityChanged += SetHintAvailable;
            _hintService.OnCreditsChanged += SetHintCredits;

            // Nghe thẳng, không qua presenter. Popup mở bằng SỰ KIỆN thì mới cần một
            // object luôn sống làm cái tai — vì chính popup chưa tồn tại trước lần mở
            // đầu tiên. Ở đây sự kiện là hệ quả trực tiếp của việc bấm cái nút mà HUD
            // đang giữ, nên HUD đã là object luôn sống đó rồi.
            _hintService.OnCreditsExhausted += HandleCreditsExhausted;
            _levelFlow.OnLevelCleared += HandleLevelCleared;

            SetLevel(_levelService.CurrentLevel);
            SetHintCredits(_hintService.RemainingCredits);

            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettingsClicked);

            if (_resetButton != null) _resetButton.onClick.AddListener(HandleResetClicked);

            RefreshResetAvailable();

            // Ẩn cho tới khi có màn được nạp. Lúc mới vào game màn hình chờ đang che,
            // mà HUD thì chưa có gì để hiện ngoài chữ "Level 0".
            SetVisible(false);

            if (_hintButton == null) return;

            _hintButton.onClick.AddListener(HandleHintClicked);
            SetHintAvailable(_hintService.CanUseHint);
        }

        /// Huỷ đăng ký để tránh gọi vào object đã bị huỷ.
        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
            if (_paintService != null) _paintService.OnCellPainted -= HandleCellPainted;
            if (_hintService != null)
            {
                _hintService.OnHintAvailabilityChanged -= SetHintAvailable;
                _hintService.OnCreditsChanged -= SetHintCredits;
                _hintService.OnCreditsExhausted -= HandleCreditsExhausted;
            }

            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
            if (_hintButton != null) _hintButton.onClick.RemoveListener(HandleHintClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        private void HandleLevelStarted(int levelId)
        {
            SetVisible(true);
            SetLevel(levelId);

            RefreshResetAvailable();
        }

        /// Ô đầu tiên được tô là lúc nút Tô lại có việc để làm. Nghe từng ô nghe thì phí,
        /// nhưng thân hàm chỉ là một phép gán mà Unity tự bỏ qua khi giá trị không đổi.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex) => RefreshResetAvailable();

        private void HandleLevelCleared() => SetVisible(false);

        /// Ẩn bằng SetActive chứ không đổi alpha: HUD tắt hẳn thì nút gợi ý cũng không
        /// còn nhận được cú chạm nào, khỏi phải nhớ khoá riêng từng nút.
        ///
        /// Ẩn chính object này vẫn an toàn dù handler nằm trên nó: sự kiện C# giữ tham
        /// chiếu tới instance, nên hàm vẫn chạy khi GameObject đang tắt — đó là cách
        /// HUD tự bật lại được ở màn sau.
        /// public vì popup Cài đặt phải ẩn HUD trước khi mở Home.
        public void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private void HandleHintClicked() => _hintService.UseHint();

        /// Xoá tiến độ tô của màn đang chơi rồi nạp lại. Gameplay lo phần còn lại — HUD
        /// không biết bản lưu nằm ở đâu, cũng không biết bảng được dựng lại thế nào.
        private void HandleResetClicked() => _paintService.ResetCurrentLevel();

        private void RefreshResetAvailable()
        {
            if (_resetButton == null || _paintService == null) return;

            // Selectable.interactable tự bỏ qua khi giá trị không đổi, nên gán thẳng mỗi
            // lần là đủ — không cần nhớ giá trị cũ ở đây.
            _resetButton.interactable = _paintService.CanReset;
        }

        private void HandleHomeClicked()
        {
            SetVisible(false);

            if (_home != null) _home.Show();
        }

        /// Chỉ mở popup. Đường về Home nằm trong chính popup đó, và cũng chính nó lo
        /// việc ẩn HUD — HUD không cần biết Home tồn tại.
        private void HandleSettingsClicked() => _popupService.Show(PopupKey.Settings);

        /// Chưa chọn màu, hoặc màu đang chọn đã tô hết, thì nút xám đi — bấm vào không
        /// có gì xảy ra mà người chơi lại tưởng game đứng.
        private void HandleCreditsExhausted() => _popupService.Show(PopupKey.HintMove);

        /// Chỉ SetText khi con số thật sự đổi — cùng lý do đã ghi ở SetLevel.
        private void SetHintCredits(int remaining)
        {
            if (_hintCreditsBadge != null) _hintCreditsBadge.SetActive(remaining > 0);

            if (_hintCreditsText == null) return;
            if (remaining == _displayedCredits) return;

            _displayedCredits = remaining;
            _hintCreditsText.SetText("{0}", remaining);
        }

        private void SetHintAvailable(bool available)
        {
            if (_hintButton == null) return;

            _hintButton.interactable = available;
        }

        private void SetLevel(int level)
        {
            if (level == _displayedLevel) return;

            _displayedLevel = level;
        }
    }
}
