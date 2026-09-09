using System;
using TMPro;
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

        private Action<int> _onClicked;
        private int _displayedRemaining = -1;
        private float _displayedProgress = -1f;

        private Vector2 _riseBasePosition;
        private Vector3 _riseBaseScale;
        private bool _hasRiseBase;

        private RectTransform _shadowRect;
        private Vector3 _shadowBaseScale;
        private bool _hasShadowBase;

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
