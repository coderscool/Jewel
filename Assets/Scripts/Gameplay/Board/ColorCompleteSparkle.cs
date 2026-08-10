using System.Collections.Generic;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Tô xong TOÀN BỘ ô của một màu thì mọi ô mang màu đó loé sáng cùng lúc.
    ///
    /// Lớp này KHÔNG dựng hiệu ứng — hình ảnh nằm hết trong prefab Particle System bạn
    /// tự làm trong Editor. Code chỉ trả lời hai câu: *lúc nào* và *ở những ô nào*.
    /// Muốn đổi màu, tốc độ, số sao thì mở prefab ra chỉnh, không phải đụng vào đây.
    ///
    /// Chỉ phát ở ô đang lọt trong khung hình. Một màu trên bảng 64x64 có thể chiếm
    /// hơn 500 ô, mà ô ngoài màn hình thì người chơi không thấy — sinh ra chỉ để tụt
    /// khung hình đúng vào khoảnh khắc đáng lẽ phải đã mắt nhất.
    public class ColorCompleteSparkle : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Prefab Particle System của hiệu ứng loé. Phải TẮT Play On Awake — " +
                 "code tự gọi Play() đúng lúc.")]
        [SerializeField] private ParticleSystem _burstPrefab;

        [SerializeField] private Transform _root;

        [Tooltip("Số ô loé tối đa trong một lần. Vượt quá thì cắt — mắt không đếm được " +
                 "hơn chừng này đốm sáng nổ cùng lúc, nhưng máy thì vẫn phải vẽ đủ.")]
        [SerializeField] private int _maxPerBurst = 120;

        [Tooltip("Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì bỏ qua: ở cỡ đó " +
                 "cả trăm đốm sáng chỉ còn là một mảng nhiễu.")]
        [SerializeField] private float _minCellScreenPixels = 14f;

        [SerializeField] private int _prewarmCount = 60;

        [Tooltip("Chờ ít nhất ngần này giây rồi mới tin IsAlive để thu về. Đặt lớn hơn " +
                 "Start Delay lớn nhất trong prefab, không thì hệ hạt bị thu ngay trước " +
                 "khi nó kịp bắn hạt đầu tiên.")]
        [SerializeField] private float _minAliveSeconds = 0.3f;

        private struct ActiveBurst
        {
            public ParticleSystem System;
            public float Elapsed;
        }

        private readonly List<ActiveBurst> _active = new();
        private readonly Stack<ParticleSystem> _pool = new();

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
            ReleaseAll();
            _celebrated.Clear();
            Prewarm();
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_paintService.RemainingFor(paletteIndex) > 0) return;
            if (!_celebrated.Add(paletteIndex)) return;

            Burst(paletteIndex);
        }

        private void Burst(int paletteIndex)
        {
            if (_burstPrefab == null)
            {
                Debug.LogWarning($"{nameof(ColorCompleteSparkle)} chưa gán Burst Prefab — " +
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

                    PlayAt(layout.CellToWorldCenter(x, y));

                    if (--budget <= 0) return;
                }
            }
        }

        private void PlayAt(Vector2 world)
        {
            var system = Rent();
            if (system == null) return;

            system.transform.position = world;

            // Clear trước Play: hệ hạt lấy từ kho có thể còn hạt đông cứng từ lần trước,
            // và chúng sẽ hiện ra ngay frame đầu ở đúng chỗ mới.
            system.Clear(true);
            system.Play(true);

            _active.Add(new ActiveBurst { System = system, Elapsed = 0f });
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;

            var deltaTime = Time.deltaTime;

            // Chạy ngược vì Release() xoá phần tử ngay dưới chân.
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.Elapsed += deltaTime;

                if (item.Elapsed >= _minAliveSeconds && !item.System.IsAlive(true))
                {
                    Release(i);
                    continue;
                }

                // Ghi lại vì ActiveBurst là struct: sửa bản sao không đụng tới List.
                _active[i] = item;
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

        private void Release(int index)
        {
            var item = _active[index];

            item.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.System.gameObject.SetActive(false);
            _pool.Push(item.System);

            // Kéo phần tử cuối vào chỗ trống thay vì RemoveAt giữa danh sách. Vòng lặp
            // gọi hàm này chạy ngược nên phần tử vừa kéo về đã duyệt rồi.
            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }

        private void ReleaseAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private ParticleSystem Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_burstPrefab == null) return null;

            return Instantiate(_burstPrefab, _root);
        }

        /// Dựng sẵn lúc vào màn. Instantiate cả trăm hệ hạt đúng vào frame màu vừa xong
        /// là cách chắc chắn nhất để khoảnh khắc ăn mừng biến thành cú khựng.
        private void Prewarm()
        {
            if (_burstPrefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var system = Instantiate(_burstPrefab, _root);
                system.gameObject.SetActive(false);
                _pool.Push(system);
            }
        }
    }
}
