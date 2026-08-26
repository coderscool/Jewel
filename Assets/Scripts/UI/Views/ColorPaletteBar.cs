using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

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

        [Tooltip("Scroll Rect của thanh màu. Có gán thì mỗi lần vào màn nó tự cuộn về ô " +
                 "màu ĐẦU TIÊN.\n\n" +
                 "Cần thiết vì vị trí cuộn không tự reset: kéo thanh sang phải ở màn này " +
                 "thì vào màn sau nó vẫn nằm nguyên chỗ đó, dù danh sách màu đã khác hẳn.")]
        [SerializeField] private ScrollRect _scrollRect;

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

            // Đếm slot RIÊNG, không dùng chỉ số của vòng lặp.
            //
            // Dùng chỉ số vòng lặp thì mỗi màu bị bỏ qua để lại một slot trống ở giữa,
            // mà GetSwatch tạo ô mới theo slot — nên nó vẫn phải sinh ra những ô đệm
            // cho các slot bị nhảy cóc. Chúng chưa Bind bao giờ nhưng vẫn hiện, và
            // người chơi thấy vài ô màu trắng trơn nằm đầu thanh.
            //
            // Trước khi có tính năng lưu thì lỗi này không lộ, vì lúc vào màn chưa màu
            // nào xong sẵn để mà bị bỏ qua.
            var slot = 0;

            foreach (var paletteIndex in used)
            {
                if (paletteIndex < 0 || paletteIndex >= colors.Count) continue;

                var remaining = _paintService.RemainingFor(paletteIndex);
                if (remaining <= 0) continue;   // màu đã xong sẵn thì không dựng ô nào

                var swatch = GetSwatch(slot++);
                swatch.Bind(paletteIndex, colors[paletteIndex], HandleSwatchClicked);
                swatch.SetRemaining(remaining);
                swatch.SetProgress(_paintService.ProgressFor(paletteIndex));
                swatch.SetSelected(false);
                swatch.gameObject.SetActive(true);
            }

            RelayoutBar();
        }

        /// Sắp lại thanh: còn nhiều màu thì cuộn về ô đầu, còn ít màu thì căn giữa.
        ///
        /// Phải đợi HẾT MỘT FRAME. Content Size Fitter tính lại bề rộng của thanh ở cuối
        /// frame, và ngay sau đó Scroll Rect kẹp lại vị trí theo bề rộng mới. Đo hay đặt
        /// trong cùng frame với lúc dựng các ô đều là làm xong bị ghi đè.
        private void RelayoutBar()
        {
            if (_scrollRect == null || !isActiveAndEnabled) return;

            StopAllCoroutines();
            StartCoroutine(RelayoutBarRoutine());
        }

        private IEnumerator RelayoutBarRoutine()
        {
            yield return null;

            if (_scrollRect == null) yield break;

            Canvas.ForceUpdateCanvases();

            var content = _scrollRect.content;
            if (content == null) yield break;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            // Dừng đà quán tính. Cú kéo dở dang của màn trước còn trớn thì nó đẩy thanh
            // trôi tiếp ngay sau khi ta đặt xong.
            _scrollRect.velocity = Vector2.zero;

            var fitsInViewport = content.rect.width <= viewport.rect.width;

            // TẮT cuộn ngang khi các ô đã vừa khung nhìn.
            //
            // Không phải để chặn thao tác, mà vì Scroll Rect luôn kéo content về nằm gọn
            // trong biên — và khi content NHỎ HƠN khung nhìn, phép kẹp đó dí nó vào một
            // mép. Căn giữa xong sẽ bị nó đẩy về lại ngay frame sau. Tắt trục ngang thì
            // phép kẹp không đụng tới trục đó nữa.
            _scrollRect.horizontal = !fitsInViewport;

            if (!fitsInViewport)
            {
                _scrollRect.horizontalNormalizedPosition = 0f;   // 0 là mép TRÁI
                yield break;
            }

            // Dịch content sao cho tâm nó trùng tâm khung nhìn.
            //
            // Đo qua toạ độ thế giới rồi đổi về hệ của viewport, thay vì tính từ
            // anchoredPosition: cách này không phụ thuộc người dựng đặt Anchor và Pivot
            // của content ở đâu.
            var contentCenter = viewport.InverseTransformPoint(content.TransformPoint(content.rect.center));
            var delta = viewport.rect.center.x - contentCenter.x;

            content.anchoredPosition += new Vector2(delta, 0f);
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

                // Thanh vừa hụt đi một ô nên phải sắp lại. Đây mới là lúc hay xảy ra nhất:
                // càng về cuối màn càng ít màu, và đó đúng là lúc dồn về giữa dễ thấy nhất.
                RelayoutBar();
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

        /// Ô màu ĐẦU TIÊN đang hiện trên thanh, hoặc null khi thanh còn trống.
        ///
        /// Trả về RectTransform chứ không phải toạ độ: hướng dẫn cần bám theo nó khi bố
        /// cục đổi, mà một điểm chụp sẵn thì không bám được.
        public RectTransform FirstSwatchRect
        {
            get
            {
                foreach (var swatch in _swatches)
                {
                    if (swatch == null) continue;
                    if (!swatch.gameObject.activeSelf) continue;

                    return (RectTransform)swatch.transform;
                }

                return null;
            }
        }

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

        /// Cố tình KHÔNG đòi ô phải đang hiện.
        ///
        /// Ô màu vừa tô hết bị ẩn ngay trong lượt sự kiện OnCellPainted, mà thanh màu
        /// lại đăng ký sự kiện đó TRƯỚC hiệu ứng ngọc bay. Đòi activeSelf thì viên cuối
        /// cùng của mỗi màu hỏi vị trí xuất phát đúng lúc ô vừa tắt, không tìm ra, và
        /// vĩnh viễn không có hiệu ứng bay.
        ///
        /// Sửa ở đây chứ không sửa thứ tự Init: dựa vào thứ tự đăng ký event là loại
        /// ràng buộc vô hình, ai đổi một dòng ở Bootstrap là hỏng lại mà không hiểu vì sao.
        ///
        /// Ô đã Unbind mang PaletteIndex = -1 nên không bao giờ khớp nhầm.
        private ColorSwatchView FindSwatch(int paletteIndex)
        {
            if (paletteIndex < 0) return null;

            foreach (var swatch in _swatches)
            {
                if (swatch.PaletteIndex == paletteIndex) return swatch;
            }

            return null;
        }

        /// Tạo một lần rồi bật tắt để tái dùng — không Instantiate/Destroy mỗi màn.
        ///
        /// Ô mới sinh ra ở trạng thái TẮT: prefab vốn đang bật, nên ô nào tạo ra mà bên
        /// gọi chưa kịp bật lên sẽ hiện nguyên si nội dung của prefab.
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

        /// Unbind trước khi ẩn: ô còn giữ chỉ số màu của màn trước sẽ bị FindSwatch
        /// khớp nhầm, và ngọc của màn mới bay ra từ một chỗ vô nghĩa.
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
