using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Board;
using DG.Tweening;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Hướng dẫn cho người chơi mới: một ngón tay chỉ vào ô màu đầu tiên trên thanh chọn,
    /// kèm một bảng nhắc.
    ///
    /// Hiện đúng MỘT LẦN trong đời máy: vào màn hướng dẫn, bảng chưa tô ô nào, và người
    /// chơi chưa bao giờ tô được ô nào. Tắt ngay khi chọn màu đầu tiên.
    ///
    /// Điều kiện thứ ba là thứ mới, và nó không suy ra được từ hai cái đầu: "bảng này
    /// chưa tô ô nào" với "người này chưa bao giờ tô" cho ra cùng một bảng trống, nhưng
    /// nói hai chuyện khác hẳn nhau. Thiếu nó thì hướng dẫn hiện lại mỗi lần vào lại màn
    /// 1 chưa tô — kể cả ngay sau khi người chơi bấm nút Tô lại, tức đúng lúc người ta đã
    /// thạo tới mức chủ động chơi lại.
    ///
    /// KHÔNG dùng PopupService. Popup có nền chặn bấm, mà thứ ngón tay đang chỉ vào lại
    /// chính là cái người chơi phải bấm — người chơi sẽ chạm vào và không có gì xảy ra.
    /// Lớp này chỉ là ảnh đè lên, phải để Raycast Target TẮT ở mọi ảnh con.
    public class TutorialOverlayView : MonoBehaviour
    {
        [Tooltip("Object chứa ngón tay và bảng nhắc. Bật/tắt cả cụm.\n\n" +
                 "PHẢI là một object CON, không được để trống và không được trỏ về chính " +
                 "object mang script này: tắt chính mình là mọi coroutine đang chạy bị " +
                 "huỷ và lần bật sau không khởi động lại được.")]
        [SerializeField] private GameObject _content;

        [Tooltip("Ảnh bàn tay. Nó được đặt vào vị trí ô màu đầu tiên mỗi lần hướng dẫn hiện.")]
        [SerializeField] private RectTransform _finger;

        [Tooltip("Ảnh bàn tay của NHỊP 2 — lúc ngón tay kéo vòng trên bàn chơi.\n\n" +
                 "Để trống thì nhịp 2 dùng lại chính ảnh ở ô trên, y như trước.\n\n" +
                 "Hai ô này KHÔNG bao giờ cùng hiện: lớp hướng dẫn tự bật cái đang tới " +
                 "lượt và tắt cái kia, nên cứ để cả hai bật sẵn trong scene cũng được.")]
        [SerializeField] private RectTransform _dragFinger;

        [Tooltip("NHỊP 2 — dịch ngón tay khỏi tâm ô trên bàn chơi.\n\n" +
                 "Có ô riêng vì hai nhịp đo từ hai thứ khác nhau: nhịp 1 nhắm vào một ô " +
                 "màu trên thanh dưới đáy màn hình, nhịp 2 nhắm vào một ô giữa bức tranh. " +
                 "Thêm nữa, ảnh bàn tay của nhịp 2 có thể nghiêng khác nên đầu ngón trỏ " +
                 "nằm ở chỗ khác trong khung ảnh.\n\n" +
                 "Mặc định trùng ô của nhịp 1, nên để yên cả hai thì không có gì đổi so " +
                 "với trước.")]
        [SerializeField] private Vector2 _dragFingerOffset = new(0f, -60f);

        [Tooltip("NHỊP 1 — dịch ngón tay khỏi tâm ô màu, tính bằng pixel của canvas. " +
                 "Thường cần dịch xuống dưới để đầu ngón trỏ chạm vào ô chứ không phải cả " +
                 "bàn tay đè lên nó.")]
        [SerializeField] private Vector2 _fingerOffset = new(0f, -60f);

        [Tooltip("Camera đang vẽ bàn chơi. ĐỂ TRỐNG cũng chạy: lúc đó lấy Camera.main.\n\n" +
                 "Chỉ dùng ở nhịp hai, để đổi toạ độ ô trên bảng sang chỗ đứng của ngón tay " +
                 "trên canvas.")]
        [SerializeField] private Camera _worldCamera;

        [Header("Nhịp 2 — kéo qua các ô gợi ý")]
        [Tooltip("Cạnh của vòng ngón tay đi, tính bằng Ô: xuống chừng này ô, sang chừng " +
                 "này ô, lên, rồi ngược lại về chỗ cũ.\n\n" +
                 "Nhỏ thôi. Vòng càng rộng thì càng lâu mới quay lại chỗ bắt đầu, mà thứ " +
                 "cần nói chỉ là 'kéo tay qua mấy ô này'.")]
        [Range(1, 4)]
        [SerializeField] private int _loopCells = 2;

        [Tooltip("Chờ ngần này giây sau khi camera bắt đầu bay rồi mới dựng đường đi và " +
                 "cho ngón tay hiện lại.\n\n" +
                 "PHẢI dài hơn Focus Duration của BoardCamera: đường đi được chốt bằng toạ " +
                 "độ màn hình, đọc lúc camera còn đang bay là chốt vào một khung hình sắp " +
                 "biến mất.")]
        [SerializeField] private float _paintStageDelay = 0.85f;

        [Tooltip("Thời gian đi TRỌN một vòng, tính bằng giây.")]
        [SerializeField] private float _loopSeconds = 2.2f;

        [Header("Nhịp 3 — mời chọn màu tiếp")]
        [Tooltip("Chờ ngần này giây sau khi ô màu vừa xong BIẾN MẤT khỏi thanh, rồi mới " +
                 "thu camera về.\n\n" +
                 "Một nhịp thở giữa hai màn diễn: thanh màu vừa khép lại xong, thu camera " +
                 "ngay lúc đó là hai chuyển động chồng lên nhau.")]
        [SerializeField] private float _finalStageDelay = 0.6f;

        [Tooltip("Thời gian camera thu về toàn cảnh.")]
        [SerializeField] private float _finalZoomSeconds = 0.5f;

        [Header("Điều kiện hiện")]
        [Tooltip("Màn nào thì hiện hướng dẫn.")]
        [SerializeField] private int _tutorialLevelId = 1;

        [Tooltip("Chờ ngần này giây sau khi vào màn rồi mới hiện, để người chơi kịp nhìn " +
                 "bức tranh trước đã.")]
        [SerializeField] private float _delaySeconds = 0.6f;

        [Tooltip("In ra Console lý do hướng dẫn hiện hoặc không hiện, mỗi lần vào màn. " +
                 "Bật khi thấy 'vào màn 1 mà chẳng có gì'.")]
        [SerializeField] private bool _logDecision;

        [Header("Nhịp gõ của ngón tay")]
        [Tooltip("Ngón tay xuất phát cao hơn ô màu bao nhiêu pixel rồi hạ xuống chạm vào " +
                 "nó. 0 là đứng yên.")]
        [SerializeField] private float _tapTravel = 80f;

        [Tooltip("Một nhịp gõ trọn vẹn mất bao lâu: hạ xuống, dừng một nhịp, rồi nhấc lên.")]
        [SerializeField] private float _tapSeconds = 1.1f;

        [Tooltip("Phần của nhịp dành cho lúc HẠ XUỐNG. Phần còn lại chia cho quãng dừng ở " +
                 "đáy và quãng nhấc lên.")]
        [Range(0.1f, 0.8f)]
        [SerializeField] private float _tapDownPortion = 0.35f;

        [Tooltip("Phần của nhịp dành cho quãng DỪNG ở đáy, ngay sau khi chạm. Không có " +
                 "quãng này thì cú chạm trôi tuột và mắt không kịp đọc ra là 'bấm vào đây'.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _tapHoldPortion = 0.18f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private ColorPaletteBar _paletteBar;
        private TutorialState _tutorialState;
        private IHintService _hintService;
        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isShowing;
        private bool _isDisabled;
        private bool _hasWarnedInactive;

        /// Màu mà nhịp 2 đã dạy, đang chờ nó được tô xong hẳn. -1 là không chờ ai.
        ///
        /// Giữ CHỈ SỐ MÀU chứ không phải một cờ bool: giữa lúc chờ, người chơi hoàn toàn
        /// có thể chọn màu khác rồi tô xong màu đó trước. Một cờ bool sẽ nhận nhầm cú đó
        /// và mở nhịp 3 quá sớm.
        private int _awaitedColor = NoColor;

        private const int NoColor = -1;

        /// Đã quyết định là phải hiện, nhưng chưa chạy được vì object còn tắt.
        private bool _pendingShow;

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            ColorPaletteBar paletteBar,
            TutorialState tutorialState,
            IHintService hintService,
            BoardView boardView,
            BoardCamera boardCamera)
        {
            _levelService = levelService;
            _paintService = paintService;
            _paletteBar = paletteBar;
            _tutorialState = tutorialState;
            _hintService = hintService;
            _boardView = boardView;
            _boardCamera = boardCamera;

            if (_content == null || _content == gameObject)
            {
                _isDisabled = true;

                Debug.LogError($"{nameof(TutorialOverlayView)}: ô Content phải trỏ tới một " +
                               "object CON chứa ngón tay và bảng nhắc. Bỏ trống hoặc trỏ về " +
                               "chính object này thì việc tắt hướng dẫn sẽ huỷ luôn coroutine " +
                               "của chính nó. Hướng dẫn bị tắt.", this);
                return;
            }

            // Mốc mở nhịp 3: ô màu vừa xong biến hẳn khỏi thanh.
            if (_paletteBar != null) _paletteBar.OnSwatchRemoved += HandleSwatchRemoved;

            // Nghe OnBoardReady chứ không nghe OnLevelStarted: lúc màn bắt đầu thì thanh
            // màu chưa dựng xong, mà ngón tay cần biết ô màu đầu tiên đứng ở đâu.
            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;

            // KHÔNG đăng ký OnLevelStarted để tắt hướng dẫn. Nghe rất hợp lý, nhưng nó tự
            // bóp chết chính mình:
            //
            //   OnLevelStarted
            //     └─ PaintManager (đăng ký TRƯỚC)  ──► bắn OnBoardReady ngay trong handler
            //          └─ HandleBoardReady  →  StartCoroutine(ShowRoutine)
            //     └─ HandleLevelStarted  →  Hide()  →  StopAllCoroutines()
            //
            // Tức là hướng dẫn được tạo rồi bị huỷ trong cùng một frame, và Console vẫn in
            // "→ HIỆN" nên nhìn vào log thì mọi thứ có vẻ đúng.
            //
            // Không cần nó thật: HandleBoardReady đã Hide() ngay ở dòng đầu, mà hai sự kiện
            // này luôn nổ trong cùng một frame nên không có khoảng hở nào để hướng dẫn của
            // màn cũ kịp lọt sang màn mới.

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_paletteBar != null) _paletteBar.OnSwatchRemoved -= HandleSwatchRemoved;

            if (_paintService != null)
            {
                _paintService.OnBoardReady -= HandleBoardReady;
                _paintService.OnColorSelected -= HandleColorSelected;
                _paintService.OnCellPainted -= HandleCellPainted;
            }
        }

        private void HandleBoardReady()
        {
            Hide();

            // Bỏ lời hẹn còn treo: màn mới thì ô màu cũ không còn nghĩa gì, mà một cú
            // chờ sót lại sẽ mở nhịp 3 giữa một màn chơi chẳng liên quan.
            _awaitedColor = NoColor;

            if (_isDisabled) return;

            var loadedLevel = LoadedLevelId();
            var untouched = _paintService.IsUntouched;
            var experienced = IsExperienced();

            if (_logDecision)
            {
                var show = loadedLevel == _tutorialLevelId && untouched && !experienced;

                Debug.Log($"[Tutorial] màn đang nạp {loadedLevel} (cần {_tutorialLevelId}), " +
                          $"chưa tô ô nào: {untouched}, đã từng tô: {experienced} → " +
                          $"{(show ? "HIỆN" : "bỏ qua")}", this);
            }

            if (loadedLevel != _tutorialLevelId) return;
            if (!untouched) return;
            if (experienced) return;

            _pendingShow = true;
            TryStartShow();
        }

        /// Object đang tắt thì GIỮ LẠI ý định, chờ nó bật lên rồi mới chạy.
        ///
        /// Cần thế vì OnBoardReady nổ lúc nạp màn, mà lúc đó canvas HUD có thể còn đang
        /// tắt sau màn hình chờ. Bỏ luôn ý định ở đây thì hướng dẫn mất hẳn, và nguyên
        /// nhân lại nằm ở một object khác hẳn nên rất khó lần.
        private void TryStartShow()
        {
            if (!_pendingShow) return;

            // Coroutine không chạy được trên GameObject đang tắt, và Unity ném exception
            // chứ không im lặng.
            if (!gameObject.activeInHierarchy)
            {
                WarnInactiveOnce();
                return;
            }

            _pendingShow = false;
            StartCoroutine(ShowRoutine());
        }

        private void OnEnable() => TryStartShow();

        /// Màn đang THẬT SỰ được nạp, không phải màn theo tiến trình.
        ///
        /// Từ khi Home cho chọn màn, hai con số này tách nhau: đang ở màn 5 mà chọn chơi
        /// lại màn 1 thì CurrentLevel vẫn trả về 5. Dùng CurrentLevel ở đây là hướng dẫn
        /// không bao giờ hiện lại được nữa sau khi người chơi qua màn 1.
        ///
        /// Đọc qua CurrentConfig chứ không nhớ lại tham số của OnLevelStarted: PaintManager
        /// đăng ký sự kiện đó TRƯỚC lớp này và bắn OnBoardReady ngay trong handler của nó,
        /// nên tới lúc hàm này chạy thì handler OnLevelStarted của chính lớp này còn chưa
        /// tới lượt. CurrentConfig thì đã được đặt xong từ trước khi sự kiện bắn ra.
        private int LoadedLevelId()
        {
            var config = _levelService.CurrentConfig;

            return config != null ? config.LevelId : _levelService.CurrentLevel;
        }

        /// Người chơi đã biết tô rồi hay chưa.
        ///
        /// Hai nguồn, và nguồn thứ hai là đường CỨU cho những máy đã cài từ trước: cờ
        /// has_painted_once mới có từ bản này, nên người chơi đang dở màn 20 vẫn đọc ra
        /// false. Màn hướng dẫn đã hoàn thành là bằng chứng không thể chối rằng họ đã tô,
        /// và nó có sẵn trong tiến trình từ lâu.
        ///
        /// Giữ luôn cả hai chứ không chỉ dùng nguồn thứ hai: người chơi mới bỏ dở màn 1
        /// giữa chừng thì IsCompleted vẫn false, mà họ thì đã tô rồi.
        private bool IsExperienced()
        {
            if (_tutorialState != null && _tutorialState.HasPaintedOnce) return true;

            return _levelService != null && _levelService.IsCompleted(_tutorialLevelId);
        }

        /// Ô đầu tiên trong đời được tô. MarkPainted tự bỏ qua từ lần thứ hai trở đi nên
        /// không cần huỷ đăng ký cho đúng lúc.
        ///
        /// Và đó cũng là lúc hướng dẫn xong việc: người chơi vừa tự tay làm đúng thứ ngón
        /// tay đang mời họ làm. Để nó kéo tiếp là dạy lại một điều họ vừa chứng minh là đã
        /// biết — và tệ hơn, nó khoá kéo/zoom thêm một quãng nữa mà chẳng để làm gì.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            _tutorialState?.MarkPainted();

            // Chỉ nhịp 2 mới bàn giao tiếp. Nhịp 3 cũng nghe sự kiện này — người chơi tô
            // tiếp trong lúc ngón tay còn đang mời — mà nó thì không có gì phải làm.
            if (_tutorialState == null || _tutorialState.Stage != TutorialStage.PaintCells) return;

            // Ghi lại màu đang dạy TRƯỚC khi ẩn: nhịp 3 chỉ mở khi đúng màu này tô xong
            // hẳn, còn người chơi thì giữa chừng có thể đổi sang màu khác.
            _awaitedColor = paletteIndex;

            // Hide dập vòng kéo đang chạy VÀ thả hết khoá (SetVisible(false) đưa nhịp về
            // None). Thả ngay từ đây là cố ý: từ giây này người chơi phải tự tô nốt cả
            // màu, mà tô nốt thì gần như chắc chắn cần kéo bảng đi chỗ khác.
            Hide();
        }

        /// Màu vừa được tô xong hẳn: cú loé đã diễn, ô màu đã biến khỏi thanh.
        ///
        /// Đây mới là lúc mở nhịp 3 — không phải lúc tô được ô đầu tiên. Người chơi còn
        /// đang giữa một màu dở thì lời mời "chọn màu tiếp đi" là mời họ bỏ dở việc đang
        /// làm, và cú thu camera đi kèm sẽ giật bay đúng chỗ họ đang tô.
        private void HandleSwatchRemoved(int paletteIndex)
        {
            if (_awaitedColor == NoColor || paletteIndex != _awaitedColor) return;

            _awaitedColor = NoColor;

            StartCoroutine(FinalStageRoutine());
        }

        /// Nhịp 3: thu camera về toàn cảnh rồi mời chọn màu tiếp — và KHÔNG khoá gì nữa.
        ///
        /// Thu camera trước, ngón tay sau. Ô màu đứng yên trên thanh nên ngón tay chỉ vào
        /// nó lúc nào cũng đúng chỗ, nhưng người chơi thì chỉ nhìn được MỘT thứ chuyển
        /// động một lúc — cho hai thứ chạy cùng lúc là để cú thu camera nuốt mất ngón tay.
        private IEnumerator FinalStageRoutine()
        {
            if (_finalStageDelay > 0f) yield return new WaitForSeconds(_finalStageDelay);

            _boardCamera?.ResetFraming(Mathf.Max(0f, _finalZoomSeconds));

            if (_finalZoomSeconds > 0f) yield return new WaitForSeconds(_finalZoomSeconds);

            var target = _paletteBar != null ? _paletteBar.TutorialSwatchRect : null;

            // Hết ô để chỉ thì thôi, không hiện ngón tay trỏ vào chỗ trống. Màu vừa tô
            // xong hẳn là ô đó đã biến khỏi thanh — chuyện bình thường ở màn ít màu.
            if (target == null) yield break;

            SetVisible(true);

            // ĐẶT NHỊP SAU KHI BẬT, và nhịp này không khoá gì: LocksInput trả false ở
            // PickNextColor, nên bảng, thanh màu và mọi ô màu đều mở lại từ giây này.
            _tutorialState?.SetStage(TutorialStage.PickNextColor);

            var finger = UseFinger(forDragStage: false);
            if (finger == null) yield break;

            finger.position = target.position;
            finger.anchoredPosition += _fingerOffset;

            yield return TapRoutine(finger, finger.anchoredPosition);
        }

        /// Chọn xong màu là hết hướng dẫn — và bàn giao ngay cho một cú gợi ý miễn phí.
        ///
        /// Ẩn TRƯỚC rồi mới bay: Hide gọi StopAllCoroutines, mà cú bay thì sống trong
        /// camera chứ không trong lớp này, nên nó không bị cắt. Làm ngược lại thì ngón tay
        /// còn gõ trên màn hình trong lúc camera đã lao đi — chỉ vào một ô màu không còn
        /// nằm ở đó nữa.
        ///
        /// Không đợi thêm nhịp nào: người chơi vừa bấm, và cú bay chính là phản hồi cho cú
        /// bấm đó. Chèn một quãng chờ vào giữa là để họ kịp nghĩ rằng không có gì xảy ra.
        ///
        /// Chỉ bay ở ĐÚNG màn hướng dẫn: hàm này chỉ được đăng ký một lần cho cả đời
        /// object, nên phải tự hỏi mình có đang hiện hay không — chọn màu ở màn 30 mà
        /// camera tự phóng sát thì đó là lỗi, không phải hướng dẫn.
        private void HandleColorSelected(int paletteIndex)
        {
            if (!_isShowing) return;

            // Nhịp 3 mời chọn màu, và người chơi vừa chọn — hết việc. Không bay đi đâu
            // nữa: cú bay của nhịp 2 là để chỉ chỗ tô đầu tiên, còn ở đây họ đã tô rồi và
            // đang tự đi tiếp. Giật camera lúc đó là giành lại quyền vừa trả cho họ.
            if (_tutorialState != null && _tutorialState.Stage == TutorialStage.PickNextColor)
            {
                Hide();
                return;
            }

            // Hide TRƯỚC, không phải SetVisible(false): Hide gọi luôn StopAllCoroutines,
            // dập nhịp gõ của ngón tay ở nhịp 1. Thiếu bước đó thì hai coroutine cùng ghi
            // vào anchoredPosition của cùng một ngón tay.
            Hide();

            if (_hintService == null || !_hintService.FocusHintWithoutSpending(out var cell))
            {
                return;
            }

            StartCoroutine(PaintStageRoutine(cell));
        }

        /// Nhịp 2: ngón tay hiện lại trên bàn chơi và kéo qua mấy ô đang chờ tô.
        ///
        /// Chờ camera bay xong rồi mới dựng đường đi. Đường đi được chốt bằng TOẠ ĐỘ MÀN
        /// HÌNH một lần duy nhất, nên đọc lúc camera còn đang bay là chốt vào một khung
        /// hình sắp biến mất — ngón tay sẽ trượt ở một chỗ chẳng liên quan gì tới bảng.
        /// Đó cũng là lý do nhịp này khoá kéo/zoom: bảng mà trôi thì đường đi sai ngay.
        private IEnumerator PaintStageRoutine(Vector2Int startCell)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _paintStageDelay));

            // Bật cụm và chọn ngón tay TRƯỚC khi dựng đường đi.
            //
            // Đường đi được tính trong hệ toạ độ của chính ngón tay sẽ chạy, và phép đổi
            // hệ cần đọc Canvas phía trên nó — mà GetComponentInParent bỏ qua object đang
            // tắt. Dựng đường trước là đi hỏi một cái canvas chưa bật, và nhận về null.
            SetVisible(true);

            var finger = UseFinger(forDragStage: true);

            var path = finger != null ? BuildLoopPath(finger, startCell) : null;

            // Dưới hai điểm thì không có gì để kéo. Thà kết thúc hướng dẫn ở đây còn hơn
            // để một ngón tay đứng im giữa bảng — người chơi sẽ chờ nó làm gì đó.
            if (path == null || path.Count < 2)
            {
                // Hide chứ không phải yield break trần: cụm vừa được bật ở trên, bỏ đi mà
                // không tắt là để lại một bàn tay chết trên màn hình.
                Hide();
                yield break;
            }

            _tutorialState?.SetStage(TutorialStage.PaintCells);

            yield return LoopRoutine(finger, path);
        }

        /// Vòng KHÉP KÍN quanh ô camera vừa bay tới: xuống, sang phải, lên, rồi về chỗ cũ.
        ///
        /// Khép kín nên ngón tay không phải nhấc lên lần nào — nó cứ đi mãi, và đó đúng là
        /// thứ "tuần hoàn" nghĩa là. Một đường thẳng thì cứ hết đường lại phải nhấc tay
        /// quay về, mà cú quay về đó không dạy gì cả, chỉ là chi phí của hình dạng.
        ///
        /// Dựng bằng HAI VECTƠ BƯỚC đo từ chính lưới, không cộng chỉ số ô rồi đổi hệ từng
        /// điểm: chỉ cần ô bên phải và ô bên dưới là biết một ô dài bao nhiêu theo mỗi
        /// hướng trên canvas. Nhờ vậy vòng vẫn dựng được cả khi ô xuất phát nằm sát mép
        /// bảng — mấy chỉ số kia chỉ dùng để ĐO, không ai đi tô chúng.
        ///
        /// Trả về toạ độ CANVAS, không phải toạ độ ô: đổi hệ ba lần ở đây thì vòng lặp
        /// phía dưới không phải đụng tới camera lần nào nữa.
        private List<Vector2> BuildLoopPath(RectTransform finger, Vector2Int startCell)
        {
            if (finger == null || _boardView == null) return null;

            var layout = _boardView.Layout;
            if (layout == null) return null;

            var camera = _worldCamera != null ? _worldCamera : Camera.main;
            if (camera == null) return null;

            // Hệ toạ độ của CHÍNH ngón tay sắp chạy, không phải của ngón tay nhịp 1: hai
            // ảnh có thể nằm dưới hai object cha khác nhau, và lúc đó toạ độ tính theo cha
            // này đặt vào con kia sẽ lệch đúng bằng khoảng cách giữa hai cái cha.
            var parent = finger.parent as RectTransform;
            if (parent == null) return null;

            var canvas = finger.GetComponentInParent<Canvas>();

            // Overlay canvas thì ScreenPointToLocalPointInRectangle phải nhận null, không
            // phải camera của canvas — truyền nhầm ở đó cho ra toạ độ lệch hẳn.
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x, startCell.y, out var origin)
                || !TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x + 1, startCell.y, out var right)
                || !TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x, startCell.y + 1, out var down))
            {
                return null;
            }

            // Ô (x, y+1) là ô nằm DƯỚI trên màn hình: lưới đánh số từ góc trên bên trái
            // nên cellY tăng là world y giảm — xem BoardLayout.
            var stepRight = (right - origin) * _loopCells;
            var stepDown = (down - origin) * _loopCells;

            var p0 = origin + _dragFingerOffset;

            // Điểm cuối trùng điểm đầu: vòng tự nối lại, không cần xử lý riêng lúc quay lại.
            return new List<Vector2>
            {
                p0,
                p0 + stepDown,
                p0 + stepDown + stepRight,
                p0 + stepRight,
                p0,
            };
        }

        private static bool TryCellToCanvas(BoardLayout layout, Camera camera, RectTransform parent,
            Camera uiCamera, int x, int y, out Vector2 local)
        {
            var world = layout.CellToWorldCenter(x, y);
            var screen = camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screen, uiCamera, out local);
        }

        /// Đặt ngón tay ĐÚNG lên ô đầu rồi đi vòng mãi cho tới khi hướng dẫn tắt.
        ///
        /// KHÔNG có quãng hạ tay từ trên xuống. Nhịp 1 cần nó vì ở đó ngón tay đang nói
        /// "bấm vào đây", mà một cú bấm thì phải có đường đi xuống mới đọc ra là bấm. Ở
        /// nhịp này thứ đang được dạy là KÉO, và quãng rơi đầu chỉ làm ngón tay xuất phát
        /// lệch lên khỏi ô nó đang chỉ — đúng cái làm người xem nhìn nhầm chỗ.
        ///
        /// Cũng KHÔNG nhấc lên giữa các vòng. Đường đi khép kín nên nhấc lên là tự tạo ra
        /// một quãng đứt ở đúng chỗ đáng lẽ liền mạch nhất, và người xem sẽ đọc nó thành
        /// "gõ từng chỗ" thay vì "kéo một nét".
        private IEnumerator LoopRoutine(RectTransform finger, List<Vector2> path)
        {
            var lap = Mathf.Max(0.1f, _loopSeconds);

            // Đặt vào chỗ ngay từ frame đầu, không đợi vòng lặp: frame đầu tiên mà ngón
            // tay còn ở vị trí cũ của nhịp trước là một cú nháy thấy được.
            finger.anchoredPosition = path[0];

            var elapsed = 0f;

            while (_isShowing)
            {
                elapsed += Time.unscaledDeltaTime;

                // Trừ đi một vòng thay vì gán lại 0: phép gán làm mất phần dư của frame
                // vừa chạy, và mỗi vòng ăn bớt một chút cho tới khi nhịp đi lệch thấy được.
                while (elapsed >= lap) elapsed -= lap;

                finger.anchoredPosition = SampleAlong(path, elapsed / lap);

                yield return null;
            }
        }

        /// Điểm trên đường gấp khúc ở tỉ lệ t (0..1), chia đều theo SỐ ĐOẠN.
        ///
        /// Chia theo đoạn chứ không theo chiều dài thật: các ô liền nhau nên mọi đoạn dài
        /// bằng nhau, và phép chia đơn giản này cho ra đúng kết quả mà không phải đo trước.
        private static Vector2 SampleAlong(List<Vector2> path, float t)
        {
            var segments = path.Count - 1;
            if (segments <= 0) return path[0];

            var scaled = t * segments;
            var index = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);

            return Vector2.Lerp(path[index], path[index + 1], scaled - index);
        }

        /// Bật đúng một ngón tay và tắt cái còn lại, rồi trả về cái vừa bật.
        ///
        /// Tắt HẲN object chứ không chỉ bỏ qua khi vẽ: hai bàn tay cùng nằm trong Content,
        /// mà Content thì bật cả cụm. Không tắt cái thừa thì nhịp 2 có hai bàn tay trên
        /// màn — một cái đang kéo, một cái đứng chết ở ô màu từ nhịp trước.
        ///
        /// Ô Drag Finger để trống thì cả hai nhịp dùng chung một ảnh, đúng như bản trước.
        private RectTransform UseFinger(bool forDragStage)
        {
            var drag = _dragFinger != null ? _dragFinger : _finger;
            var active = forDragStage ? drag : _finger;

            if (_finger != null && _finger != active) _finger.gameObject.SetActive(false);
            if (_dragFinger != null && _dragFinger != active) _dragFinger.gameObject.SetActive(false);

            if (active != null && !active.gameObject.activeSelf) active.gameObject.SetActive(true);

            return active;
        }

        private IEnumerator ShowRoutine()
        {
            if (_delaySeconds > 0f) yield return new WaitForSeconds(_delaySeconds);

            var target = _paletteBar != null ? _paletteBar.TutorialSwatchRect : null;

            if (target == null)
            {
                Debug.LogWarning($"{nameof(TutorialOverlayView)}: thanh màu không có đủ ô cho " +
                                 "ô Tutorial Swatch Order bên ColorPaletteBar, nên không biết " +
                                 "chỉ ngón tay vào đâu. Bỏ qua hướng dẫn.", this);
                yield break;
            }

            SetVisible(true);

            _tutorialState?.SetStage(TutorialStage.PickColor);

            var finger = UseFinger(forDragStage: false);

            if (finger == null)
            {
                // Kiểu hỏng khó chịu nhất: cụm hướng dẫn vẫn hiện ra vì ảnh bàn tay nằm
                // trong Content, nên nhìn qua thì mọi thứ có vẻ chạy — chỉ là ngón tay
                // đứng im, và không có gì trong Console để lần ra.
                Debug.LogWarning($"{nameof(TutorialOverlayView)}: ô Finger còn trống nên ngón " +
                                 "tay không gõ. Kéo RectTransform của ảnh bàn tay vào ô đó.", this);
                yield break;
            }

            // Bám theo toạ độ thế giới của ô màu rồi mới cộng phần dịch. Đặt bằng
            // anchoredPosition thì phải cùng một cha với ô màu, mà cụm hướng dẫn lại
            // nằm ở lớp trên cùng của canvas.
            finger.position = target.position;
            finger.anchoredPosition += _fingerOffset;

            // Chốt lại chỗ ngón tay CHẠM, rồi nhịp gõ chỉ nhấc lên hạ xuống quanh đó.
            // Đọc sau khi đã đặt xong, vì đây mới là giá trị thật sau khi đổi hệ toạ độ.
            StartCoroutine(TapRoutine(finger, finger.anchoredPosition));
        }

        /// Ngón tay hạ từ trên xuống chạm ô màu, dừng một nhịp, rồi nhấc lên và lặp lại.
        ///
        /// Đứng im thì mắt đọc ra là một hình dán chứ không phải một hành động đang được
        /// mời làm theo. Chuyển động đi XUỐNG mới nói được "bấm vào đây" — phóng to thu nhỏ
        /// tại chỗ chỉ nói được "nhìn đây".
        ///
        /// Ba pha có tốc độ khác nhau có chủ ý: hạ nhanh, dừng hẳn, nhấc lên chậm. Đi và
        /// về cùng tốc độ thì nó thành con lắc, mà con lắc không giống một cú bấm.
        private IEnumerator TapRoutine(RectTransform finger, Vector2 restPosition)
        {
            if (finger == null || _tapSeconds <= 0f || _tapTravel <= 0f) yield break;

            var down = Mathf.Clamp01(_tapDownPortion);
            var hold = Mathf.Clamp01(_tapHoldPortion);
            var upStart = Mathf.Min(0.99f, down + hold);

            while (_isShowing)
            {
                var elapsed = 0f;

                while (elapsed < _tapSeconds && _isShowing)
                {
                    elapsed += Time.unscaledDeltaTime;

                    var t = Mathf.Clamp01(elapsed / _tapSeconds);
                    float lift;

                    if (t < down)
                    {
                        // 1 = đang ở trên cao, 0 = đã chạm ô màu.
                        lift = 1f - DOVirtual.EasedValue(0f, 1f, t / down, Ease.OutQuad);
                    }
                    else if (t < upStart)
                    {
                        lift = 0f;
                    }
                    else
                    {
                        lift = DOVirtual.EasedValue(0f, 1f, (t - upStart) / (1f - upStart), Ease.InOutSine);
                    }

                    finger.anchoredPosition = restPosition + Vector2.up * (_tapTravel * lift);
                    yield return null;
                }
            }

            finger.anchoredPosition = restPosition;
        }

        /// Ghi nhận việc phải hoãn, kèm tên object đang tắt.
        ///
        /// KHÔNG báo đỏ: canvas HUD tắt lúc khởi động là cách dựng scene hợp lệ, và
        /// OnEnable đã lo phần chạy tiếp. Báo lỗi cho một tình huống đã được xử lý chỉ
        /// làm người đọc đi sửa thứ không hỏng.
        ///
        /// Vẫn chỉ đích danh object cha đang tắt chứ không nói trống không: thủ phạm
        /// thường không phải object mang script, mà là một object cha nào đó trên đường
        /// lên gốc — nhìn hệ thống phân cấp thấy object của mình đang bật thì rất dễ
        /// kết luận nhầm sang lỗi khác.
        private void WarnInactiveOnce()
        {
            if (_hasWarnedInactive || !_logDecision) return;

            _hasWarnedInactive = true;

            var culprit = transform;
            while (culprit != null && culprit.gameObject.activeSelf) culprit = culprit.parent;

            var blocker = culprit != null ? culprit.name : "(không rõ)";

            Debug.Log($"[Tutorial] hoãn lại vì object '{blocker}' đang tắt " +
                      $"({PathOf(transform)}). Sẽ chạy ngay khi nó bật lên.", this);
        }

        private static string PathOf(Transform target)
        {
            var path = target.name;

            for (var parent = target.parent; parent != null; parent = parent.parent)
            {
                path = $"{parent.name}/{path}";
            }

            return path;
        }

        private void Hide()
        {
            StopAllCoroutines();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            var changed = _isShowing != visible;

            _isShowing = visible;

            // Báo xuống Domain để tầng Gameplay hỏi được. BoardInput khoá kéo/zoom bảng
            // theo cờ này, và nó không có đường nào nhìn thấy lớp UI này.
            //
            // Đặt ở đây chứ không ở hai đầu Show/Hide: SetVisible là cửa DUY NHẤT đổi
            // trạng thái hiện/ẩn, nên không có đường nào lọt lưới.
            // Tắt là hết hướng dẫn, chắc chắn. Còn BẬT thì không tự đặt nhịp nào: hai
            // nhịp đều đi qua đây, và đoán hộ ở đây thì nhịp 2 vừa đặt xong đã bị chính
            // lời gọi SetVisible(true) của nó ghi đè lại thành nhịp 1.
            if (!visible) _tutorialState?.SetStage(TutorialStage.None);

            if (_content != null && _content.activeSelf != visible) _content.SetActive(visible);

            // Chỉ bắn ở đúng khoảnh khắc CHUYỂN trạng thái. Hide() được gọi ở rất nhiều
            // đường — mỗi lần bảng dựng xong, mỗi lần chọn màu — và phần lớn trong số đó
            // là tắt một thứ vốn đã tắt.
            //
            // Phần đồng bộ _content ở trên thì KHÔNG nằm trong nhánh này: lần gọi đầu
            // tiên, từ Init, là "tắt một thứ đang tắt" theo cờ nhưng lại là lần duy nhất
            // tắt cụm hướng dẫn mà người dựng scene để bật sẵn trong prefab.
            // Không còn sự kiện riêng ở đây: mọi bên quan tâm đều nghe
            // TutorialState.OnStageChanged. Một view phát tín hiệu song song với Domain là
            // hai nguồn sự thật cho cùng một chuyện, và chúng lệch nhau ở đúng nhịp mới
            // thêm vào.
            _ = changed;
        }
    }
}
