using System.Collections;
using DG.Tweening;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Hướng dẫn cho người chơi mới: một ngón tay chỉ vào ô màu đầu tiên trên thanh chọn,
    /// kèm một bảng nhắc.
    ///
    /// Hiện khi vào màn hướng dẫn mà CHƯA TÔ ô nào, tắt ngay khi người chơi chọn màu đầu
    /// tiên. Không lưu cờ "đã xem" — điều kiện đọc thẳng từ trạng thái tô, nên chỉ cần
    /// xoá tiến độ là thử lại được bao nhiêu lần cũng xong.
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

        [Tooltip("Dịch ngón tay khỏi tâm ô màu, tính bằng pixel của canvas. Thường cần " +
                 "dịch xuống dưới để đầu ngón trỏ chạm vào ô chứ không phải cả bàn tay " +
                 "đè lên nó.")]
        [SerializeField] private Vector2 _fingerOffset = new(0f, -60f);

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

        /// Hướng dẫn đang nằm trên màn hình. Lời nhắc "chưa chọn màu" đọc cờ này để im
        /// lặng — hai thứ nói cùng một điều, chồng lên nhau chỉ thành ồn.
        public bool IsShowing => _isShowing;

        private bool _isShowing;
        private bool _isDisabled;
        private bool _hasWarnedInactive;

        /// Đã quyết định là phải hiện, nhưng chưa chạy được vì object còn tắt.
        private bool _pendingShow;

        public void Init(ILevelService levelService, IPaintService paintService, ColorPaletteBar paletteBar)
        {
            _levelService = levelService;
            _paintService = paintService;
            _paletteBar = paletteBar;

            if (_content == null || _content == gameObject)
            {
                _isDisabled = true;

                Debug.LogError($"{nameof(TutorialOverlayView)}: ô Content phải trỏ tới một " +
                               "object CON chứa ngón tay và bảng nhắc. Bỏ trống hoặc trỏ về " +
                               "chính object này thì việc tắt hướng dẫn sẽ huỷ luôn coroutine " +
                               "của chính nó. Hướng dẫn bị tắt.", this);
                return;
            }

            // Nghe OnBoardReady chứ không nghe OnLevelStarted: lúc màn bắt đầu thì thanh
            // màu chưa dựng xong, mà ngón tay cần biết ô màu đầu tiên đứng ở đâu.
            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnColorSelected += HandleColorSelected;

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
            if (_paintService != null)
            {
                _paintService.OnBoardReady -= HandleBoardReady;
                _paintService.OnColorSelected -= HandleColorSelected;
            }
        }

        private void HandleBoardReady()
        {
            Hide();

            if (_isDisabled) return;

            var loadedLevel = LoadedLevelId();
            var untouched = _paintService.IsUntouched;

            if (_logDecision)
            {
                Debug.Log($"[Tutorial] màn đang nạp {loadedLevel} (cần {_tutorialLevelId}), " +
                          $"chưa tô ô nào: {untouched} → " +
                          $"{(loadedLevel == _tutorialLevelId && untouched ? "HIỆN" : "bỏ qua")}", this);
            }

            if (loadedLevel != _tutorialLevelId) return;
            if (!untouched) return;

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

        private void HandleColorSelected(int paletteIndex) => Hide();

        private IEnumerator ShowRoutine()
        {
            if (_delaySeconds > 0f) yield return new WaitForSeconds(_delaySeconds);

            var target = _paletteBar != null ? _paletteBar.FirstSwatchRect : null;

            if (target == null)
            {
                Debug.LogWarning($"{nameof(TutorialOverlayView)}: thanh màu chưa có ô nào nên " +
                                 "không biết chỉ ngón tay vào đâu. Bỏ qua hướng dẫn.", this);
                yield break;
            }

            SetVisible(true);

            if (_finger == null)
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
            _finger.position = target.position;
            _finger.anchoredPosition += _fingerOffset;

            // Chốt lại chỗ ngón tay CHẠM, rồi nhịp gõ chỉ nhấc lên hạ xuống quanh đó.
            // Đọc sau khi đã đặt xong, vì đây mới là giá trị thật sau khi đổi hệ toạ độ.
            StartCoroutine(TapRoutine(_finger.anchoredPosition));
        }

        /// Ngón tay hạ từ trên xuống chạm ô màu, dừng một nhịp, rồi nhấc lên và lặp lại.
        ///
        /// Đứng im thì mắt đọc ra là một hình dán chứ không phải một hành động đang được
        /// mời làm theo. Chuyển động đi XUỐNG mới nói được "bấm vào đây" — phóng to thu nhỏ
        /// tại chỗ chỉ nói được "nhìn đây".
        ///
        /// Ba pha có tốc độ khác nhau có chủ ý: hạ nhanh, dừng hẳn, nhấc lên chậm. Đi và
        /// về cùng tốc độ thì nó thành con lắc, mà con lắc không giống một cú bấm.
        private IEnumerator TapRoutine(Vector2 restPosition)
        {
            if (_finger == null || _tapSeconds <= 0f || _tapTravel <= 0f) yield break;

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

                    _finger.anchoredPosition = restPosition + Vector2.up * (_tapTravel * lift);
                    yield return null;
                }
            }

            _finger.anchoredPosition = restPosition;
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
            _isShowing = visible;

            if (_content == null) return;

            if (_content.activeSelf != visible) _content.SetActive(visible);
        }
    }
}
