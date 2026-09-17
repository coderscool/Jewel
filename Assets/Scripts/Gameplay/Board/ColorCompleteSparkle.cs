using System.Collections.Generic;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Loé sáng mọi ô của một màu khi màu đó được tô xong.
    public class ColorCompleteSparkle : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Kho hiệu ứng loé.")]
        [SerializeField] private BurstEffectPool _burstPool;

        [Tooltip("Số ô loé mỗi frame, 0 là loé cùng lúc.")]
        [SerializeField] private int _maxPerFrame;

        [Tooltip("Thời gian chờ sau khi viên ngọc cuối của màu đáp xuống rồi mới loé.")]
        [SerializeField] private float _startDelay = 0.15f;

        [Tooltip("Chỉ loé những ô đang lọt trong khung hình.")]
        [SerializeField] private bool _visibleCellsOnly = true;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì không loé.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        private readonly List<Vector2Int> _pending = new();

        private readonly HashSet<int> _celebrated = new();

        private float _delayRemaining;

        private bool _celebrating;

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        public bool IsCelebrating => _celebrating;

        public float StartDelay => Mathf.Max(0f, _startDelay);

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect,
            ISoundService sound)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;
            _sound = sound;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;

            _flyEffect.OnJewelLanded += HandleJewelLanded;
        }

        private bool _soundPending;

        private ISoundService _sound;

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
        }

        private void HandleBoardRebuilt()
        {
            _celebrated.Clear();
            _pending.Clear();

            _celebrating = false;
            _delayRemaining = 0f;

            if (_burstPool == null) return;

            _burstPool.ReleaseAll();
            _burstPool.Prewarm();
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_paintService.RemainingFor(paletteIndex) > 0) return;
            if (_flyEffect != null && _flyEffect.HasInFlight(paletteIndex)) return;

            if (!_celebrated.Add(paletteIndex)) return;

            Burst(paletteIndex);
        }

        /// Xếp mọi ô của màu vừa xong vào hàng chờ.
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

            if (queued > 0)
            {
                _celebrating = true;
                _delayRemaining = Mathf.Max(0f, _startDelay);
                _soundPending = true;
            }
        }

        /// Rút hàng chờ theo nhịp mỗi frame.
        private void Update()
        {
            if (_delayRemaining > 0f)
            {
                _delayRemaining -= Time.deltaTime;

                if (_delayRemaining > 0f) return;

                _delayRemaining = 0f;
            }

            if (_soundPending)
            {
                _soundPending = false;

                if (_sound != null) _sound.Play(SoundKey.ColorComplete);
            }

            Drain();

            if (_celebrating && _pending.Count == 0
                && (_burstPool == null || _burstPool.ActiveCount == 0))
            {
                _celebrating = false;
            }
        }

        private void Drain()
        {
            if (_pending.Count == 0 || _burstPool == null) return;

            var layout = _boardView.Layout;
            if (layout == null)
            {
                _pending.Clear();
                return;
            }

            var budget = _maxPerFrame > 0 ? _maxPerFrame : int.MaxValue;

            while (_pending.Count > 0 && budget-- > 0)
            {
                var last = _pending.Count - 1;
                var cell = _pending[last];

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
