using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Tô xong TOÀN BỘ ô của một màu thì mọi ô mang màu đó loé sáng cùng lúc.
    ///
    /// Lớp này KHÔNG dựng hiệu ứng và cũng không quản hệ hạt — hình ảnh nằm trong prefab
    /// Particle System bạn tự làm trong Editor, việc tạo/thu hồi do ParticleBurstPool lo.
    /// Ở đây chỉ trả lời hai câu: *lúc nào* và *ở những ô nào*.
    ///
    /// Chỉ phát ở ô đang lọt trong khung hình. Một màu trên bảng 64x64 có thể chiếm hơn
    /// 500 ô, mà ô ngoài màn hình thì người chơi không thấy — sinh ra chỉ để tụt khung
    /// hình đúng vào khoảnh khắc đáng lẽ phải đã mắt nhất.
    public class ColorCompleteSparkle : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private ParticleBurstPool _burstPool;

        [Tooltip("Số ô loé trong MỖI FRAME.\n\n" +
                 "**Để 0 là cả màu loé cùng một lúc** — đây là mặc định.\n\n" +
                 "Đặt một số dương thì hiệu ứng rải ra nhiều frame cho nhẹ máy. Đó là " +
                 "nhịp rải, KHÔNG phải giới hạn: ô chưa tới lượt nằm chờ frame sau, " +
                 "không ô nào bị bỏ.\n\n" +
                 "Loé cùng lúc thì nhớ đặt Prewarm Count của kho đủ lớn, không thì cả " +
                 "trăm hệ hạt phải Instantiate ngay trong frame đó.")]
        [SerializeField] private int _maxPerFrame;

        [Tooltip("Chỉ loé những ô đang lọt trong khung hình. Bỏ tick thì loé cả ô ngoài " +
                 "màn — trung thực với ý 'mọi ô đều loé' nhưng tốn hệ hạt cho thứ không " +
                 "ai nhìn thấy, và chúng chiếm mất chỗ của những ô đang thấy.")]
        [SerializeField] private bool _visibleCellsOnly = true;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì bỏ qua: ở cỡ đó " +
                 "cả trăm đốm sáng chỉ còn là một mảng nhiễu.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        [Tooltip("In ra Console số ô của màu vừa xong và số ô thật sự được xếp hàng loé. " +
                 "Bật khi thấy 'nó không loé hết' — hai con số lệch nhau bao nhiêu sẽ chỉ " +
                 "thẳng ra nguyên nhân.")]
        [SerializeField] private bool _logBurstCount;

        /// Những ô đã xếp hàng chờ tới lượt loé.
        private readonly List<Vector2Int> _pending = new();

        /// Màu đã loé rồi thì thôi. Cần chốt lại vì lúc ô cuối của một màu được tô xong,
        /// vài viên ngọc cùng màu vẫn đang bay — mỗi viên đáp xuống lại thấy
        /// RemainingFor = 0 và đòi loé thêm một lần nữa.
        private readonly HashSet<int> _celebrated = new();

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;

            // Nghe lúc ĐÁP chứ không phải lúc bấm: viên ngọc cuối cùng phải nằm vào chỗ
            // rồi mới ăn mừng. Nghe OnCellPainted thì hiệu ứng nổ trong khi viên cuối
            // còn đang bay giữa đường.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
        }

        private void HandleBoardRebuilt()
        {
            _celebrated.Clear();
            _pending.Clear();

            if (_burstPool == null) return;

            _burstPool.ReleaseAll();
            _burstPool.Prewarm();
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            // RemainingFor giảm ngay lúc người chơi BẤM, không phải lúc viên ngọc đáp.
            // Kéo tay tô nhanh mấy ô cuối thì con số về 0 trong khi vài viên vẫn đang bay,
            // và viên đầu tiên hạ cánh sẽ châm ngòi ăn mừng quá sớm — đó là lỗi bạn thấy.
            //
            // HasInFlight mới là câu hỏi đúng: màu này còn viên nào giữa trời không.
            if (_paintService.RemainingFor(paletteIndex) > 0) return;
            if (_flyEffect != null && _flyEffect.HasInFlight(paletteIndex)) return;

            if (!_celebrated.Add(paletteIndex)) return;

            Burst(paletteIndex);
        }

        /// Xếp mọi ô của màu vừa xong vào hàng chờ. Việc loé do Update rút dần.
        ///
        /// Trước đây hàm này bắn thẳng và CẮT BỎ phần vượt hạn mức, nên màu nhiều ô chỉ
        /// loé được một phần rồi thôi. Xếp hàng thì không ô nào bị mất, mà frame vẫn
        /// không phải gánh cả trăm hệ hạt cùng lúc.
        private void Burst(int paletteIndex)
        {
            if (_burstPool == null || !_burstPool.HasPrefab)
            {
                Debug.LogWarning($"{nameof(ColorCompleteSparkle)} chưa có Burst Pool kèm prefab — " +
                                 "tô xong một màu sẽ không có hiệu ứng gì.");
                return;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;

            if (layout == null || grid == null) return;
            if (CellScreenPixels() < _minCellScreenPixels) return;

            var area = _visibleCellsOnly
                ? layout.VisibleCells(CameraWorldRect())
                : new RectInt(0, 0, grid.Width, grid.Height);

            var queued = 0;

            for (var y = area.yMin; y < area.yMax; y++)
            {
                for (var x = area.xMin; x < area.xMax; x++)
                {
                    if (grid.GetCell(x, y) != paletteIndex) continue;

                    _pending.Add(new Vector2Int(x, y));
                    queued++;
                }
            }

            if (_logBurstCount) LogBurstCount(paletteIndex, grid, queued);
        }

        /// Đếm TỔNG số ô của màu này trên cả lưới rồi so với số ô thật sự xếp hàng.
        /// Hai con số lệch nhau nghĩa là có ô bị loại — mà chỉ có một lý do để loại:
        /// nó nằm ngoài khung hình.
        private void LogBurstCount(int paletteIndex, PixelGrid grid, int queued)
        {
            var total = 0;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.GetCell(x, y) == paletteIndex) total++;
                }
            }

            Debug.Log($"[ColorComplete] màu {paletteIndex + 1}: {total} ô trên lưới, " +
                      $"{queued} ô xếp hàng loé. " +
                      (total == queued
                          ? "Khớp."
                          : $"Thiếu {total - queued} ô nằm ngoài khung hình — bỏ tick " +
                            "Visible Cells Only nếu muốn loé cả những ô đó."));
        }

        /// Rút hàng chờ theo nhịp mỗi frame.
        ///
        /// Rút từ CUỐI danh sách để xoá không phải dịch cả đuôi. Thứ tự loé vì thế đi
        /// ngược từ dưới phải lên — không ai nhận ra, vì cả màu loé xong trong vài frame.
        private void Update()
        {
            if (_pending.Count == 0 || _burstPool == null) return;

            var layout = _boardView.Layout;
            if (layout == null)
            {
                _pending.Clear();
                return;
            }

            // 0 nghĩa là không giới hạn. int.MaxValue thay vì rẽ nhánh riêng: một màu
            // nhiều nhất cũng chỉ vài nghìn ô nên phép trừ không bao giờ chạm đáy.
            var budget = _maxPerFrame > 0 ? _maxPerFrame : int.MaxValue;

            while (_pending.Count > 0 && budget-- > 0)
            {
                var last = _pending.Count - 1;
                var cell = _pending[last];

                // Kho đầy thì DỪNG, giữ nguyên ô này trong hàng chờ. Mỗi lần loé chỉ
                // sống chưa tới một giây nên chỗ sẽ trống ra — chờ thêm vài frame còn
                // hơn mất hẳn hiệu ứng của ô đó.
                if (!_burstPool.Play(layout.CellToWorldCenter(cell.x, cell.y))) return;

                _pending.RemoveAt(last);
            }
        }

        private float CellScreenPixels()
        {
            return BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize);
        }

        private Rect CameraWorldRect()
        {
            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;
            var center = _camera.transform.position;

            return new Rect(
                center.x - halfWidth,
                center.y - halfHeight,
                halfWidth * 2f,
                halfHeight * 2f);
        }
    }
}
