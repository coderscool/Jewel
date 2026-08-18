using System.Collections.Generic;
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

        [Tooltip("Số ô loé tối đa trong một lần. Vượt quá thì cắt — mắt không đếm được " +
                 "hơn chừng này đốm sáng nổ cùng lúc, nhưng máy thì vẫn phải vẽ đủ.")]
        [SerializeField] private int _maxPerBurst = 120;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì bỏ qua: ở cỡ đó " +
                 "cả trăm đốm sáng chỉ còn là một mảng nhiễu.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

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

            var visible = layout.VisibleCells(CameraWorldRect());
            var budget = Mathf.Max(1, _maxPerBurst);

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    if (grid.GetCell(x, y) != paletteIndex) continue;

                    _burstPool.Play(layout.CellToWorldCenter(x, y));

                    if (--budget <= 0) return;
                }
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
