using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Vệt sáng quét qua ô ngay khi viên ngọc đáp xuống.
    public class JewelLandSparkle : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Kho hiệu ứng loé.")]
        [SerializeField] private BurstEffectPool _burstPool;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì không loé.")]
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
