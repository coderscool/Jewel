using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Vệt sáng quét qua ô ngay khi viên ngọc đáp xuống.
    ///
    /// Nghe OnJewelLanded — sự kiện chỉ nổ MỘT LẦN cho mỗi ô trong cả màn. Nhờ vậy kéo
    /// camera ra rồi kéo vào lại không làm hiệu ứng chạy lại, khác hẳn với việc gắn
    /// Particle System vào prefab viên ngọc.
    public class JewelLandSparkle : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private ParticleBurstPool _burstPool;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì bỏ qua — ở cỡ đó hiệu " +
                 "ứng chỉ còn vài pixel nhấp nháy, trông như nhiễu.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        private BoardView _boardView;
        private JewelFlyEffect _flyEffect;

        public void Init(BoardView boardView, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _flyEffect = flyEffect;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
        }

        private void HandleBoardRebuilt()
        {
            if (_burstPool == null) return;

            _burstPool.ReleaseAll();
            _burstPool.Prewarm();
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_burstPool == null || !_burstPool.HasPrefab) return;

            var layout = _boardView.Layout;
            if (layout == null) return;

            if (BoardLayout.CellScreenPixels(Screen.height, _camera.orthographicSize) < _minCellScreenPixels)
            {
                return;
            }

            _burstPool.Play(layout.CellToWorldCenter(cell.x, cell.y));
        }
    }
}
