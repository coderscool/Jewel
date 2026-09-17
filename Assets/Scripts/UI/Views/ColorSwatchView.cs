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

        [Tooltip("Màu chữ số của ô màu.")]
        [SerializeField] private Color _textColor = Color.black;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Số ô còn lại của màu này.")]
        [SerializeField] private TMP_Text _remainingText;

        [Tooltip("Viền báo màu đang được chọn.")]
        [SerializeField] private GameObject _selectedHighlight;

        [Tooltip("Dấu hiệu hiện trên đầu viên ngọc khi ô được nhấc lên.")]
        [SerializeField] private GameObject _selectedIcon;

        [Tooltip("Vòng tròn tiến độ.")]
        [SerializeField] private Image _progressRing;

        [Tooltip("Object được nâng lên khi ô này được chọn.")]
        [SerializeField] private RectTransform _riseTarget;

        [Tooltip("Nâng lên bao nhiêu pixel khi được chọn.")]
        [SerializeField] private float _selectedRise = 24f;

        [Tooltip("Phóng to bao nhiêu lần khi được chọn.")]
        [SerializeField] private float _selectedScale = 1.25f;

        [Header("Bóng đổ — để trống cũng chạy")]
        [Tooltip("Vệt bóng mờ dưới chân viên đá.")]
        [SerializeField] private Graphic _shadow;

        [Tooltip("Độ nhấc của bóng khi ô được chọn, tính bằng pixel.")]
        [SerializeField] private float _selectedShadowRise = 24f;

        [Tooltip("Cỡ bóng khi ô được chọn, so với lúc thường.")]
        [SerializeField] private float _selectedShadowScale = 1.4f;

        [Tooltip("Độ đục của bóng khi không được chọn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shadowAlpha = 0.35f;

        [Tooltip("Độ đục của bóng khi được chọn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _selectedShadowAlpha = 0.22f;

        [Header("Tô xong màu — để trống cả hai thì ẩn ngay như cũ")]
        [Tooltip("Object thu nhỏ rồi biến mất khi màu tô xong.")]
        [SerializeField] private RectTransform _completeShrinkTarget;

        [Tooltip("Viên ngọc nâng lên rồi mờ dần khi màu tô xong.")]
        [SerializeField] private Graphic _completeJewel;

        [Tooltip("Viên ngọc nâng lên bao nhiêu pixel trước khi tan hẳn.")]
        [SerializeField] private float _jewelRise = 60f;

        [Tooltip("Tỉ lệ thời gian của cú bay lên và mờ dần của viên ngọc.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _jewelPortion = 0.5f;

        [Tooltip("Dấu tick to dần rồi mờ đi khi màu tô xong.")]
        [SerializeField] private Graphic _completeTick;

        [Tooltip("Cả màn diễn kéo dài bao nhiêu giây.")]
        [SerializeField] private float _completeDuration = 0.65f;

        [Tooltip("Tỉ lệ thời gian của cú thu nhỏ.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _shrinkPortion = 0.4f;

        [Tooltip("Tỉ lệ thời gian chờ trước khi dấu tick hiện.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _tickDelay = 0.3f;

        [Tooltip("Mốc báo xong ra ngoài, tính theo tỉ lệ Complete Duration.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _handoffPortion = 0.8f;

        [Tooltip("Cỡ dấu tick lúc bắt đầu, so với cỡ trong prefab.")]
        [SerializeField] private float _tickStartScale = 0.4f;

        [SerializeField] private float _tickEndScale = 1.6f;

        [Tooltip("Mốc dấu tick bắt đầu mờ, tính theo tỉ lệ thời gian hiện của nó.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _tickFadeStart = 0.35f;

        [Header("Cú loé lúc viên ngọc tan")]
        [Tooltip("Image vẽ hoạt ảnh loé.")]
        [SerializeField] private Image _completeBurst;

        [Tooltip("Flipbook Clip của cú loé.")]
        [SerializeField] private FlipbookClip _completeBurstClip;

        [Tooltip("Thời điểm loé, tính theo tỉ lệ Complete Duration.")]
        [Range(0f, 1f)]
        [SerializeField] private float _burstDelay = 0.5f;

        [Tooltip("Nhân vào tốc độ phát.")]
        [SerializeField] private float _burstSpeed = 1f;

        private Action<int> _onClicked;
        private int _displayedRemaining = -1;
        private float _displayedProgress = -1f;

        private Vector2 _riseBasePosition;
        private Vector3 _riseBaseScale;
        private bool _hasRiseBase;

        private Coroutine _complete;

        private Vector3 _completeBaseScale = Vector3.one;
        private bool _hasCompleteBase;

        private Vector2 _jewelBasePosition;
        private bool _hasJewelBase;

        private float _layoutBaseWidth;
        private bool _hasLayoutBase;

        private bool _hidingForComplete;

        private Vector3 _tickBaseScale = Vector3.one;
        private bool _hasTickBase;

        private RectTransform _shadowRect;
        private Vector3 _shadowBaseScale;

        private Vector2 _shadowBasePosition;
        private bool _hasShadowBase;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _burstChecked;
#endif

        private bool _selected;
        private bool _raised;

        public int PaletteIndex { get; private set; } = -1;

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

        /// Gỡ ô khỏi mọi màu.
        public void Unbind()
        {
            PaletteIndex = -1;
            _onClicked = null;
            _displayedRemaining = -1;
            _displayedProgress = -1f;
        }

        /// Hiện số ô còn lại.
        public void SetRemaining(int remaining)
        {
            if (_remainingText == null) return;
            if (remaining == _displayedRemaining) return;

            _displayedRemaining = remaining;
            _remainingText.SetText("{0}", remaining);
        }

        /// Hiện tiến độ tô, thang 0..1.
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

        /// Nhấc viên ngọc lên mà không đánh dấu ô là đang được chọn.
        public void SetRaised(bool raised)
        {
            _raised = raised;

            RefreshRaisedLook();
        }

        /// Cập nhật hình ảnh theo tư thế nhấc lên.
        private void RefreshRaisedLook()
        {
            var up = _selected || _raised;

            if (_hidingForComplete)
            {
                if (_selectedIcon != null) _selectedIcon.SetActive(false);
                if (_shadow != null) _shadow.enabled = false;

                if (_progressRing != null) _progressRing.enabled = up;

                ApplyRise(up);
                return;
            }

            if (_selectedIcon != null) _selectedIcon.SetActive(up);

            if (_progressRing != null) _progressRing.enabled = up;

            ApplyRise(up);
            ApplyShadow(up);
        }

        /// Diễn hiệu ứng khi màu này được tô xong.
        public void PlayComplete(Action onFinished, float burstDelaySeconds = -1f)
        {
            if (!isActiveAndEnabled)
            {
                onFinished?.Invoke();
                return;
            }

            StopComplete();

            _complete = StartCoroutine(CompleteRoutine(onFinished, burstDelaySeconds));
        }

        public bool IsPlayingComplete => _complete != null && isActiveAndEnabled;

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

        private Transform ShrinkTarget => _completeShrinkTarget != null ? _completeShrinkTarget : transform;

        private void StopComplete()
        {
            if (_complete == null) return;

            StopCoroutine(_complete);
            _complete = null;
        }

        private IEnumerator CompleteRoutine(Action onFinished, float burstDelaySeconds)
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

            var tickShown = false;

            var duration = Mathf.Max(0.01f, _completeDuration);

            _hidingForComplete = true;

            if (_selectedIcon != null) _selectedIcon.SetActive(false);
            if (_shadow != null) _shadow.enabled = false;

            WarnBurstSetupOnce();

            var burstUsable = _completeBurst != null
                              && _completeBurstClip != null
                              && _completeBurstClip.IsUsable;

            var burstFrames = burstUsable ? _completeBurstClip.FrameCount : 0;
            var burstRate = burstUsable ? _completeBurstClip.Fps * Mathf.Max(0.01f, _burstSpeed) : 0f;
            var burstStart = burstDelaySeconds >= 0f
                ? burstDelaySeconds
                : Mathf.Clamp01(_burstDelay) * duration;
            var burstFrame = -1;
            var burstShown = false;

            var total = burstFrames > 0
                ? Mathf.Max(duration, burstStart + burstFrames / burstRate)
                : duration;

            var elapsed = 0f;

            var handoff = duration * Mathf.Clamp01(_handoffPortion);
            var handedOff = false;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / duration);

                var shrink = Mathf.Clamp01(t / _shrinkPortion);

                target.localScale = baseScale * (1f - DOVirtual.EasedValue(0f, 1f, shrink, Ease.InBack));

                if (jewelRect != null)
                {
                    var jewel = Mathf.Clamp01(t / _jewelPortion);

                    jewelRect.anchoredPosition = _jewelBasePosition + new Vector2(
                        0f, _jewelRise * DOVirtual.EasedValue(0f, 1f, jewel, Ease.OutCubic));

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

                    var tick = Mathf.Clamp01((t - _tickDelay) / Mathf.Max(0.01f, 1f - _tickDelay));

                    tickTransform.localScale = tickBaseScale * Mathf.LerpUnclamped(
                        _tickStartScale, _tickEndScale, DOVirtual.EasedValue(0f, 1f, tick, Ease.OutCubic));

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
                        burstFrame = frame;
                        _completeBurst.sprite = _completeBurstClip.Frame(frame);
                    }
                }

                if (!handedOff && elapsed >= handoff)
                {
                    handedOff = true;

                    onFinished?.Invoke();
                }

                yield return null;
            }

            _complete = null;

            if (_completeTick != null) _completeTick.gameObject.SetActive(false);
            if (_completeBurst != null) _completeBurst.gameObject.SetActive(false);

            if (!handedOff) onFinished?.Invoke();
        }

        /// Bật cú loé và ép nó về đúng trạng thái nhìn thấy được.
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

        /// Cảnh báo một lần khi cú loé dựng sai.
        private void WarnBurstSetupOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_burstChecked) return;
            _burstChecked = true;

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

            if (burst.IsChildOf(ShrinkTarget))
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm TRONG Shrink Target nên nó bị thu về " +
                    "cỡ 0 đúng lúc phải loé. Kéo nó ra ngoài Shrink Target.", this);
            }

            if (_completeBurst.maskable
                && (_completeBurst.GetComponentInParent<Mask>() != null
                    || _completeBurst.GetComponentInParent<RectMask2D>() != null))
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm trong một Mask (Scroll Rect của " +
                    "thanh màu) và vẫn bật Maskable, nên phần tràn ra ngoài khung bị cắt. " +
                    "Bỏ tick Maskable trên Image đó nếu muốn cú loé tràn trọn vẹn.", this);
            }

            if (_completeJewel != null && burst.IsChildOf(_completeJewel.transform)
                                       && burst != _completeJewel.transform)
            {
                Debug.LogWarning(
                    $"[{name}] Complete Burst đang nằm TRONG Complete Jewel — nó sẽ bay lên " +
                    "và mờ đi theo viên ngọc thay vì thế chỗ viên ngọc.", this);
            }
#endif
        }

        /// Trả ô về đúng hình dạng lúc chưa diễn gì.
        private void ResetCompleteVisuals()
        {
            _hidingForComplete = false;

            if (_selectedIcon != null) _selectedIcon.SetActive(_selected || _raised);
            if (_shadow != null) _shadow.enabled = true;

            if (_completeBurst != null)
            {
                _completeBurst.sprite = null;
                _completeBurst.gameObject.SetActive(false);
            }

            if (_hasCompleteBase) ShrinkTarget.localScale = _completeBaseScale;

            if (_hasLayoutBase) SetLayoutWidth(_layoutBaseWidth);

            if (_completeJewel != null)
            {
                if (_hasJewelBase) ((RectTransform)_completeJewel.transform).anchoredPosition = _jewelBasePosition;

                var jewel = _completeJewel.color;
                jewel.a = 1f;
                _completeJewel.color = jewel;
            }

            if (_completeTick == null) return;

            if (_hasTickBase) _completeTick.transform.localScale = _tickBaseScale;

            var color = _completeTick.color;
            color.a = 1f;
            _completeTick.color = color;

            _completeTick.gameObject.SetActive(false);
        }

        /// Bóng loang rộng ra và nhạt đi khi viên đá được nhấc lên.
        private void ApplyShadow(bool selected)
        {
            if (_shadow == null) return;

            if (!_hasShadowBase)
            {
                _shadowRect = (RectTransform)_shadow.transform;
                _shadowBaseScale = _shadowRect.localScale;
                _shadowBasePosition = _shadowRect.anchoredPosition;
                _hasShadowBase = true;
            }

            _shadowRect.anchoredPosition = selected
                ? _shadowBasePosition + new Vector2(0f, _selectedShadowRise)
                : _shadowBasePosition;

            _shadowRect.localScale = selected
                ? _shadowBaseScale * Mathf.Max(0.01f, _selectedShadowScale)
                : _shadowBaseScale;

            var color = _shadow.color;
            color.a = selected ? _selectedShadowAlpha : _shadowAlpha;
            _shadow.color = color;
        }

        private void ApplyRise(bool selected)
        {
            if (_riseTarget == null) return;

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
