using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Data;
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
        [Tooltip("Số tiền đang có. Để trống thì HUD không hiện tiền.\n\n" +
                 "PHẢI gán ô này thì con số mới sống: không có nó, cái nhãn trong scene cứ " +
                 "đứng nguyên ở chuỗi bạn gõ lúc dựng giao diện, và nó trông y hệt một con " +
                 "số thật — chỉ là không bao giờ đổi.")]
        [SerializeField] private Text _coinsText;

        [Tooltip("Nút gợi ý. Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _hintButton;

        [Tooltip("Số lượt gợi ý miễn phí còn lại. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _hintCreditsText;

        [Tooltip("Huy hiệu bọc con số. Tự ẩn khi hết lượt — lúc đó nút chuyển sang mời " +
                 "xem quảng cáo, mà một số 0 nằm cạnh lời mời chỉ gây nhiễu.")]
        [SerializeField] private GameObject _hintCreditsBadge;

        [Header("Booster tô tự do")]
        [Tooltip("Nút bật booster: trong ít giây, MỌI ô chưa tô đều hiện dấu gợi ý và " +
                 "chạm vào ô nào cũng tô được ô đó (ra đúng màu của ô, không phải màu " +
                 "đang chọn).\n\n" +
                 "Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _freePaintButton;

        [Tooltip("Số lượt tô tự do còn lại. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _freePaintCreditsText;

        [Tooltip("Huy hiệu bọc con số. Tự ẩn khi hết lượt — cùng lý do như huy hiệu của " +
                 "nút gợi ý.")]
        [SerializeField] private GameObject _freePaintCreditsBadge;

        [Tooltip("Object bọc CẢ khung đồng hồ — kéo fr_time vào đây. Chỉ mình nó được " +
                 "bật/tắt theo booster.\n\n" +
                 "Để trống thì bật/tắt riêng phần chữ và phần vòng chạy như bản cũ. Cách " +
                 "đó vẫn chạy, chỉ là mọi thứ khác trong khung — nền, viền, icon — ở lại " +
                 "trên màn hình sau khi booster đã tắt.")]
        [SerializeField] private GameObject _freePaintTimerRoot;

        [Tooltip("Đồng hồ đếm ngược, chỉ hiện trong lúc booster chạy. Ghi dạng 00:SS, " +
                 "thêm số 0 đằng trước khi còn dưới 10 giây. Để trống thì không hiện.")]
        [SerializeField] private Text _freePaintTimerText;

        [Tooltip("Vòng/thanh chạy vơi dần theo thời gian còn lại, thang 0..1. Image phải " +
                 "để Image Type = Filled. Để trống thì bỏ qua.")]
        [SerializeField] private Image _freePaintTimerFill;

        [Header("Booster tô hết màu")]
        [Tooltip("Nút bật booster: tô nốt MỌI ô còn lại của màu đang chọn.\n\n" +
                 "Chưa chọn màu mà bấm thì nó hiện lời nhắc chọn màu và KHÔNG trừ lượt.\n\n" +
                 "Để trống thì HUD chạy bình thường, chỉ là không có nút.")]
        [SerializeField] private Button _fillColorButton;

        [Tooltip("Số lượt tô hết màu còn lại. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _fillColorCreditsText;

        [Tooltip("Huy hiệu bọc con số. Tự ẩn khi hết lượt — cùng lý do như huy hiệu của " +
                 "nút gợi ý.")]
        [SerializeField] private GameObject _fillColorCreditsBadge;

        [Tooltip("Vòng/thanh chạy đầy dần theo tiến độ đợt tô, thang 0..1. Image phải để " +
                 "Image Type = Filled. Chỉ hiện trong lúc đang tô. Để trống thì bỏ qua.")]
        [SerializeField] private Image _fillColorProgressFill;

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

        [Header("Mở khoá booster")]
        [Tooltip("Bảng mốc mở khoá của từng booster. Để trống thì mọi booster mở sẵn từ " +
                 "màn 1 — HUD chạy đúng như trước khi có phần này.")]
        [SerializeField] private BoosterUnlockConfig _boosterUnlock;

        [Tooltip("Phần mặt ĐÃ MỞ KHOÁ của nút gợi ý — icon, huy hiệu, những gì người " +
                 "chơi thấy khi booster dùng được. Tự ẩn khi chưa tới mốc, để lộ mặt khoá " +
                 "nằm dưới (ảnh nền của chính cái nút).\n\n" +
                 "Bật sẵn trong prefab: nó là trạng thái thường, còn khoá mới là ngoại lệ.\n\n" +
                 "Để trống thì nút vẫn bị khoá đúng, chỉ là trông y hệt lúc mở — người " +
                 "chơi gặp một cái nút bấm không ăn và không biết vì sao.")]
        [SerializeField] private GameObject _hintUnlockRoot;

        [Tooltip("Chữ ghi màn mở khoá, ví dụ \"5\". Ngược với ô trên: object này chỉ HIỆN " +
                 "khi đang khoá. Để trống thì không hiện số.")]
        [SerializeField] private Text _hintLockLevelText;

        [Tooltip("Phần mặt đã mở khoá của nút tô tự do. Cùng quy ước với nút gợi ý.")]
        [SerializeField] private GameObject _freePaintUnlockRoot;

        [SerializeField] private Text _freePaintLockLevelText;

        [Tooltip("Phần mặt đã mở khoá của nút tô hết màu. Cùng quy ước với nút gợi ý.")]
        [SerializeField] private GameObject _fillColorUnlockRoot;

        [SerializeField] private Text _fillColorLockLevelText;

        [Tooltip("Khuôn chữ ghi màn mở khoá. {0} là số màn.\n\n" +
                 "Để trống thì chỉ ghi trần con số — hợp khi trong ảnh ổ khoá đã có sẵn " +
                 "chữ \"Level\".")]
        [SerializeField] private string _lockLevelFormat = "Level {0}";

        [Tooltip("Object bị ẩn khi thắng màn. Để TRỐNG thì ẩn chính object này — cách " +
                 "đó vẫn chạy đúng, chỉ là không tách được phần nào của HUD ở lại.")]
        [SerializeField] private GameObject _content;
        private HomeScreenView _home;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private IHintService _hintService;
        private IFreePaintService _freePaintService;
        private IFillColorService _fillColorService;
        private PlayerWallet _wallet;
        private ILevelFlowService _levelFlow;
        private IPopupService _popupService;
        private ISoundService _sound;
        private PlayerProgress _progress;

        /// Ba booster đang mở khoá hay chưa, tính lại mỗi lần vào màn.
        ///
        /// Giữ lại thay vì hỏi config mỗi lần: SetHintAvailable và hai hàm anh em của nó
        /// chạy theo sự kiện của service, có thể nổ nhiều lần trong một màn, và cả ba đều
        /// phải AND với trạng thái khoá.
        private bool _hintUnlocked = true;
        private bool _freePaintUnlocked = true;
        private bool _fillColorUnlocked = true;

        private int _displayedLevel = -1;
        private int _displayedCredits = -1;
        private int _displayedFreePaintCredits = -1;
        private int _displayedFillColorCredits = -1;

        /// Số tiền đang hiện trên màn. Chỉ đổi chữ khi giá trị thật sự khác — cùng lý do
        /// đã ghi ở HomeScreenView.SetCoins.
        private int _displayedCoins = -1;

        /// Số giây nguyên đã ghi ra lần gần nhất. Đồng hồ chỉ hiện tới giây, nên đổi chữ
        /// mỗi frame là 59 lần SetText thừa cho mỗi giây thật — mà SetText thì sinh rác GC.
        private int _displayedFreePaintSeconds = -1;

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            IHintService hintService,
            IFreePaintService freePaintService,
            IFillColorService fillColorService,
            ILevelFlowService levelFlow,
            IPopupService popupService,
            PlayerWallet wallet,
            HomeScreenView home,
            ISoundService sound,
            PlayerProgress progress)
        {
            _progress = progress;

            _levelService = levelService;
            _paintService = paintService;
            _hintService = hintService;
            _freePaintService = freePaintService;
            _fillColorService = fillColorService;
            _levelFlow = levelFlow;
            _popupService = popupService;
            _wallet = wallet;
            _home = home;
            _sound = sound;

            // TRƯỚC mọi lời gọi SetXAvailable ở dưới: ba hàm đó đọc cờ khoá, mà cờ mặc
            // định là "đã mở". Chạy sau thì nút khoá vẫn bấm được cho tới lần vào màn kế.
            RefreshBoosterLocks();

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

            if (_freePaintService != null)
            {
                _freePaintService.OnAvailabilityChanged += SetFreePaintAvailable;
                _freePaintService.OnCreditsChanged += SetFreePaintCredits;
                _freePaintService.OnCreditsExhausted += HandleFreePaintExhausted;
                _freePaintService.OnActiveChanged += HandleFreePaintActiveChanged;
            }

            if (_fillColorService != null)
            {
                _fillColorService.OnAvailabilityChanged += SetFillColorAvailable;
                _fillColorService.OnCreditsChanged += SetFillColorCredits;
                _fillColorService.OnCreditsExhausted += HandleFillColorExhausted;
                _fillColorService.OnFillingChanged += HandleFillingChanged;
            }

            // Nghe ví TRƯỚC rồi đọc giá trị hiện tại, không phải ngược lại: giữa hai lời
            // gọi đó vẫn có thể có một cú cộng tiền, và cú đó sẽ rơi vào khoảng trống.
            if (_wallet != null)
            {
                _wallet.OnCoinsChanged += SetCoins;
                SetCoins(_wallet.Coins);
            }

            SetLevel(_levelService.CurrentLevel);
            SetHintCredits(_hintService.RemainingCredits);

            if (_freePaintButton != null) _freePaintButton.onClick.AddListener(HandleFreePaintClicked);

            if (_freePaintService != null)
            {
                SetFreePaintCredits(_freePaintService.RemainingCredits);
                SetFreePaintAvailable(_freePaintService.CanUse);
                HandleFreePaintActiveChanged(_freePaintService.IsActive);
            }

            if (_fillColorButton != null) _fillColorButton.onClick.AddListener(HandleFillColorClicked);

            if (_fillColorService != null)
            {
                SetFillColorCredits(_fillColorService.RemainingCredits);
                SetFillColorAvailable(_fillColorService.CanUse);
                HandleFillingChanged(_fillColorService.IsFilling);
            }

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

            if (_freePaintService != null)
            {
                _freePaintService.OnAvailabilityChanged -= SetFreePaintAvailable;
                _freePaintService.OnCreditsChanged -= SetFreePaintCredits;
                _freePaintService.OnCreditsExhausted -= HandleFreePaintExhausted;
                _freePaintService.OnActiveChanged -= HandleFreePaintActiveChanged;
            }

            if (_fillColorService != null)
            {
                _fillColorService.OnAvailabilityChanged -= SetFillColorAvailable;
                _fillColorService.OnCreditsChanged -= SetFillColorCredits;
                _fillColorService.OnCreditsExhausted -= HandleFillColorExhausted;
                _fillColorService.OnFillingChanged -= HandleFillingChanged;
            }

            if (_wallet != null) _wallet.OnCoinsChanged -= SetCoins;

            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
            if (_fillColorButton != null) _fillColorButton.onClick.RemoveListener(HandleFillColorClicked);
            if (_freePaintButton != null) _freePaintButton.onClick.RemoveListener(HandleFreePaintClicked);
            if (_hintButton != null) _hintButton.onClick.RemoveListener(HandleHintClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        private void HandleLevelStarted(int levelId)
        {
            SetVisible(true);
            SetLevel(levelId);

            // Tiến trình chỉ nhích lên giữa hai màn, nên đây là chỗ duy nhất cần soi lại.
            RefreshBoosterLocks();

            RefreshResetAvailable();
        }

        /// Đọc lại mốc mở khoá rồi dựng lại cả ba nút.
        ///
        /// So với PlayerProgress.Level — TIẾN TRÌNH CAO NHẤT — chứ không phải màn đang
        /// chơi. Chơi lại màn 1 sau khi đã tới màn 20 thì booster vẫn còn đó.
        private void RefreshBoosterLocks()
        {
            // Chưa gán bảng thì mọi thứ mở sẵn, y như trước khi có phần này. Không cảnh
            // báo: một game không khoá booster nào là cấu hình hợp lệ.
            var level = _progress != null ? _progress.Level : int.MaxValue;

            _hintUnlocked = _boosterUnlock == null
                            || _boosterUnlock.IsUnlocked(CreditPoolKind.Hint, level);
            _freePaintUnlocked = _boosterUnlock == null
                                 || _boosterUnlock.IsUnlocked(CreditPoolKind.FreePaint, level);
            _fillColorUnlocked = _boosterUnlock == null
                                 || _boosterUnlock.IsUnlocked(CreditPoolKind.FillColor, level);

            ApplyLockVisual(CreditPoolKind.Hint, _hintUnlocked, _hintUnlockRoot, _hintLockLevelText);
            ApplyLockVisual(CreditPoolKind.FreePaint, _freePaintUnlocked,
                _freePaintUnlockRoot, _freePaintLockLevelText);
            ApplyLockVisual(CreditPoolKind.FillColor, _fillColorUnlocked,
                _fillColorUnlockRoot, _fillColorLockLevelText);

            // Dựng lại trạng thái bấm được từ nguồn thật. Không tự đặt interactable ở đây:
            // nút còn phụ thuộc vào việc service có cho dùng hay không, và chỉ service mới
            // biết điều đó.
            if (_hintService != null) SetHintAvailable(_hintService.CanUseHint);
            if (_freePaintService != null) SetFreePaintAvailable(_freePaintService.CanUse);
            if (_fillColorService != null) SetFillColorAvailable(_fillColorService.CanUse);

            // Huy hiệu số lượt cũng phải theo: một con số 3 nằm cạnh ổ khoá chỉ gây nhiễu.
            // Cùng lý do đã ghi ở chỗ huy hiệu tự ẩn khi hết lượt.
            if (_hintCreditsBadge != null && !_hintUnlocked) _hintCreditsBadge.SetActive(false);
            if (_freePaintCreditsBadge != null && !_freePaintUnlocked) _freePaintCreditsBadge.SetActive(false);
            if (_fillColorCreditsBadge != null && !_fillColorUnlocked) _fillColorCreditsBadge.SetActive(false);
        }

        /// Hai object ngược chiều nhau: phần mở khoá hiện khi ĐÃ mở, chữ số màn hiện khi
        /// CHƯA. Mặt khoá không có ô riêng — nó là thứ nằm sẵn dưới phần mở khoá, lộ ra
        /// khi phần đó tắt đi.
        private void ApplyLockVisual(CreditPoolKind booster, bool unlocked, GameObject unlockRoot, Text levelText)
        {
            if (unlockRoot != null) unlockRoot.SetActive(unlocked);

            if (levelText == null) return;

            levelText.gameObject.SetActive(!unlocked);

            // Chỉ ghi chữ khi đang khoá. Ghi cả lúc đã mở là dựng lưới chữ cho một dòng
            // nằm trong object vừa tắt.
            //
            // string.Format sinh rác, nhưng hàm này chạy đúng một lần mỗi lần vào màn —
            // không phải chỗ đáng đi vòng để né.
            if (unlocked || _boosterUnlock == null) return;

            var level = _boosterUnlock.UnlockLevelFor(booster);

            levelText.text = string.IsNullOrEmpty(_lockLevelFormat)
                ? level.ToString()
                : string.Format(_lockLevelFormat, level);
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

        /// Tiếng kêu ở cú BẤM, không phải ở lúc booster thật sự chạy.
        ///
        /// Bấm khi hết lượt thì booster không chạy mà mở popup mời mua — người chơi vẫn
        /// phải nghe thấy cú bấm của mình có tới nơi. Im lặng ở đó đọc ra là nút hỏng.
        private void HandleHintClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.Hint);

            _hintService.UseHint();
        }

        private void HandleFreePaintClicked()
        {
            if (_freePaintService == null) return;

            if (_sound != null) _sound.Play(SoundKey.FreePaint);

            _freePaintService.Use();
        }

        private void HandleFreePaintExhausted() => _popupService.Show(PopupKey.FreePaint);

        /// Bật/tắt phần đồng hồ đếm ngược.
        ///
        /// Không đụng tới nút. Nút giờ KHÔNG xám trong lúc booster chạy — cú bấm lặp
        /// lại bị FreePaintController.Use bỏ qua mà không trừ lượt. Mọi thay đổi trạng
        /// thái nút vẫn chỉ đi một đường: CanUse → OnAvailabilityChanged →
        /// SetFreePaintAvailable. Tắt tay thêm ở đây là dựng ra hai nguồn sự thật cho
        /// cùng một cái nút.
        private void HandleFreePaintActiveChanged(bool active)
        {
            if (_freePaintTimerRoot != null)
            {
                _freePaintTimerRoot.SetActive(active);
            }
            else
            {
                if (_freePaintTimerText != null) _freePaintTimerText.gameObject.SetActive(active);
                if (_freePaintTimerFill != null) _freePaintTimerFill.gameObject.SetActive(active);
            }

            _displayedFreePaintSeconds = -1;

            if (active) TickFreePaintTimer();
        }

        private void HandleFillColorClicked()
        {
            if (_fillColorService == null) return;

            if (_sound != null) _sound.Play(SoundKey.MagicWand);

            _fillColorService.Use();
        }

        private void HandleFillColorExhausted() => _popupService.Show(PopupKey.FillColor);

        /// Bật/tắt vòng tiến độ của đợt tô. Không đụng tới nút — nó tự xám qua CanUse,
        /// cùng đường như nút booster kia.
        private void HandleFillingChanged(bool filling)
        {
            if (_fillColorProgressFill == null) return;

            _fillColorProgressFill.gameObject.SetActive(filling);

            if (filling) _fillColorProgressFill.fillAmount = 0f;
        }

        /// Chỉ chạy khi có việc: booster đếm ngược đang bật, hoặc đợt tô đang chạy.
        private void Update()
        {
            if (_freePaintService != null && _freePaintService.IsActive) TickFreePaintTimer();

            if (_fillColorProgressFill != null && _fillColorService != null && _fillColorService.IsFilling)
            {
                _fillColorProgressFill.fillAmount = Mathf.Clamp01(_fillColorService.FillProgress);
            }
        }

        private void TickFreePaintTimer()
        {
            var remaining = _freePaintService.RemainingSeconds;

            if (_freePaintTimerFill != null)
            {
                _freePaintTimerFill.fillAmount = Mathf.Clamp01(remaining / _freePaintService.DurationSeconds);
            }

            if (_freePaintTimerText == null) return;

            // Làm tròn LÊN: còn 0.4 giây mà hiện số 0 thì đồng hồ đứng ở 0 gần một giây
            // trước khi tắt, đọc ra như bị treo.
            var seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            if (seconds == _displayedFreePaintSeconds) return;

            _displayedFreePaintSeconds = seconds;

            // Đệm số 0 cho phần giây một chữ số: "00:5" đọc ra là một con số bị hụt, còn
            // "00:05" mới ra dáng đồng hồ. Ghép thêm một ký tự chứ không gọi ToString("00")
            // — cùng một string sinh ra, nhưng cách này không phải nuôi một chuỗi định
            // dạng mà người đọc sau phải dịch ngược.
            _freePaintTimerText.text = seconds < 10
                ? "00:0" + seconds
                : "00:" + seconds;
        }

        private void SetFreePaintCredits(int remaining)
        {
            if (_freePaintCreditsBadge != null) _freePaintCreditsBadge.SetActive(remaining > 0 && _freePaintUnlocked);

            if (_freePaintCreditsText == null) return;
            if (remaining == _displayedFreePaintCredits) return;

            _displayedFreePaintCredits = remaining;
            _freePaintCreditsText.SetText("{0}", remaining);
        }

        private void SetFreePaintAvailable(bool available)
        {
            if (_freePaintButton == null) return;

            _freePaintButton.interactable = available && _freePaintUnlocked;
        }

        private void SetFillColorCredits(int remaining)
        {
            if (_fillColorCreditsBadge != null) _fillColorCreditsBadge.SetActive(remaining > 0 && _fillColorUnlocked);

            if (_fillColorCreditsText == null) return;
            if (remaining == _displayedFillColorCredits) return;

            _displayedFillColorCredits = remaining;
            _fillColorCreditsText.SetText("{0}", remaining);
        }

        private void SetFillColorAvailable(bool available)
        {
            if (_fillColorButton == null) return;

            _fillColorButton.interactable = available && _fillColorUnlocked;
        }

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
        /// Tiếng ButtonClick chứ không phải Direction: bánh răng chỉ MỞ một bảng nằm đè
        /// lên, người chơi vẫn đang ở trong màn. Direction để dành cho những nút thật sự
        /// đưa họ đi chỗ khác — Play, Home, Continue.
        private void HandleSettingsClicked()
        {
            if (_sound != null) _sound.Play(SoundKey.ButtonClick);

            _popupService.Show(PopupKey.Settings);
        }

        /// Bấm nút mà hết lượt: mở popup mời thêm lượt. Một dòng cho mỗi booster.
        ///
        /// Key nằm THẲNG trong code chứ không phải một ô PopupKey ngoài Inspector, và ba
        /// cái giống hệt nhau là có chủ ý: quan hệ "nút này ↔ popup này" là cố định theo
        /// thiết kế, không phải một thứ để chỉnh. Đưa ra Inspector thì nó thành ba ô có
        /// thể để trống hoặc gán chéo nhau, mà cả ba trạng thái sai đó đều chỉ lộ ra đúng
        /// lúc người chơi hết lượt — tức là lúc hiếm nhất trong quá trình test.
        ///
        /// Đổi lại: MỖI key ở đây BẮT BUỘC phải có prefab khai trong PopupConfig. Thiếu thì
        /// PopupManager.Show bắn LogError chứ không im lặng — đó cũng là điều mong muốn,
        /// vì một cái nút hết lượt mà không mở gì cả thì người chơi tưởng game đứng.
        private void HandleCreditsExhausted() => _popupService.Show(PopupKey.HintMove);

        /// Chỉ SetText khi con số thật sự đổi — cùng lý do đã ghi ở SetLevel.
        private void SetHintCredits(int remaining)
        {
            if (_hintCreditsBadge != null) _hintCreditsBadge.SetActive(remaining > 0 && _hintUnlocked);

            if (_hintCreditsText == null) return;
            if (remaining == _displayedCredits) return;

            _displayedCredits = remaining;
            _hintCreditsText.SetText("{0}", remaining);
        }

        /// Booster đang khoá thì nút TẮT, dù service có nói gì.
        ///
        /// Phải chặn ngay tại đây chứ không chỉ ở lúc đổi màn: hàm này chạy theo sự kiện
        /// của service, và một tín hiệu "dùng được" nổ ra giữa màn sẽ lặng lẽ bật lại
        /// đúng cái nút vừa bị khoá.
        private void SetHintAvailable(bool available)
        {
            if (_hintButton == null) return;

            _hintButton.interactable = available && _hintUnlocked;
        }

        /// Chỉ đổi chữ khi con số thật sự khác — đổi text là dựng lại lưới chữ, mà sự
        /// kiện tiền có thể nổ nhiều lần liên tiếp lúc coin bay ở popup thắng màn.
        private void SetCoins(int coins)
        {
            if (_coinsText == null) return;
            if (coins == _displayedCoins) return;

            _displayedCoins = coins;
            _coinsText.text = coins.ToString();
        }

        private void SetLevel(int level)
        {
            if (level == _displayedLevel) return;

            _displayedLevel = level;
        }
    }
}
