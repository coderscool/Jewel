using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Thanh chọn màu dưới màn hình.
    public class ColorPaletteBar : MonoBehaviour, IPaintOriginProvider
    {
        [SerializeField] private ColorSwatchView _swatchPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Camera của bảng.")]
        [SerializeField] private Camera _worldCamera;

        [Tooltip("Scroll Rect của thanh màu.")]
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Object bị ẩn khi thắng màn.")]
        [SerializeField] private GameObject _content;

        [Tooltip("CanvasGroup để thanh màu mờ dần lúc màn ăn mừng bắt đầu.")]
        [SerializeField] private CanvasGroup _celebrationFadeGroup;

        [Tooltip("Thời gian thanh màu mờ đi.")]
        [SerializeField] private float _celebrationFadeDuration = 0.25f;

        [Header("Cuộn tới ô màu vừa chọn")]
        [Tooltip("Thời gian cuộn thanh màu tới ô màu vừa được chọn.")]
        [SerializeField] private float _focusScrollDuration = 0.22f;

        [Tooltip("Thời gian khép khe hở sau khi một màu tô xong.")]
        [SerializeField] private float _collapseDuration = 0.3f;

        private readonly List<ColorSwatchView> _swatches = new();

        private Coroutine _relayout;
        private Coroutine _focusScroll;

        private IPaintService _paintService;
        private ILevelService _levelService;
        private ILevelFlowService _levelFlow;

        private Tween _celebrationFade;
        private JewelFlyEffect _flyEffect;
        private ISoundService _sound;

        private ColorCompleteSparkle _colorCompleteSparkle;

        [Tooltip("Thứ tự ô màu được dạy trong màn hướng dẫn, đếm từ 0.")]
        [Min(0)]
        [SerializeField] private int _tutorialSwatchOrder = 1;

        private TutorialState _tutorialState;

        private bool _selectingFromSwatch;

        private readonly HashSet<int> _completed = new();

        /// Khởi tạo phụ thuộc.
        public void Init(IPaintService paintService, ILevelService levelService, ILevelFlowService levelFlow,
            JewelFlyEffect flyEffect, ISoundService sound, TutorialState tutorialState,
            ColorCompleteSparkle colorCompleteSparkle)
        {
            _colorCompleteSparkle = colorCompleteSparkle;

            _paintService = paintService;
            _levelService = levelService;
            _levelFlow = levelFlow;
            _flyEffect = flyEffect;
            _sound = sound;
            _tutorialState = tutorialState;

            if (_tutorialState != null) _tutorialState.OnStageChanged += HandleTutorialStageChanged;

            if (_flyEffect != null) _flyEffect.OnJewelLanded += HandleJewelLanded;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnCellPainted += HandleCellPainted;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnColorFocusRequested += HandleColorFocusRequested;
            _paintService.OnFreePaintChanged += HandleFreePaintChanged;

            if (_levelFlow != null)
            {
                _levelFlow.OnCelebrationStarted += HandleCelebrationStarted;
                _levelFlow.OnLevelCleared += HandleLevelCleared;
            }
        }

        private void OnDestroy()
        {
            if (_tutorialState != null) _tutorialState.OnStageChanged -= HandleTutorialStageChanged;

            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            KillCelebrationFade();

            if (_levelFlow != null)
            {
                _levelFlow.OnCelebrationStarted -= HandleCelebrationStarted;
                _levelFlow.OnLevelCleared -= HandleLevelCleared;
            }

            if (_paintService == null) return;

            _paintService.OnBoardReady -= HandleBoardReady;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnColorFocusRequested -= HandleColorFocusRequested;
            _paintService.OnFreePaintChanged -= HandleFreePaintChanged;
        }

        /// Ẩn thanh màu khi màn ăn mừng bắt đầu.
        private void HandleCelebrationStarted()
        {
            var target = _content != null ? _content : gameObject;
            if (!target.activeSelf) return;

            if (_celebrationFadeGroup == null || _celebrationFadeDuration <= 0f)
            {
                SetVisible(false);
                return;
            }

            KillCelebrationFade();

            _celebrationFadeGroup.interactable = false;
            _celebrationFadeGroup.blocksRaycasts = false;

            _celebrationFade = DOVirtual
                .Float(_celebrationFadeGroup.alpha, 0f, _celebrationFadeDuration, value =>
                {
                    if (_celebrationFadeGroup != null) _celebrationFadeGroup.alpha = value;
                })
                .SetUpdate(true)
                .OnComplete(() => SetVisible(false));
        }

        private void HandleLevelCleared() => SetVisible(false);

        /// Trả CanvasGroup về trạng thái hiện đủ.
        private void RestoreCelebrationFade()
        {
            KillCelebrationFade();

            if (_celebrationFadeGroup == null) return;

            _celebrationFadeGroup.alpha = 1f;
            _celebrationFadeGroup.interactable = true;
            _celebrationFadeGroup.blocksRaycasts = true;
        }

        private void KillCelebrationFade()
        {
            if (_celebrationFade != null && _celebrationFade.IsActive()) _celebrationFade.Kill();

            _celebrationFade = null;
        }

        /// Ẩn hoặc hiện thanh màu.
        private void SetVisible(bool visible)
        {
            if (visible) RestoreCelebrationFade();
            else KillCelebrationFade();

            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private void HandleBoardReady()
        {
            SetVisible(!_paintService.IsComplete);

            _completed.Clear();

            HideAll();

            if (_swatchPrefab == null)
            {
                Debug.LogWarning($"{nameof(ColorPaletteBar)} chưa gán Swatch Prefab — thanh màu sẽ trống.");
                return;
            }

            var data = _levelService.CurrentGrid;
            if (data == null) return;

            var colors = _levelService.CurrentJewelColors;
            var used = _paintService.UsedPaletteIndices;

            var slot = 0;

            foreach (var paletteIndex in used)
            {
                if (paletteIndex < 0 || paletteIndex >= colors.Count) continue;

                var remaining = _paintService.RemainingFor(paletteIndex);
                if (remaining <= 0) continue;

                var swatch = GetSwatch(slot++);
                swatch.Bind(paletteIndex, colors[paletteIndex], HandleSwatchClicked);
                swatch.SetRemaining(remaining);
                swatch.SetProgress(_paintService.ProgressFor(paletteIndex));
                swatch.SetSelected(false);
                swatch.gameObject.SetActive(true);
            }

            SetAllRaised(_paintService.FreePaintActive);

            RelayoutBar();
        }

        /// Nhấc hoặc hạ mọi viên ngọc khi tô tự do bật tắt.
        private void HandleFreePaintChanged(bool active) => SetAllRaised(active);

        /// Nhấc hoặc hạ viên ngọc của mọi ô.
        private void SetAllRaised(bool raised)
        {
            foreach (var swatch in _swatches)
            {
                if (swatch == null) continue;

                swatch.SetRaised(raised);
            }
        }

        /// Sắp lại thanh: còn nhiều màu thì cuộn về ô đầu, còn ít màu thì căn giữa.
        private void RelayoutBar()
        {
            if (_scrollRect == null || !isActiveAndEnabled) return;

            if (_focusScroll != null) StopCoroutine(_focusScroll);
            if (_relayout != null) StopCoroutine(_relayout);

            _relayout = StartCoroutine(RelayoutBarRoutine());
        }

        private IEnumerator RelayoutBarRoutine()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            ApplyBarAlignment(scrollToStart: true);
        }

        /// Đặt thanh về đúng chỗ ngay lập tức theo bề rộng hiện tại của nó.
        private void ApplyBarAlignment(bool scrollToStart)
        {
            if (_scrollRect == null) return;

            var content = _scrollRect.content;
            if (content == null) return;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var fitsInViewport = content.rect.width <= viewport.rect.width;

            _scrollRect.horizontal = !fitsInViewport && !IsTutorialRunning;

            if (!fitsInViewport)
            {
                if (!scrollToStart) return;

                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition = 0f;

                return;
            }

            var contentLeft = viewport.InverseTransformPoint(
                content.TransformPoint(new Vector3(content.rect.xMin, 0f, 0f))).x;

            var delta = viewport.rect.xMin - contentLeft;

            if (Mathf.Abs(delta) < 0.01f) return;

            _scrollRect.velocity = Vector2.zero;
            content.anchoredPosition += new Vector2(delta, 0f);
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return;

            var remaining = _paintService.RemainingFor(paletteIndex);

            swatch.SetRemaining(remaining);
            swatch.SetProgress(_paintService.ProgressFor(paletteIndex));

            if (remaining <= 0 && _flyEffect == null) CompleteSwatch(paletteIndex);
        }

        /// Thu ô màu khi viên ngọc cuối của màu đáp xuống.
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_paintService.RemainingFor(paletteIndex) > 0) return;

            if (_flyEffect.HasInFlight(paletteIndex)) return;

            CompleteSwatch(paletteIndex);
        }

        /// Ô màu thu nhỏ dần rồi biến mất, xong thì thanh sắp lại.
        private void CompleteSwatch(int paletteIndex)
        {
            if (!_completed.Add(paletteIndex)) return;

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return;

            var burstDelay = _colorCompleteSparkle != null ? _colorCompleteSparkle.StartDelay : -1f;

            swatch.PlayComplete(() => StartCoroutine(CollapseRoutine(swatch, paletteIndex)), burstDelay);
        }

        /// Khép dần khe hở của ô vừa xong, rồi mới tắt nó và sắp lại thanh.
        private IEnumerator CollapseRoutine(ColorSwatchView swatch, int paletteIndex)
        {
            var duration = Mathf.Max(0f, _collapseDuration);
            var from = swatch.LayoutBaseWidth;
            var to = -LayoutSpacing();

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                swatch.SetLayoutWidth(Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration)));

                Canvas.ForceUpdateCanvases();

                ApplyBarAlignment(scrollToStart: false);

                yield return null;
            }

            while (swatch.IsPlayingComplete) yield return null;

            swatch.gameObject.SetActive(false);

            swatch.SetLayoutWidth(from);

            Canvas.ForceUpdateCanvases();

            ApplyBarAlignment(scrollToStart: false);

            OnSwatchRemoved?.Invoke(paletteIndex);
        }

        /// Spacing của Horizontal Layout Group đang xếp các ô màu.
        private float LayoutSpacing()
        {
            if (_root == null) return 0f;

            var group = _root.GetComponent<HorizontalLayoutGroup>();

            return group != null ? group.spacing : 0f;
        }

        private void HandleColorSelected(int paletteIndex)
        {
            if (_sound != null) _sound.Play(SoundKey.ChooseJewel);

            foreach (var swatch in _swatches)
            {
                if (!swatch.gameObject.activeSelf) continue;

                swatch.SetSelected(swatch.PaletteIndex == paletteIndex);
            }
        }

        /// Chọn màu khi chạm vào một ô màu.
        private void HandleSwatchClicked(int paletteIndex)
        {
            if (IsTutorialRunning)
            {
                var allowed = TutorialSwatchPaletteIndex;

                if (allowed >= 0 && paletteIndex != allowed) return;
            }

            _selectingFromSwatch = true;

            try
            {
                _paintService.SelectColor(paletteIndex);
            }
            finally
            {
                _selectingFromSwatch = false;
            }
        }

        private void HandleColorFocusRequested(int paletteIndex)
        {
            if (_selectingFromSwatch) return;

            ScrollToSwatch(paletteIndex);
        }

        /// Cuộn thanh để ô màu lọt vào tầm nhìn — chỉ khi nó đang nằm ngoài.
        private void ScrollToSwatch(int paletteIndex)
        {
            if (_scrollRect == null || !isActiveAndEnabled) return;

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null || !swatch.gameObject.activeInHierarchy) return;

            if (_focusScroll != null) StopCoroutine(_focusScroll);

            _focusScroll = StartCoroutine(ScrollToSwatchRoutine((RectTransform)swatch.transform));
        }

        private IEnumerator ScrollToSwatchRoutine(RectTransform swatch)
        {
            yield return null;

            if (_scrollRect == null || swatch == null) yield break;

            var content = _scrollRect.content;
            if (content == null) yield break;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var scrollable = content.rect.width - viewport.rect.width;
            if (scrollable <= 1f) yield break;

            var rect = swatch.rect;
            var left = content.InverseTransformPoint(
                swatch.TransformPoint(new Vector3(rect.xMin, 0f, 0f))).x - content.rect.xMin;
            var right = content.InverseTransformPoint(
                swatch.TransformPoint(new Vector3(rect.xMax, 0f, 0f))).x - content.rect.xMin;

            if (right < left)
            {
                var swap = left;
                left = right;
                right = swap;
            }

            var window = viewport.rect.width;
            var from = Mathf.Clamp01(_scrollRect.horizontalNormalizedPosition);

            var visibleLeft = from * scrollable;

            if (left >= visibleLeft && right <= visibleLeft + window) yield break;

            var windowLeft = (left + right) * 0.5f - window * 0.5f;

            var to = Mathf.Clamp01(windowLeft / scrollable);
            if (Mathf.Abs(to - from) < 0.001f) yield break;

            var duration = Mathf.Max(0f, _focusScrollDuration);

            if (duration <= 0f)
            {
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition = to;
                yield break;
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition =
                    Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));

                yield return null;
            }

            _scrollRect.velocity = Vector2.zero;
            _scrollRect.horizontalNormalizedPosition = to;
            _focusScroll = null;
        }

        public RectTransform TutorialSwatchRect
        {
            get
            {
                var swatch = VisibleSwatchAt(_tutorialSwatchOrder);

                return swatch != null ? (RectTransform)swatch.transform : null;
            }
        }

        public event Action<int> OnSwatchRemoved;

        public int TutorialSwatchPaletteIndex
        {
            get
            {
                var swatch = VisibleSwatchAt(_tutorialSwatchOrder);

                return swatch != null ? swatch.PaletteIndex : -1;
            }
        }

        /// Ô thứ `order` trong số những ô đang hiện, đếm từ 0.
        private ColorSwatchView VisibleSwatchAt(int order)
        {
            if (order < 0) return null;

            var seen = 0;

            foreach (var swatch in _swatches)
            {
                if (swatch == null) continue;
                if (!swatch.gameObject.activeSelf) continue;

                if (seen == order) return swatch;

                seen++;
            }

            return null;
        }

        private bool IsTutorialRunning => _tutorialState != null && _tutorialState.LocksInput;

        /// Dựng lại trạng thái cuộn khi hướng dẫn bật/tắt.
        private void HandleTutorialStageChanged(TutorialStage stage) => ApplyBarAlignment(false);

        public bool TryGetOriginWorldPosition(int paletteIndex, out Vector3 world)
        {
            world = default;

            if (_worldCamera == null)
            {
                Debug.LogWarning($"{nameof(ColorPaletteBar)} chưa gán World Camera — " +
                                 "hiệu ứng ngọc bay sẽ không có điểm xuất phát.");
                return false;
            }

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return false;

            var canvas = swatch.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, swatch.ColorCenterWorldPosition);
            var depth = Mathf.Abs(_worldCamera.transform.position.z);

            world = _worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            return true;
        }

        /// Tìm ô màu theo chỉ số màu.
        private ColorSwatchView FindSwatch(int paletteIndex)
        {
            if (paletteIndex < 0) return null;

            foreach (var swatch in _swatches)
            {
                if (swatch.PaletteIndex == paletteIndex) return swatch;
            }

            return null;
        }

        /// Lấy hoặc tạo ô màu để tái dùng.
        private ColorSwatchView GetSwatch(int slot)
        {
            while (_swatches.Count <= slot)
            {
                var created = Instantiate(_swatchPrefab, _root);
                created.gameObject.SetActive(false);

                _swatches.Add(created);
            }

            return _swatches[slot];
        }

        /// Gỡ và ẩn mọi ô màu.
        private void HideAll()
        {
            foreach (var swatch in _swatches)
            {
                swatch.Unbind();
                swatch.gameObject.SetActive(false);
            }
        }
    }
}
