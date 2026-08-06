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
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private Button _button;

        [Tooltip("Màu chữ số và chữ số ô còn lại, dùng chung cho mọi ô màu.")]
        [SerializeField] private Color _textColor = Color.black;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Số ô còn lại của màu này. Để trống thì không hiện số.")]
        [SerializeField] private TMP_Text _remainingText;

        [Tooltip("Viền báo màu đang được chọn. Để trống thì không có dấu hiệu chọn.")]
        [SerializeField] private GameObject _selectedHighlight;

        [Tooltip("Vòng tròn tiến độ. Image phải đặt Image Type = Filled, " +
                 "Fill Method = Radial 360 — code chỉ gán fillAmount, Unity lo phần vẽ cung.")]
        [SerializeField] private Image _progressRing;

        private Action<int> _onClicked;
        private int _displayedRemaining = -1;
        private float _displayedProgress = -1f;

        public int PaletteIndex { get; private set; } = -1;

        public void Bind(int paletteIndex, Color32 color, Action<int> onClicked)
        {
            PaletteIndex = paletteIndex;
            _onClicked = onClicked;
            _displayedRemaining = -1;
            _displayedProgress = -1f;

            _colorImage.color = color;

            _numberText.color = _textColor;
            _numberText.SetText("{0}", paletteIndex + 1);

            if (_remainingText != null) _remainingText.color = _textColor;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
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
            if (_selectedHighlight == null) return;

            _selectedHighlight.SetActive(selected);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }

        private void HandleClick() => _onClicked?.Invoke(PaletteIndex);
    }
}
