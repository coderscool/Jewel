using DG.Tweening;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Khung tranh viền quanh bức tranh đã hoàn thành, kèm tấm nền trắng lót phía sau.
    /// WinCelebration gọi tới khi dải quét đã tắt và camera đang lùi ra lấy chỗ.
    ///
    /// Hai SpriteRenderer 9-slice, không dựng bốn cạnh rời: bốn object là bốn chỗ để lệch
    /// nhau, và góc khung thì phải tự ghép — 9-slice đã làm sẵn cả hai việc đó.
    ///
    /// Ba lớp xếp từ sau ra trước: **nền trắng → bức tranh → khung**. Nền trắng phải thò
    /// ra quá mép tranh và chui xuống DƯỚI bề dày khung, không thì giữa hai lớp hở một
    /// vành nền game.
    ///
    /// Một lớp VIEW thuần: không nghe sự kiện, không biết luật thắng màn, không giữ trạng
    /// thái nào ngoài lượt tween đang chạy. Ai gọi thì nó hiện. Nhờ vậy nó không cần đi
    /// qua DI và không phải nối thêm dây ở GameEntryPoint.
    ///
    /// Cỡ tính bằng WORLD UNIT và chốt một lần lúc hiện ra, nên camera có lùi tiếp thì
    /// khung vẫn ôm đúng bức tranh — người chơi thấy tranh nhỏ lại bên trong một cái khung
    /// đứng yên, đúng cảm giác lồng tranh vào khung.
    public class BoardFrame : MonoBehaviour
    {
        [Tooltip("SpriteRenderer vẽ KHUNG. **Draw Mode phải để Sliced** và sprite phải có " +
                 "Border trong Sprite Editor — không thì cạnh khung bị kéo dãn méo và ô Size " +
                 "không có tác dụng.\n\n" +
                 "Sorting Order phải CAO HƠN mọi lớp của bảng: khung nằm trước bức tranh.")]
        [SerializeField] private SpriteRenderer _frame;

        [Tooltip("Mép NGOÀI của khung cách mép bức tranh bao nhiêu Ô.\n\n" +
                 "Chú ý đây là mép NGOÀI. Phần nhìn thấy của nền trắng = số này TRỪ ĐI bề " +
                 "dày khung (Border chia cho Pixels Per Unit của sprite). Với sprite mặc " +
                 "định — border 96, PPU 64 — khung dày 1.5 ô, nên để 2.7 là lộ ra 1.2 ô nền " +
                 "trắng. Để đúng 1.5 là khung ôm sát tranh, không còn nền nào nhìn thấy.\n\n" +
                 "Khai bằng ô chứ không bằng pixel: một ô luôn rộng đúng một world unit, nên " +
                 "con số này giữ nguyên ý nghĩa ở mọi cỡ bảng và mọi mức zoom.")]
        [SerializeField] private float _paddingCells = 2.7f;

        [Header("Nền lót")]
        [Tooltip("SpriteRenderer vẽ tấm nền trắng sau bức tranh. Để trống thì bỏ hẳn lớp này.\n\n" +
                 "Draw Mode = Sliced như khung. Sorting Order phải THẤP HƠN mọi lớp của " +
                 "bảng (nó nằm SAU tranh) nhưng vẫn cao hơn canvas nền.")]
        [SerializeField] private SpriteRenderer _backing;

        [Tooltip("Nền trắng thò ra quá mép bức tranh bao nhiêu Ô.\n\n" +
                 "Phải LỚN HƠN mép trong của khung (tức Padding Cells trừ bề dày khung), " +
                 "để rìa nền chui xuống dưới khung — bằng đúng thì một pixel lệch cũng thành " +
                 "một vành sáng chạy quanh tranh. Thừa ra bao nhiêu cũng vô hại vì khung che " +
                 "hết. Sai thì có cảnh báo trong Console.")]
        [SerializeField] private float _backingPaddingCells = 2f;

        [Header("Hiệu ứng hiện ra")]
        [Tooltip("Khung bắt đầu ở cỡ gấp ngần này lần rồi co về 1. Lớn hơn 1 là khung ập " +
                 "vào từ ngoài — hợp với cảm giác đóng khung. Để 1 là không co gì.\n\n" +
                 "Chỉ áp cho KHUNG. Nền trắng chỉ hiện dần: nó là tấm giấy được đặt sẵn ở " +
                 "đó, cho nó cũng bay vào là hai thứ cùng động và mắt không biết nhìn đâu.")]
        [SerializeField] private float _scaleFrom = 1.12f;

        [Tooltip("Thời gian khung co về đúng cỡ.")]
        [SerializeField] private float _scaleDuration = 0.45f;

        [Tooltip("Thời gian hiện dần từ trong suốt, dùng chung cho cả khung lẫn nền. Nên " +
                 "NGẮN HƠN thời gian co, để khung kịp rõ mặt trước khi nó dừng lại.")]
        [SerializeField] private float _fadeDuration = 0.3f;

        [SerializeField] private Ease _scaleEase = Ease.OutBack;

        /// Lượt hiện đang chạy. Giữ lại để Kill — tween sống sót qua lúc object bị tắt sẽ
        /// ghi đè lên alpha của lượt sau.
        private Tween _scaleTween;
        private Tween _fadeTween;

        /// Màu gốc của hai sprite, chụp một lần. Lượt hiện đầu tiên đặt alpha về 0, nên đọc
        /// muộn hơn là đọc lại đúng cái 0 mà chính nó vừa ghi.
        private Color _frameBaseColor = Color.white;
        private Color _backingBaseColor = Color.white;
        private bool _hasBaseColors;

        /// Mép ngoài khung cách mép tranh bao nhiêu ô. WinCelebration hỏi con số này để
        /// bảo camera lùi đúng bằng chỗ cần — thay vì bắt bạn tự canh một hệ số zoom rồi
        /// canh lại mỗi lần đổi bề dày khung.
        public float OuterPaddingCells => Mathf.Max(0f, _paddingCells);

        private void Awake()
        {
            CaptureBaseColors();
            SetActive(false);
            WarnIfMisconfigured();
        }

        private void OnDestroy() => KillTweens();

        /// Đóng khung cho bức tranh nằm trong `boardBounds` (world).
        ///
        /// Nhận Bounds thay vì tự đi hỏi BoardView: lớp này không cần biết bảng là gì, và
        /// một tham số thì gọi lại được từ bất cứ đâu — kể cả từ một nút thử trong Editor.
        public void Show(Bounds boardBounds)
        {
            if (_frame == null && _backing == null) return;

            KillTweens();
            CaptureBaseColors();
            SetActive(true);

            Fit(_backing, boardBounds, _backingPaddingCells);
            Fit(_frame, boardBounds, _paddingCells);

            SetAlpha(0f);

            PlayScale();
            PlayFade();
        }

        /// Cất cả hai lớp đi ngay, không hiệu ứng. Gọi khi dựng lại bảng.
        public void HideInstantly()
        {
            KillTweens();

            SetAlpha(0f);

            if (_frame != null) _frame.transform.localScale = Vector3.one;

            SetActive(false);
        }

        private void Fit(SpriteRenderer target, Bounds bounds, float paddingCells)
        {
            if (target == null) return;

            target.transform.position = new Vector3(
                bounds.center.x, bounds.center.y, target.transform.position.z);

            // Draw Mode Simple thì Size là thuộc tính chỉ đọc trên thực tế — gán vào chỉ
            // sinh cảnh báo của Unity mà không đổi gì. Cảnh báo của chính chúng ta ở
            // WarnIfMisconfigured đã nói rõ hơn, nên ở đây chỉ lặng lẽ bỏ qua.
            if (target.drawMode == SpriteDrawMode.Simple) return;

            var padding = Mathf.Max(0f, paddingCells) * 2f;

            // Size của Draw Mode Sliced tính theo world unit, và một ô rộng đúng một unit —
            // nên cộng thẳng lề vào là xong, không phải quy đổi gì.
            target.size = new Vector2(bounds.size.x + padding, bounds.size.y + padding);
        }

        private void PlayScale()
        {
            if (_frame == null) return;

            var from = Mathf.Max(0.01f, _scaleFrom);
            var duration = Mathf.Max(0f, _scaleDuration);

            if (duration <= 0f || Mathf.Approximately(from, 1f))
            {
                _frame.transform.localScale = Vector3.one;
                return;
            }

            _frame.transform.localScale = Vector3.one * from;
            _scaleTween = _frame.transform.DOScale(1f, duration).SetEase(_scaleEase);
        }

        private void PlayFade()
        {
            var duration = Mathf.Max(0f, _fadeDuration);

            if (duration <= 0f)
            {
                SetAlpha(1f);
                return;
            }

            // DOVirtual.Float chứ KHÔNG phải SpriteRenderer.DOFade: DOFade của
            // SpriteRenderer nằm trong DOTweenModuleSprite, một file .cs rời trong Plugins
            // nên nó biên dịch vào Assembly-CSharp — assembly mà JewelPainter.Gameplay
            // không với tới. Chỉ DOTween.dll là auto-reference. Cùng cái bẫy đã ghi ở
            // PopupView và WinPopupView.
            _fadeTween = DOVirtual.Float(0f, 1f, duration, SetAlpha);
        }

        /// `t` là phần alpha GỐC của mỗi sprite, không phải alpha tuyệt đối — nền trắng để
        /// mờ sẵn trong Inspector thì nó mờ y như vậy lúc hiện xong.
        private void SetAlpha(float t)
        {
            Apply(_frame, _frameBaseColor, t);
            Apply(_backing, _backingBaseColor, t);
        }

        private static void Apply(SpriteRenderer target, Color baseColor, float t)
        {
            if (target == null) return;

            var color = baseColor;
            color.a = baseColor.a * t;
            target.color = color;
        }

        private void SetActive(bool active)
        {
            // Tắt object chứ không chỉ đặt alpha 0: alpha 0 vẫn đi qua đường vẽ, và khung
            // chỉ thuộc về khoảnh khắc thắng màn chứ không sống suốt lúc chơi.
            if (_frame != null && _frame.gameObject.activeSelf != active)
            {
                _frame.gameObject.SetActive(active);
            }

            if (_backing != null && _backing.gameObject.activeSelf != active)
            {
                _backing.gameObject.SetActive(active);
            }
        }

        private void CaptureBaseColors()
        {
            if (_hasBaseColors) return;

            _hasBaseColors = true;

            if (_frame != null) _frameBaseColor = Fix(_frame, _frame.color);
            if (_backing != null) _backingBaseColor = Fix(_backing, _backing.color);
        }

        /// Sprite để sẵn alpha 0 trong Inspector thì lớp đó không bao giờ hiện, mà cũng
        /// không có lỗi nào báo. Coi như 1 và nói ra.
        private Color Fix(SpriteRenderer target, Color color)
        {
            if (color.a > 0.001f) return color;

            Debug.LogWarning(
                $"{nameof(BoardFrame)}: màu của '{target.name}' đang có alpha 0 — lớp này sẽ " +
                "hiện ra mà vẫn trong suốt. Đã tạm coi như 1.", this);

            color.a = 1f;
            return color;
        }

        private void WarnIfMisconfigured()
        {
            WarnIfNotSliced(_frame);
            WarnIfNotSliced(_backing);

            if (_frame == null || _frame.sprite == null) return;

            // Bề dày khung đọc thẳng từ sprite thay vì bắt khai lại bằng tay: khai lại là
            // thêm một con số phải nhớ đồng bộ mỗi lần đổi ảnh khung.
            var border = _frame.sprite.border;
            var ppu = Mathf.Max(0.0001f, _frame.sprite.pixelsPerUnit);
            var band = Mathf.Max(Mathf.Max(border.x, border.z), Mathf.Max(border.y, border.w)) / ppu;

            if (band <= 0f)
            {
                Debug.LogWarning(
                    $"{nameof(BoardFrame)}: sprite '{_frame.sprite.name}' không khai Border " +
                    "trong Sprite Editor, nên 9-slice không có gì để giữ — cạnh và góc khung " +
                    "sẽ bị kéo dãn méo theo cỡ bảng.", this);

                return;
            }

            if (_paddingCells < band)
            {
                Debug.LogWarning(
                    $"{nameof(BoardFrame)}: Padding Cells ({_paddingCells:0.##}) nhỏ hơn bề dày " +
                    $"khung ({band:0.##} ô), nên mép trong của khung sẽ ăn vào bức tranh. " +
                    $"Để ít nhất {band:0.##}; muốn lộ nền trắng thì cộng thêm bề rộng nền " +
                    "bạn muốn thấy.", this);
            }

            if (_backing == null) return;

            var innerEdge = _paddingCells - band;

            if (_backingPaddingCells < innerEdge)
            {
                Debug.LogWarning(
                    $"{nameof(BoardFrame)}: Backing Padding Cells ({_backingPaddingCells:0.##}) " +
                    $"chưa với tới mép trong của khung ({innerEdge:0.##} ô), nên sẽ hở một vành " +
                    $"nền game giữa nền trắng và khung. Đặt khoảng {innerEdge + 0.5f:0.##}.",
                    this);
            }
        }

        private void WarnIfNotSliced(SpriteRenderer target)
        {
            if (target == null || target.drawMode != SpriteDrawMode.Simple) return;

            Debug.LogWarning(
                $"{nameof(BoardFrame)}: '{target.name}' đang để Draw Mode = Simple, nên ô Size " +
                "không dùng được và nó sẽ giữ nguyên cỡ sprite gốc thay vì ôm lấy bảng. Đổi " +
                "sang Sliced.", this);
        }

        private void KillTweens()
        {
            if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();
            if (_fadeTween != null && _fadeTween.IsActive()) _fadeTween.Kill();

            _scaleTween = null;
            _fadeTween = null;
        }
    }
}
