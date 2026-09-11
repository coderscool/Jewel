using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using JewelPainter.Gameplay.Config;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô màu trong thanh chọn màu: màu, số thứ tự, số ô còn lại, viền khi được chọn.
    public class ColorSwatchView : MonoBehaviour
    {
        [SerializeField] private Image _colorImage;
        [SerializeField] private Text _numberText;
        [SerializeField] private Button _button;

        [Tooltip("Màu chữ số và chữ số ô còn lại, dùng chung cho mọi ô màu.")]
        [SerializeField] private Color _textColor = Color.black;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Số ô còn lại của màu này. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _remainingText;

        [Tooltip("Viền báo màu đang được chọn. Để trống thì không có dấu hiệu chọn.")]
        [SerializeField] private GameObject _selectedHighlight;

        [Tooltip("Dấu hiệu thứ hai khi ô được NHẤC LÊN — thường là một mũi tên hoặc nhãn " +
                 "đặt TRÊN ĐẦU viên ngọc.\n\n" +
                 "Đi theo động tác NHẤC LÊN, không theo việc được chọn: booster tô tự do " +
                 "nhấc mọi ô lên thì mọi ô cũng hiện cái này. Chỉ riêng Selected Highlight " +
                 "là vẫn nằm trên đúng một ô.\n\n" +
                 "Tách khỏi Selected Highlight vì hai thứ nằm ở hai chỗ khác nhau và " +
                 "thường muốn dựng riêng: viền bọc quanh ô, còn cái này nhô lên trên. " +
                 "Chỉ cần một dấu hiệu thì bỏ trống ô nào không dùng.\n\n" +
                 "Đặt nó ngoài vùng bị cắt của Scroll Rect nếu muốn nó nhô cao hơn thanh.")]
        [SerializeField] private GameObject _selectedIcon;

        [Tooltip("Vòng tròn tiến độ. Image phải đặt Image Type = Filled, " +
                 "Fill Method = Radial 360 — code chỉ gán fillAmount, Unity lo phần vẽ cung.")]
        [SerializeField] private Image _progressRing;

        [Tooltip("Object được nâng lên khi ô này được chọn. Phải là một object CON " +
                 "(ví dụ 'Content' bọc ColorFill và Number), KHÔNG phải chính ColorSwatch — " +
                 "Horizontal Layout Group điều khiển vị trí con trực tiếp nên sẽ kéo nó về.")]
        [SerializeField] private RectTransform _riseTarget;

        [Tooltip("Nâng lên bao nhiêu pixel khi được chọn.")]
        [SerializeField] private float _selectedRise = 24f;

        [Tooltip("Phóng to bao nhiêu lần khi được chọn. 1 là giữ nguyên cỡ.\n\n" +
                 "Áp lên đúng object Rise Target, nên nó nhô lên và to ra cùng một lúc. " +
                 "Chỉ nhô mà không to thì ô được chọn dễ bị đọc nhầm là ô bị lệch hàng.")]
        [SerializeField] private float _selectedScale = 1.25f;

        [Header("Bóng đổ — để trống cũng chạy")]
        [Tooltip("Vệt bóng mờ dưới chân viên đá.\n\n" +
                 "PHẢI đặt NGOÀI Rise Target (con trực tiếp của ColorSwatch, nằm TRÊN CÙNG " +
                 "trong danh sách con để vẽ sau lưng mọi thứ). Đặt trong Rise Target thì " +
                 "bóng nhô lên theo viên đá — mà bóng dính chặt vào vật thì mắt đọc ra là " +
                 "cả hai cùng nằm phẳng, và toàn bộ cảm giác 'được nhấc lên' biến mất.\n\n" +
                 "Kiểu Graphic chứ không phải Image: sau này đổi sang RawImage hay một " +
                 "graphic tự viết đều không phải sửa lại chỗ này.")]
        [SerializeField] private Graphic _shadow;

        [Tooltip("Cỡ bóng khi ô ĐƯỢC CHỌN, so với cỡ lúc thường. Lớn hơn 1 vì vật nhấc " +
                 "cao thì bóng loang rộng ra.")]
        [SerializeField] private float _selectedShadowScale = 1.4f;

        [Tooltip("Độ đục của bóng khi KHÔNG được chọn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shadowAlpha = 0.35f;

        [Tooltip("Độ đục của bóng khi ĐƯỢC CHỌN. Đặt NHẠT HƠN ô trên, đừng đậm hơn.\n\n" +
                 "Đây là chỗ ai cũng đặt ngược lúc đầu: được chọn thì muốn nổi bật nên " +
                 "tăng độ đục lên. Nhưng vật càng nhấc cao thì bóng càng LOANG RỘNG và " +
                 "càng NHẠT — rộng ra mà lại đậm thêm thì mắt đọc ra là viên đá bị ấn " +
                 "xuống chứ không phải nhấc lên.")]
        [Range(0f, 1f)]
        [SerializeField] private float _selectedShadowAlpha = 0.22f;

        [Header("Tô xong màu — để trống cả hai thì ẩn ngay như cũ")]
        [Tooltip("Object THU NHỎ dần rồi biến mất khi màu này vừa được tô xong. Để trống " +
                 "thì thu chính ô màu này.\n\n" +
                 "ĐỪNG kéo Rise Target vào đây: Rise Target đang bị phần nhấc lên điều " +
                 "khiển localScale, hai bên sẽ giành nhau và ô màu kẹt ở một cỡ nào đó " +
                 "sau lần tái dùng đầu tiên.")]
        [SerializeField] private RectTransform _completeShrinkTarget;

        [Tooltip("Viên ngọc NÂNG LÊN rồi mờ dần tan đi, thay vì đứng đợi tới lúc cả ô bị " +
                 "tắt. Thường là chính object đã gán ở ô Color Image. Để trống thì bỏ qua " +
                 "phần này.\n\n" +
                 "ĐỪNG gán trùng với Shrink Target hay Rise Target: cả ba đều điều khiển " +
                 "cùng một Transform và sẽ giành nhau.")]
        [SerializeField] private Graphic _completeJewel;

        [Tooltip("Viên ngọc nâng lên bao nhiêu pixel trước khi tan hẳn.")]
        [SerializeField] private float _jewelRise = 60f;

        [Tooltip("Cú bay lên và mờ dần của viên ngọc chiếm bao nhiêu PHẦN của cả màn diễn, " +
                 "thang 0..1. Đặt xấp xỉ Shrink Portion thì viên ngọc và cái nền cùng rời " +
                 "sân khấu một lúc.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _jewelPortion = 0.5f;

        [Tooltip("Dấu tick hiện lên đúng lúc đó: to dần ra rồi mờ đi. Để trống thì bỏ qua " +
                 "phần này.\n\n" +
                 "Đặt nó NGOÀI Shrink Target, không thì nó vừa to ra vừa bị thu nhỏ theo và " +
                 "gần như đứng yên. Để sẵn ở trạng thái TẮT trong prefab.")]
        [SerializeField] private Graphic _completeTick;

        [Tooltip("Cả màn diễn kéo dài bao nhiêu giây.\n\n" +
                 "Đây là thứ duy nhất chỉnh tốc độ — cú thu nhỏ và dấu tick chia nhau đúng " +
                 "khoảng thời gian này theo tỉ lệ, nên kéo dài ra là cả hai cùng chậm lại " +
                 "và vẫn khớp nhau.")]
        [SerializeField] private float _completeDuration = 0.85f;

        [Tooltip("Cú thu nhỏ chiếm bao nhiêu PHẦN của cả màn diễn, thang 0..1. 0.4 nghĩa " +
                 "là ô co lại và biến mất trong 40% đầu, phần còn lại là sân khấu của " +
                 "riêng dấu tick.\n\n" +
                 "Để 1 là hai thứ chạy song song suốt màn diễn như bản trước.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _shrinkPortion = 0.4f;

        [Tooltip("Dấu tick chờ bao nhiêu PHẦN của màn diễn rồi mới hiện ra, thang 0..1.\n\n" +
                 "Đặt xấp xỉ Shrink Portion thì tick xuất hiện đúng lúc ô vừa biến mất. " +
                 "Đặt thấp hơn một chút (0.3 so với 0.4) thì hai động tác gối lên nhau một " +
                 "nhịp ngắn — mắt đọc ra là ô BIẾN THÀNH dấu tick, chứ không phải hai " +
                 "chuyện rời rạc nối đuôi.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _tickDelay = 0.3f;

        [Tooltip("Cỡ dấu tick lúc bắt đầu và lúc kết thúc, so với cỡ dựng trong prefab.")]
        [SerializeField] private float _tickStartScale = 0.4f;

        [SerializeField] private float _tickEndScale = 1.6f;

        [Tooltip("Dấu tick bắt đầu mờ đi từ mốc nào của ĐỜI NÓ, thang 0..1 — tính từ lúc " +
                 "nó hiện ra chứ không phải từ đầu màn diễn. 0.35 nghĩa là một phần ba đầu " +
                 "nó hiện rõ rồi mới tan dần.\n\n" +
                 "Để 0 là vừa hiện ra đã bắt đầu mờ, và mắt không kịp đọc ra đó là dấu tick.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _tickFadeStart = 0.35f;

        [Header("Cú loé lúc viên ngọc tan")]
        [Tooltip("Ảnh vẽ hoạt ảnh loé. Phải là Image của UI, KHÔNG phải Sprite Renderer: " +
                 "Canvas vẽ đè lên mọi Sprite Renderer nên hiệu ứng sẽ vô hình. Đó cũng là " +
                 "lý do không gắn thẳng FlipbookBurstPool vào đây: kho đó phát bằng " +
                 "SpriteRenderer, hợp với bàn cờ chứ không hợp với Canvas. Hai bên dùng " +
                 "chung asset Flipbook Clip — một lần bake, hai nơi phát, chỉ khác cái đem " +
                 "đi vẽ.\n\n" +
                 "Đặt nó ở chỗ viên ngọc, để object TẮT sẵn trong prefab, và đặt NGOÀI " +
                 "Shrink Target: nằm trong đó thì nó bị thu về cỡ 0 cùng cái nền, đúng vào " +
                 "lúc đáng lẽ phải loé.\n\n" +
                 "Thanh màu nằm trong một Scroll Rect có Mask, nên cú loé tràn ra khỏi " +
                 "khung sẽ bị cắt cụt. Bỏ tick Maskable trên chính Image này là nó thoát " +
                 "khỏi mọi Mask cha và vẽ tràn thoải mái — đổi lại, nó cũng vẽ tràn khi ô " +
                 "màu đang trôi nửa trong nửa ngoài mép thanh.")]
        [SerializeField] private Image _completeBurst;

        [Tooltip("Cùng asset mà FlipbookBurstPool dùng — một lần bake, hai nơi phát.")]
        [SerializeField] private FlipbookClip _completeBurstClip;

        [Tooltip("Loé vào lúc nào, tính theo TỈ LỆ của Complete Duration.\n\n" +
                 "Để bằng Jewel Portion (mặc định 0.5) thì cú loé nổ đúng khoảnh khắc viên " +
                 "ngọc vừa tan hết — nó thế chỗ viên ngọc chứ không chồng lên.")]
        [Range(0f, 1f)]
        [SerializeField] private float _burstDelay = 0.5f;

        [Tooltip("Nhân vào tốc độ phát. 1 là đúng nhịp lúc bake.")]
        [SerializeField] private float _burstSpeed = 1f;

        private Action<int> _onClicked;
        private int _displayedRemaining = -1;
        private float _displayedProgress = -1f;

        private Vector2 _riseBasePosition;
        private Vector3 _riseBaseScale;
        private bool _hasRiseBase;

        private Coroutine _complete;

        /// Cỡ gốc của object bị thu nhỏ, ghi lại ở lần diễn ĐẦU TIÊN.
        ///
        /// Nhớ cỡ gốc chứ không trả về 1: người dựng có quyền để object đó ở một cỡ khác,
        /// và ép về 1 là mọi ô màu đổi cỡ vĩnh viễn ngay sau màn diễn đầu tiên. Cùng lý
        /// do đã ghi ở ApplyRise.
        private Vector3 _completeBaseScale = Vector3.one;
        private bool _hasCompleteBase;

        /// Chỗ đứng gốc của viên ngọc, cũng ghi lại ở lần diễn đầu tiên.
        private Vector2 _jewelBasePosition;
        private bool _hasJewelBase;

        /// Bề rộng gốc của chính ô này. Cùng khuôn nhớ-một-lần như hai cái trên.
        private float _layoutBaseWidth;
        private bool _hasLayoutBase;

        /// Đang trong màn diễn "tô xong màu này".
        ///
        /// Cần một cờ riêng vì RefreshRaisedLook có thể chạy giữa chừng — booster tô tự do
        /// tắt đúng lúc đó chẳng hạn — và nó sẽ bật lại icon cùng bóng mà màn diễn vừa cố
        /// tình giấu đi.
        private bool _hidingForComplete;

        /// Cỡ gốc của dấu tick. Cùng khuôn nhớ-một-lần, và nó PHẢI theo khuôn đó.
        ///
        /// Bản trước đọc thẳng tickTransform.localScale ở đầu mỗi màn diễn, trong khi
        /// chính màn diễn lại để dấu tick nằm lại ở base x Tick End Scale. Lần sau đọc
        /// trúng con số đã phóng to ấy rồi nhân tiếp — dấu tick to lên 1.6 lần sau mỗi
        /// màu hoàn thành, và không có gì kéo nó về.
        private Vector3 _tickBaseScale = Vector3.one;
        private bool _hasTickBase;

        private RectTransform _shadowRect;
        private Vector3 _shadowBaseScale;
        private bool _hasShadowBase;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// Đã soi cách dựng cú loé chưa. Chỉ soi MỘT lần cho mỗi ô: lời cảnh báo lặp lại
        /// mỗi màu hoàn thành thì chẳng ai đọc nữa.
        private bool _burstChecked;
#endif

        /// Hai lý do ĐỘC LẬP để viên ngọc nhô lên, giữ riêng ra chứ không gộp thành một
        /// cờ: "ô này đang được chọn" và "booster đang cho tô mọi màu". Gộp lại thì lúc
        /// booster tắt, ô đang chọn cũng bị hạ xuống theo — trong khi nó vẫn đang được
        /// chọn và vẫn phải nhô.
        private bool _selected;
        private bool _raised;

        public int PaletteIndex { get; private set; } = -1;

        /// Tâm ô màu trong world. Đã gồm cả phần nhô lên khi ô được chọn, vì ColorImage
        /// nằm trong Content — chính object bị nâng.
        ///
        /// Dùng TransformPoint(rect.center) chứ không lấy thẳng transform.position:
        /// position là điểm PIVOT, chỉ trùng tâm khi pivot đúng giữa.
        public Vector3 ColorCenterWorldPosition
        {
            get
            {
                if (_colorImage == null) return transform.position;

                var rect = (RectTransform)_colorImage.transform;

                return rect.TransformPoint(rect.rect.center);
            }
        }

        public void Bind(int paletteIndex, Color32 color, Action<int> onClicked)
        {
            // Dọn trước mọi thứ: ô này có thể vừa diễn xong màn tô hết màu ở màn trước và
            // đang nằm ở cỡ 0. Coroutine chết giữa chừng khi object bị tắt, nên không thể
            // trông vào việc nó tự trả cỡ về.
            StopComplete();
            ResetCompleteVisuals();

            PaletteIndex = paletteIndex;
            _onClicked = onClicked;
            _displayedRemaining = -1;
            _displayedProgress = -1f;
            _selected = false;
            _raised = false;

            _colorImage.color = color;

            _numberText.color = _textColor;
            _numberText.text = $"{paletteIndex + 1}";

            if (_remainingText != null) _remainingText.color = _textColor;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
        }

        /// Gỡ ô khỏi mọi màu. Gọi lúc dựng lại thanh màu, để ô cũ chưa được dùng lại
        /// không còn mang chỉ số màu của màn trước — nếu còn, việc tra ô theo chỉ số
        /// màu sẽ khớp nhầm vào nó.
        public void Unbind()
        {
            PaletteIndex = -1;
            _onClicked = null;
            _displayedRemaining = -1;
            _displayedProgress = -1f;
        }

        /// Chỉ SetText khi số đổi — tránh sinh rác mỗi lần bảng cập nhật.
        public void SetRemaining(int remaining)
        {
            if (_remainingText == null) return;
            if (remaining == _displayedRemaining) return;

            _displayedRemaining = remaining;
            _remainingText.SetText("{0}", remaining);
        }

        /// progress 0..1. Chỉ gán khi giá trị đổi — fillAmount làm Canvas dirty, mà lúc
        /// kéo tay tô thì hàm này bị gọi liên tục.
        public void SetProgress(float progress)
        {
            if (_progressRing == null) return;

            var clamped = Mathf.Clamp01(progress);
            if (Mathf.Approximately(clamped, _displayedProgress)) return;

            _displayedProgress = clamped;
            _progressRing.fillAmount = clamped;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;

            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);

            RefreshRaisedLook();
        }

        /// Nhấc viên ngọc lên mà KHÔNG đánh dấu ô là đang được chọn.
        ///
        /// Dùng cho booster tô tự do: lúc đó mọi màu đều tô được, nên mọi viên cùng nhô
        /// lên. VIỀN thì vẫn chỉ nằm trên đúng một ô — "màu đang chọn" vẫn còn nghĩa, vì
        /// booster tắt là quay lại tô bằng đúng màu đó.
        public void SetRaised(bool raised)
        {
            _raised = raised;

            RefreshRaisedLook();
        }

        /// Mọi thứ đi theo tư thế NHÔ LÊN, dù nhô vì được chọn hay vì booster.
        ///
        /// Selected Icon nằm ở đây chứ không nằm cùng Selected Highlight: nó được dựng
        /// để đặt trên đầu viên ngọc, nên nó thuộc về động tác nhấc lên chứ không phải
        /// về việc đánh dấu màu nào đang chọn.
        private void RefreshRaisedLook()
        {
            var up = _selected || _raised;

            // Màn diễn tô-xong đang chạy thì icon và bóng phải nằm im ở trạng thái ẩn.
            // Xem chú thích ở _hidingForComplete.
            if (_hidingForComplete)
            {
                if (_selectedIcon != null) _selectedIcon.SetActive(false);
                if (_shadow != null) _shadow.enabled = false;

                if (_progressRing != null) _progressRing.enabled = up;

                ApplyRise(up);
                return;
            }

            if (_selectedIcon != null) _selectedIcon.SetActive(up);

            // Vòng tiến độ cũng theo tư thế nhô, không theo việc được chọn: booster tô
            // tự do cho tô MỌI màu, nên người chơi cần thấy từng màu còn bao nhiêu để
            // biết nên nhắm vào đâu trong ngần ấy giây.
            //
            // Tắt component thay vì SetActive để không kích hoạt lại cả cây con mỗi lần
            // đổi màu.
            if (_progressRing != null) _progressRing.enabled = up;

            ApplyRise(up);
            ApplyShadow(up);
        }

        /// Màn diễn khi màu này vừa được tô xong: ô thu nhỏ dần rồi biến mất, cùng lúc
        /// một dấu tick to dần ra rồi mờ đi.
        ///
        /// Bên gọi tự quyết định làm gì lúc xong — lớp này không tự ẩn mình. Ô màu thuộc
        /// về thanh chọn màu, mà thanh còn phải sắp lại các ô sau khi mất một cái; để ô
        /// tự tắt thì thanh không biết lúc nào mà sắp.
        ///
        /// Object đang tắt thì gọi thẳng onFinished: coroutine không chạy trên object tắt,
        /// và nuốt mất lời gọi lại ở đây nghĩa là thanh màu đứng đợi một tín hiệu không
        /// bao giờ tới.
        public void PlayComplete(Action onFinished)
        {
            if (!isActiveAndEnabled)
            {
                onFinished?.Invoke();
                return;
            }

            StopComplete();

            _complete = StartCoroutine(CompleteRoutine(onFinished));
        }

        /// Bề rộng mà Horizontal Layout Group đọc để chừa chỗ cho ô này.
        ///
        /// Thanh màu dùng nó để KHÉP dần khe hở khi một màu tô xong, thay vì để layout
        /// group đóng phựt một cái lúc ô bị tắt. Lớp này chỉ giữ hộ con số gốc — quyết
        /// định khép nhanh chậm ra sao là chuyện của thanh.
        public float LayoutBaseWidth
        {
            get
            {
                CacheLayoutBase();

                return _layoutBaseWidth;
            }
        }

        public void SetLayoutWidth(float width)
        {
            CacheLayoutBase();

            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private void CacheLayoutBase()
        {
            if (_hasLayoutBase) return;

            _layoutBaseWidth = ((RectTransform)transform).sizeDelta.x;
            _hasLayoutBase = true;
        }

        /// Để trống ô Shrink Target thì thu chính ô màu này.
        private Transform ShrinkTarget => _completeShrinkTarget != null ? _completeShrinkTarget : transform;

        private void StopComplete()
        {
            if (_complete == null) return;

            StopCoroutine(_complete);
            _complete = null;
        }

        private IEnumerator CompleteRoutine(Action onFinished)
        {
            var target = ShrinkTarget;

            if (!_hasCompleteBase)
            {
                _completeBaseScale = target.localScale;
                _hasCompleteBase = true;
            }

            var baseScale = _completeBaseScale;

            var jewelRect = _completeJewel != null ? (RectTransform)_completeJewel.transform : null;
            var jewelColor = _completeJewel != null ? _completeJewel.color : default;

            if (jewelRect != null && !_hasJewelBase)
            {
                _jewelBasePosition = jewelRect.anchoredPosition;
                _hasJewelBase = true;
            }

            var tickTransform = _completeTick != null ? _completeTick.transform : null;

            if (tickTransform != null && !_hasTickBase)
            {
                _tickBaseScale = tickTransform.localScale;
                _hasTickBase = true;
            }

            var tickBaseScale = _tickBaseScale;
            var tickColor = _completeTick != null ? _completeTick.color : default;

            // Tick chưa bật vội — nó chờ tới mốc Tick Delay. Bật sẵn từ đầu thì nó nằm
            // đó ở cỡ khởi đầu suốt quãng ô đang co lại, và cái đứng im đó chính là thứ
            // phá mất nhịp trước-sau.
            var tickShown = false;

            var duration = Mathf.Max(0.01f, _completeDuration);

            // Giấu icon và bóng NGAY từ frame đầu, vì viên ngọc bắt đầu bay lên từ đó.
            // Để chúng ở lại thì viên ngọc bay đi mà cái bóng của nó vẫn nằm dưới, và
            // icon thì lơ lửng trên một chỗ trống.
            _hidingForComplete = true;

            if (_selectedIcon != null) _selectedIcon.SetActive(false);
            if (_shadow != null) _shadow.enabled = false;

            WarnBurstSetupOnce();

            var burstUsable = _completeBurst != null
                              && _completeBurstClip != null
                              && _completeBurstClip.IsUsable;

            var burstFrames = burstUsable ? _completeBurstClip.FrameCount : 0;
            var burstRate = burstUsable ? _completeBurstClip.Fps * Mathf.Max(0.01f, _burstSpeed) : 0f;
            var burstStart = Mathf.Clamp01(_burstDelay) * duration;
            var burstFrame = -1;
            var burstShown = false;

            // Cú loé có quyền dài hơn phần còn lại của màn diễn, nên vòng lặp phải sống
            // tới khi nó chạy hết. Cắt ngang ở mốc Complete Duration thì hoạt ảnh cụt
            // đúng đoạn đẹp nhất, mà bên gọi lại đi ẩn ô ngay sau đó.
            //
            // t vẫn kẹp ở 1 nên mọi thứ khác đã diễn xong chỉ đứng yên, không diễn lố.
            var total = burstFrames > 0
                ? Mathf.Max(duration, burstStart + burstFrames / burstRate)
                : duration;

            var elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / duration);

                // Cú thu nhỏ chạy trên ĐỒNG HỒ RIÊNG, kết thúc ở mốc Shrink Portion.
                // Chia lại như vậy thay vì rút ngắn cả màn diễn: dấu tick vẫn được trọn
                // Complete Duration để diễn, chỉ là ô đã đi trước.
                var shrink = Mathf.Clamp01(t / _shrinkPortion);

                // InBack: ô phình ra một chút rồi mới hút vào. Thu thẳng tuột thì nó chỉ
                // đọc ra là "biến mất", còn cú phình nhẹ ấy là thứ làm nó đọc ra thành
                // "xong rồi, cất đi".
                target.localScale = baseScale * (1f - DOVirtual.EasedValue(0f, 1f, shrink, Ease.InBack));

                if (jewelRect != null)
                {
                    var jewel = Mathf.Clamp01(t / _jewelPortion);

                    // Bay lên theo OutCubic: vọt lên rồi chậm dần, đúng nhịp của một vật
                    // được thả ra chứ không phải bị kéo.
                    jewelRect.anchoredPosition = _jewelBasePosition + new Vector2(
                        0f, _jewelRise * DOVirtual.EasedValue(0f, 1f, jewel, Ease.OutCubic));

                    // Mờ theo InQuad: giữ rõ ở nửa đầu rồi mới tan nhanh. Mờ tuyến tính
                    // thì viên ngọc nhạt đi ngay từ lúc còn chưa nhúc nhích, và cú bay
                    // lên coi như không ai thấy.
                    var color = jewelColor;
                    color.a = jewelColor.a * (1f - DOVirtual.EasedValue(0f, 1f, jewel, Ease.InQuad));
                    _completeJewel.color = color;
                }

                if (_completeTick != null && t >= _tickDelay)
                {
                    if (!tickShown)
                    {
                        tickShown = true;
                        _completeTick.gameObject.SetActive(true);
                    }

                    // Đồng hồ riêng của dấu tick: 0 là lúc nó vừa hiện, 1 là lúc màn diễn
                    // khép lại. Đo theo t thì Tick Delay càng lớn nó càng bị dồn, mà đó
                    // đúng là thứ người chỉnh không hề muốn.
                    var tick = Mathf.Clamp01((t - _tickDelay) / Mathf.Max(0.01f, 1f - _tickDelay));

                    tickTransform.localScale = tickBaseScale * Mathf.LerpUnclamped(
                        _tickStartScale, _tickEndScale, DOVirtual.EasedValue(0f, 1f, tick, Ease.OutCubic));

                    // Mờ dần chỉ ở đoạn SAU. Mờ ngay từ đầu thì dấu tick chưa kịp to ra
                    // đã nhạt, và mắt không đọc ra được đó là hình gì.
                    var fade = Mathf.Clamp01((tick - _tickFadeStart) / Mathf.Max(0.01f, 1f - _tickFadeStart));

                    var color = tickColor;
                    color.a = 1f - fade;
                    _completeTick.color = color;
                }

                if (burstFrames > 0 && elapsed >= burstStart)
                {
                    if (!burstShown)
                    {
                        burstShown = true;
                        ShowBurst();
                    }

                    var frame = (int)((elapsed - burstStart) * burstRate);

                    if (frame >= burstFrames)
                    {
                        if (_completeBurst.gameObject.activeSelf)
                        {
                            _completeBurst.gameObject.SetActive(false);
                        }
                    }
                    else if (frame != burstFrame)
                    {
                        // Chỉ gán khi số khung THẬT SỰ nhảy: gán lại đúng cái sprite cũ
                        // vẫn làm Canvas dirty và dựng lại quad, mà ở 30 khung/giây thì
                        // phần lớn frame là gán thừa.
                        burstFrame = frame;
                        _completeBurst.sprite = _completeBurstClip.Frame(frame);
                    }
                }

                yield return null;
            }

            _complete = null;

            // Dấu tick đã tan hết alpha, tắt object đi cho sạch.
            if (_completeTick != null) _completeTick.gameObject.SetActive(false);
            if (_completeBurst != null) _completeBurst.gameObject.SetActive(false);

            // KHÔNG trả cỡ, chỗ đứng và alpha về ở đây.
            //
            // Bên gọi có quyền giữ ô này HIỆN thêm một lúc nữa — thanh màu khép khe hở
            // xong mới tắt nó, mất thêm vài phần mười giây. Trả về ngay tại đây thì cái
            // nền bung lại nguyên cỡ và viên ngọc sáng lại đúng lúc thanh đang trượt:
            // một cú nháy rõ mồn một.
            //
            // Việc dọn dẹp thuộc về ResetCompleteVisuals, chạy ở Bind — tức là ngay trước
            // lần ô này được dùng lại, và lúc đó nó đang tắt nên không ai thấy gì.
            onFinished?.Invoke();
        }

        /// Bật cú loé và ép nó về đúng trạng thái NHÌN THẤY ĐƯỢC.
        ///
        /// Không chỉ SetActive: một Image nằm im trong prefab hay bị người dựng tắt
        /// component, hay bị kéo alpha về 0 trong lúc ngắm nghía, và cả hai thứ đó sống
        /// sót qua mọi lần chạy sau. Triệu chứng giống hệt "chưa gán gì cả" nên cực khó
        /// truy — đặt lại cả ba ở đây rẻ hơn nhiều so với một buổi đi tìm.
        ///
        /// Kéo xuống làm con ÚT để vẽ sau cùng: cú loé mà chui xuống dưới viên ngọc hay
        /// dưới cái nền thì cũng coi như không có.
        private void ShowBurst()
        {
            if (_completeBurst == null) return;

            _completeBurst.enabled = true;

            var color = _completeBurst.color;
            color.a = 1f;
            _completeBurst.color = color;

            _completeBurst.transform.SetAsLastSibling();
            _completeBurst.gameObject.SetActive(true);
        }

        /// Soi cách dựng cú loé và kêu lên nếu nó sẽ không bao giờ hiện.
        ///
        /// Ba cái bẫy dưới đây đều cho ra cùng một kết quả: hiệu ứng đã gán đủ, code đã
        /// chạy đủ, mà màn hình không có gì. Không ai đoán ra được nếu engine im lặng,
        /// nên thà ồn một lần trong Console.
        private void WarnBurstSetupOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_burstChecked) return;
            _burstChecked = true;

            // Gán một nửa cũng là chưa gán: thiếu bên nào thì cả cú loé im lặng biến mất.
            if (_completeBurst != null && (_completeBurstClip == null || !_completeBurstClip.IsUsable))
            {
                Debug.LogWarning(
                    $"[{name}] Đã gán Complete Burst nhưng Complete Burst Clip trống hoặc " +
                    "không có khung nào — cú loé sẽ không hiện.", this);
                return;
            }

            if (_completeBurst == null)
            {
                if (_completeBurstClip != null)
                {
                    Debug.LogWarning(
                        $"[{name}] Đã gán Complete Burst Clip nhưng ô Complete Burst trống " +
                        "— chưa có Image nào để vẽ cú loé.", this);
                }

                return;
            }

            var burst = _completeBurst.transform;

            // Bẫy nặng nhất, và là bẫy mặc định: để trống ô Shrink Target thì Shrink
            // Target chính là cả ô màu này, và MỌI thứ bên trong — kể cả cú loé — bị thu
            // về cỡ 0 trước khi tới lượt nó diễn.
            if (burst.IsChildOf(ShrinkTarget))
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm TRONG Shrink Target nên nó bị thu về " +
                    "cỡ 0 đúng lúc phải loé. Kéo nó ra ngoài Shrink Target.", this);
            }

            // Bẫy im lặng nhất: hiệu ứng CÓ chạy, chỉ là phần tràn ra ngoài khung Scroll
            // Rect bị Mask xén mất, và thứ còn lại nhỏ tới mức trông như không có gì.
            if (_completeBurst.maskable
                && (_completeBurst.GetComponentInParent<Mask>() != null
                    || _completeBurst.GetComponentInParent<RectMask2D>() != null))
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm trong một Mask (Scroll Rect của " +
                    "thanh màu) và vẫn bật Maskable, nên phần tràn ra ngoài khung bị cắt. " +
                    "Bỏ tick Maskable trên Image đó nếu muốn cú loé tràn trọn vẹn.", this);
            }

            // Alpha của Graphic không lan xuống con, nhưng CanvasGroup thì có — và viên
            // ngọc tan đi bằng alpha. Nằm dưới một cái đang tan thì cú loé tan theo.
            if (_completeJewel != null && burst.IsChildOf(_completeJewel.transform)
                                       && burst != _completeJewel.transform)
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm TRONG Complete Jewel — nó sẽ bay lên " +
                    "và mờ đi theo viên ngọc thay vì thế chỗ viên ngọc.", this);
            }
#endif
        }

        /// Trả ô về đúng hình dạng lúc chưa diễn gì. Gọi ở Bind chứ không chỉ ở cuối màn
        /// diễn: màn diễn có thể đã bị cắt ngang giữa chừng.
        private void ResetCompleteVisuals()
        {
            // Trả icon và bóng về trước mọi thứ khác: màn diễn có thể đã bị cắt ngang khi
            // object bị tắt, và lúc đó không có ai chạy đoạn dọn ở cuối coroutine.
            _hidingForComplete = false;

            if (_selectedIcon != null) _selectedIcon.SetActive(_selected || _raised);
            if (_shadow != null) _shadow.enabled = true;

            if (_completeBurst != null)
            {
                // Bỏ sprite của lần loé trước. Giữ lại thì frame đầu của lần sau còn đeo
                // khung CUỐI của lần trước cho tới khi số khung nhảy — đúng một cú nháy.
                _completeBurst.sprite = null;
                _completeBurst.gameObject.SetActive(false);
            }

            // Chưa diễn lần nào thì chưa biết cỡ gốc — và cũng chưa có gì để trả về, cỡ
            // hiện tại chính là cỡ prefab. Ép về 1 ở đây là đoán mò.
            if (_hasCompleteBase) ShrinkTarget.localScale = _completeBaseScale;

            // Trả cả bề rộng: cú khép có thể đã bị cắt ngang giữa chừng (đổi màn chẳng
            // hạn), và một ô bị bỏ lại ở bề rộng âm sẽ kéo lệch cả thanh ở màn sau.
            if (_hasLayoutBase) SetLayoutWidth(_layoutBaseWidth);

            if (_completeJewel != null)
            {
                if (_hasJewelBase) ((RectTransform)_completeJewel.transform).anchoredPosition = _jewelBasePosition;

                // Trả alpha về 1 chứ không nhớ màu cũ: Bind gán lại màu ngọc ngay sau lời
                // gọi này, nên giá trị duy nhất cần đúng ở đây là độ đục.
                var jewel = _completeJewel.color;
                jewel.a = 1f;
                _completeJewel.color = jewel;
            }

            if (_completeTick == null) return;

            // Trả cỡ về, không chỉ alpha. Màn diễn để dấu tick nằm lại ở cỡ đã phóng to,
            // và ô này còn được dùng lại cho màu khác ở màn sau.
            if (_hasTickBase) _completeTick.transform.localScale = _tickBaseScale;

            var color = _completeTick.color;
            color.a = 1f;
            _completeTick.color = color;

            _completeTick.gameObject.SetActive(false);
        }

        /// Bóng loang rộng ra và nhạt đi khi viên đá được nhấc lên.
        ///
        /// Hai thứ phải đi CÙNG NHAU mới ra được cảm giác cao thấp. Chỉ phóng to bóng thì
        /// nhìn như viên đá to ra tại chỗ; chỉ làm nhạt thì như bóng sắp tắt. Rộng cộng
        /// nhạt mới là thứ mắt đọc thành 'nó đang lơ lửng'.
        private void ApplyShadow(bool selected)
        {
            if (_shadow == null) return;

            // Ghi lại cỡ gốc ở lần gọi ĐẦU TIÊN, cùng lý do đã ghi ở ApplyRise: lúc Awake
            // layout chưa chạy, và prefab vốn có thể không ở cỡ 1.
            if (!_hasShadowBase)
            {
                _shadowRect = (RectTransform)_shadow.transform;
                _shadowBaseScale = _shadowRect.localScale;
                _hasShadowBase = true;
            }

            _shadowRect.localScale = selected
                ? _shadowBaseScale * Mathf.Max(0.01f, _selectedShadowScale)
                : _shadowBaseScale;

            // Chỉ đụng alpha, giữ nguyên RGB: màu bóng do prefab đặt, và đó là thứ designer
            // chỉnh. Ghi đè cả màu ở đây là lấy mất quyền đó mà không nói một tiếng.
            var color = _shadow.color;
            color.a = selected ? _selectedShadowAlpha : _shadowAlpha;
            _shadow.color = color;
        }

        private void ApplyRise(bool selected)
        {
            if (_riseTarget == null) return;

            // Ghi lại vị trí và cỡ gốc ở lần gọi ĐẦU TIÊN, không phải trong Awake: lúc
            // Awake layout chưa chạy nên toạ độ chưa đúng.
            //
            // Nhớ cỡ gốc chứ không trả về 1: prefab có thể vốn không ở cỡ 1, và đặt bừa
            // là mọi ô màu đổi cỡ vĩnh viễn ngay lần bỏ chọn đầu tiên.
            if (!_hasRiseBase)
            {
                _riseBasePosition = _riseTarget.anchoredPosition;
                _riseBaseScale = _riseTarget.localScale;
                _hasRiseBase = true;
            }

            _riseTarget.anchoredPosition = selected
                ? _riseBasePosition + new Vector2(0f, _selectedRise)
                : _riseBasePosition;

            _riseTarget.localScale = selected
                ? _riseBaseScale * Mathf.Max(0.01f, _selectedScale)
                : _riseBaseScale;
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }

        private void HandleClick() => _onClicked?.Invoke(PaletteIndex);
    }
}
