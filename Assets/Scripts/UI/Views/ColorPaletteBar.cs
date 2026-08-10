using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Thanh chọn màu dưới màn hình. Chỉ hiện những màu ảnh thật sự dùng,
    /// kèm số ô còn lại của mỗi màu.
    ///
    /// Nằm trên Canvas nên không bị zoom và kéo theo bảng.
    public class ColorPaletteBar : MonoBehaviour, IPaintOriginProvider
    {
        [SerializeField] private ColorSwatchView _swatchPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Camera của bảng. Cần để đổi vị trí ô màu trên màn hình sang world " +
                 "cho hiệu ứng ngọc bay.")]
        [SerializeField] private Camera _worldCamera;

        private readonly List<ColorSwatchView> _swatches = new();

        private IPaintService _paintService;
        private ILevelService _levelService;

        public void Init(IPaintService paintService, ILevelService levelService)
        {
            _paintService = paintService;
            _levelService = levelService;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnCellPainted += HandleCellPainted;
            _paintService.OnColorSelected += HandleColorSelected;
        }

        private void OnDestroy()
        {
            if (_paintService == null) return;

            _paintService.OnBoardReady -= HandleBoardReady;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnColorSelected -= HandleColorSelected;
        }

        private void HandleBoardReady()
        {
            HideAll();

            if (_swatchPrefab == null)
            {
                Debug.LogWarning($"{nameof(ColorPaletteBar)} chưa gán Swatch Prefab — thanh màu sẽ trống.");
                return;
            }

            var data = _levelService.CurrentGrid;
            if (data == null) return;

            var colors = data.Colors;
            var used = _paintService.UsedPaletteIndices;

            for (var i = 0; i < used.Count; i++)
            {
                var paletteIndex = used[i];
                if (paletteIndex < 0 || paletteIndex >= colors.Count) continue;

                var remaining = _paintService.RemainingFor(paletteIndex);
                if (remaining <= 0) continue;   // màu đã xong sẵn thì không dựng ô nào

                var swatch = GetSwatch(i);
                swatch.Bind(paletteIndex, colors[paletteIndex], HandleSwatchClicked);
                swatch.SetRemaining(remaining);
                swatch.SetProgress(_paintService.ProgressFor(paletteIndex));
                swatch.SetSelected(false);
                swatch.gameObject.SetActive(true);
            }
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return;

            var remaining = _paintService.RemainingFor(paletteIndex);

            // Tô hết màu này thì gỡ ô ra khỏi thanh — giữ lại một ô bấm vào không làm gì
            // chỉ tổ gây nhầm.
            if (remaining <= 0)
            {
                swatch.gameObject.SetActive(false);
                return;
            }

            swatch.SetRemaining(remaining);
            swatch.SetProgress(_paintService.ProgressFor(paletteIndex));
        }

        private void HandleColorSelected(int paletteIndex)
        {
            foreach (var swatch in _swatches)
            {
                if (!swatch.gameObject.activeSelf) continue;

                swatch.SetSelected(swatch.PaletteIndex == paletteIndex);
            }
        }

        private void HandleSwatchClicked(int paletteIndex) => _paintService.SelectColor(paletteIndex);

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

            // Đi qua RectTransformUtility thay vì đọc thẳng transform.position: với Canvas
            // Overlay thì hai cách trùng nhau, nhưng Camera hoặc World Space thì khác hẳn.
            // Truyền null cho Overlay là đúng theo tài liệu Unity.
            var canvas = swatch.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, swatch.ColorCenterWorldPosition);
            var depth = Mathf.Abs(_worldCamera.transform.position.z);

            world = _worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            return true;
        }

        private ColorSwatchView FindSwatch(int paletteIndex)
        {
            foreach (var swatch in _swatches)
            {
                if (swatch.gameObject.activeSelf && swatch.PaletteIndex == paletteIndex) return swatch;
            }

            return null;
        }

        /// Tạo một lần rồi bật tắt để tái dùng — không Instantiate/Destroy mỗi màn.
        private ColorSwatchView GetSwatch(int slot)
        {
            while (_swatches.Count <= slot)
            {
                _swatches.Add(Instantiate(_swatchPrefab, _root));
            }

            return _swatches[slot];
        }

        private void HideAll()
        {
            foreach (var swatch in _swatches) swatch.gameObject.SetActive(false);
        }
    }
}
