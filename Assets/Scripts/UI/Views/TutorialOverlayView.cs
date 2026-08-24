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

        [Header("Nhịp gõ của ngón tay")]
        [Tooltip("Ngón tay thu nhỏ còn bao nhiêu ở đáy nhịp gõ. 1 là đứng yên.")]
        [SerializeField] private float _tapScale = 0.82f;

        [Tooltip("Một nhịp gõ mất bao lâu.")]
        [SerializeField] private float _tapSeconds = 0.75f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private ColorPaletteBar _paletteBar;

        private bool _isShowing;
        private bool _isDisabled;

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
            _levelService.OnLevelStarted += HandleLevelStarted;

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_paintService != null)
            {
                _paintService.OnBoardReady -= HandleBoardReady;
                _paintService.OnColorSelected -= HandleColorSelected;
            }

            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        /// Tắt NGAY khi đổi màn, không đợi bàn dựng xong. Không có chỗ này thì hướng dẫn
        /// của màn cũ còn nằm trên màn hình suốt lúc màn mới đang nạp.
        private void HandleLevelStarted(int levelId) => Hide();

        private void HandleBoardReady()
        {
            Hide();

            if (_isDisabled) return;
            if (_levelService.CurrentLevel != _tutorialLevelId) return;
            if (!_paintService.IsUntouched) return;

            StartCoroutine(ShowRoutine());
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

            if (_finger != null)
            {
                // Bám theo toạ độ thế giới của ô màu rồi mới cộng phần dịch. Đặt bằng
                // anchoredPosition thì phải cùng một cha với ô màu, mà cụm hướng dẫn lại
                // nằm ở lớp trên cùng của canvas.
                _finger.position = target.position;
                _finger.anchoredPosition += _fingerOffset;
            }

            SetVisible(true);
            StartCoroutine(TapRoutine());
        }

        /// Ngón tay gõ nhè nhẹ. Đứng im thì mắt đọc ra là một hình dán, không phải một
        /// hành động đang được mời làm theo.
        private IEnumerator TapRoutine()
        {
            if (_finger == null || _tapSeconds <= 0f) yield break;

            var original = _finger.localScale;

            while (_isShowing)
            {
                var elapsed = 0f;

                while (elapsed < _tapSeconds && _isShowing)
                {
                    elapsed += Time.unscaledDeltaTime;

                    // sin đi từ 0 lên 1 rồi về 0 trong một chu kỳ, nên nhịp gõ khép kín
                    // mà không cần chia hai pha lên xuống.
                    var wave = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / _tapSeconds));
                    var eased = DOVirtual.EasedValue(0f, 1f, wave, Ease.InOutSine);

                    _finger.localScale = original * Mathf.LerpUnclamped(1f, _tapScale, eased);
                    yield return null;
                }
            }

            _finger.localScale = original;
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
