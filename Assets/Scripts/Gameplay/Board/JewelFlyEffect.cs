using System;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Viên ngọc bay từ ô màu trên thanh chọn tới ô vừa tô.
    public class JewelFlyEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Header("Đường bay")]
        [Tooltip("Thời gian bay ứng với Reference Distance.")]
        [SerializeField] private float _duration = 0.4f;

        [Tooltip("Khoảng cách (world unit) mà tại đó viên bay đúng bằng Duration.")]
        [SerializeField] private float _referenceDistance = 8f;

        [Tooltip("Sàn thời gian bay.")]
        [SerializeField] private float _minDuration = 0.42f;

        [SerializeField] private float _maxDuration = 0.62f;

        [Tooltip("Mức độ thời gian bay phụ thuộc vào quãng đường.")]
        [Range(0f, 1f)]
        [SerializeField] private float _durationFalloff = 0.25f;

        [Tooltip("Xê dịch ngẫu nhiên thời gian bay, theo tỉ lệ.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float _durationVariance = 0.08f;

        [Tooltip("Ease của quãng bay xa.")]
        [SerializeField] private Ease _moveEase = Ease.OutCubic;

        [Tooltip("Ease của quãng bay gần.")]
        [SerializeField] private Ease _nearMoveEase = Ease.InOutSine;

        [Tooltip("Quãng ngắn hơn tỉ lệ này của Reference Distance thì dùng Near Move Ease.")]
        [Range(0f, 1f)]
        [SerializeField] private float _nearEaseReach = 0.8f;

        [Header("Cỡ viên")]
        [Tooltip("Cỡ viên lúc rời thanh màu khi bay xa, so với cỡ ô.")]
        [SerializeField] private float _startScale = 2.6f;

        [Tooltip("Cỡ viên lúc rời thanh màu khi bay rất ngắn.")]
        [SerializeField] private float _nearStartScale = 1.2f;

        [Tooltip("Cỡ viên ở thời điểm chạm ô, trước khi nở về đúng 1.")]
        [SerializeField] private float _settleScale = 0.92f;

        [Tooltip("Phần cuối của quãng bay dành cho pha nở về 1, tính theo tỉ lệ.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _settlePortion = 0.18f;

        [SerializeField] private Ease _scaleEase = Ease.InOutSine;

        [Header("Hiện dần")]
        [Tooltip("Phần đầu quãng bay dành cho việc hiện dần từ trong suốt, tính theo tỉ lệ.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _fadeInPortion = 0.2f;

        [Header("Giới hạn")]
        [Tooltip("Số viên bay cùng lúc tối đa.")]
        [SerializeField] private int _maxConcurrent = 24;

        [SerializeField] private int _prewarmCount = 24;

        [Tooltip("Số viên bay cùng lúc tối đa trong đợt tô của booster tô hết màu.")]
        [SerializeField] private int _burstMaxConcurrent = 420;

        [Tooltip("Order in Layer của viên ngọc đang bay.")]
        [SerializeField] private int _flyingSortingOrder = 15;

        /// Một cú bay đang dở.
        private struct Flight
        {
            public SpriteRenderer Flyer;
            public Vector2Int Cell;
            public int PaletteIndex;
            public Vector3 Origin;
            public Vector3 Target;
            public float Elapsed;
            public float Duration;
            public float StartScale;

            public float Reach;

            public float TargetAlpha;
        }

        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly List<Flight> _flights = new();
        private readonly HashSet<Vector2Int> _inFlight = new();

        private readonly Dictionary<int, int> _inFlightByPalette = new();

        private readonly HashSet<string> _warnings = new();

        private BoardView _boardView;
        private IPaintService _paintService;
        private IPaintOriginProvider _originProvider;
        private ISoundService _sound;

        private bool _burstActive;

        private int _baseSortingOrder;
        private bool _hasBaseSortingOrder;

        public event Action<Vector2Int, int> OnJewelLanded;

        public void Init(BoardView boardView, IPaintService paintService, IPaintOriginProvider originProvider,
            ISoundService sound)
        {
            _boardView = boardView;
            _paintService = paintService;
            _originProvider = originProvider;
            _sound = sound;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _paintService.OnCellPainted += HandleCellPainted;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_paintService != null) _paintService.OnCellPainted -= HandleCellPainted;

            AbortAllFlights();
        }

        /// Bật tắt chế độ bay hàng loạt của booster.
        public void SetBurstMode(bool active)
        {
            _burstActive = active;
        }

        /// Ô này có viên ngọc đang bay tới không.
        public bool IsInFlight(Vector2Int cell) => _inFlight.Contains(cell);

        /// Màu này còn viên nào đang bay giữa trời không.
        public bool HasInFlight(int paletteIndex)
        {
            return _inFlightByPalette.TryGetValue(paletteIndex, out var count) && count > 0;
        }

        private void HandleBoardRebuilt()
        {
            _burstActive = false;

            AbortAllFlights();
            Prewarm();
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            if (!TryStartFlight(cell, paletteIndex)) Land(cell, paletteIndex);
        }

        private bool TryStartFlight(Vector2Int cell, int paletteIndex)
        {
            var layout = _boardView.Layout;
            var colors = _boardView.JewelColors;

            if (layout == null || colors == null) return false;
            if (paletteIndex < 0 || paletteIndex >= colors.Count) return false;

            var limit = _burstActive ? Mathf.Max(_maxConcurrent, _burstMaxConcurrent) : _maxConcurrent;

            if (_flights.Count >= limit) return false;

            if (_originProvider == null ||
                !_originProvider.TryGetOriginWorldPosition(paletteIndex, out var origin))
            {
                WarnOnce("Không lấy được vị trí ô màu trên thanh chọn — ngọc sẽ hiện ngay " +
                         "không có hiệu ứng bay. Kiểm tra ô World Camera của ColorPaletteBar.");
                return false;
            }

            var flyer = Rent();
            if (flyer == null)
            {
                WarnOnce($"{nameof(JewelFlyEffect)} chưa gán Jewel Prefab — không có hiệu ứng bay.");
                return false;
            }

            var target = (Vector3)layout.CellToWorldCenter(cell.x, cell.y);
            var depth = _root != null ? _root.position.z : 0f;
            origin.z = depth;
            target.z = depth;

            var distance = Vector3.Distance(origin, target);
            var reach = Mathf.Clamp01(distance / Mathf.Max(0.01f, _referenceDistance));

            flyer.color = colors[paletteIndex];
            flyer.sortingOrder = _flyingSortingOrder;
            flyer.transform.position = origin;

            var startScale = Mathf.Lerp(_nearStartScale, _startScale, reach);
            flyer.transform.localScale = Vector3.one * startScale;

            var targetAlpha = flyer.color.a;

            if (_fadeInPortion > 0f)
            {
                var faded = flyer.color;
                faded.a = 0f;
                flyer.color = faded;
            }

            _inFlight.Add(cell);
            AddInFlight(paletteIndex, 1);

            _flights.Add(new Flight
            {
                Flyer = flyer,
                Cell = cell,
                PaletteIndex = paletteIndex,
                Origin = origin,
                Target = target,
                Elapsed = 0f,
                Duration = Mathf.Max(0.01f, ResolveDuration(distance)),
                StartScale = startScale,
                Reach = reach,
                TargetAlpha = targetAlpha,
            });

            return true;
        }

        /// Mỗi frame nhích mọi viên đang bay một bước.
        private void Update()
        {
            if (_flights.Count == 0) return;

            var deltaTime = Time.deltaTime;
            var travelPortion = 1f - Mathf.Clamp01(_settlePortion);

            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                var flight = _flights[i];

                if (flight.Flyer == null)
                {
                    Finish(i, flight, recycle: false);
                    continue;
                }

                flight.Elapsed += deltaTime;
                var t = Mathf.Clamp01(flight.Elapsed / flight.Duration);

                var moveEase = flight.Reach <= _nearEaseReach ? _nearMoveEase : _moveEase;
                var progress = DOVirtual.EasedValue(0f, 1f, t, moveEase);

                var flyerTransform = flight.Flyer.transform;
                flyerTransform.position = Vector3.LerpUnclamped(flight.Origin, flight.Target, progress);
                flyerTransform.localScale = Vector3.one * ResolveScale(flight.StartScale, t, travelPortion);

                if (_fadeInPortion > 0f)
                {
                    var color = flight.Flyer.color;
                    color.a = flight.TargetAlpha * DOVirtual.EasedValue(
                        0f, 1f, Mathf.Clamp01(t / _fadeInPortion), Ease.OutSine);
                    flight.Flyer.color = color;
                }

                if (t < 1f)
                {
                    _flights[i] = flight;
                    continue;
                }

                Finish(i, flight, recycle: true);
            }
        }

        /// Tỉ lệ viên ngọc theo tiến độ bay.
        private float ResolveScale(float startScale, float t, float travelPortion)
        {
            if (travelPortion >= 1f || Mathf.Approximately(_settleScale, 1f))
            {
                return Mathf.LerpUnclamped(startScale, 1f, DOVirtual.EasedValue(0f, 1f, t, _scaleEase));
            }

            if (t <= travelPortion)
            {
                var shrink = DOVirtual.EasedValue(0f, 1f, t / travelPortion, _scaleEase);
                return Mathf.LerpUnclamped(startScale, _settleScale, shrink);
            }

            var settle = DOVirtual.EasedValue(
                0f, 1f, (t - travelPortion) / (1f - travelPortion), Ease.OutSine);

            return Mathf.LerpUnclamped(_settleScale, 1f, settle);
        }

        private void Finish(int index, Flight flight, bool recycle)
        {
            var last = _flights.Count - 1;
            _flights[index] = _flights[last];
            _flights.RemoveAt(last);

            if (recycle) Recycle(flight.Flyer);

            _inFlight.Remove(flight.Cell);
            AddInFlight(flight.PaletteIndex, -1);

            Land(flight.Cell, flight.PaletteIndex);
        }

        /// Thời gian bay theo quãng đường.
        private float ResolveDuration(float distance)
        {
            var reference = Mathf.Max(0.01f, _referenceDistance);

            var factor = Mathf.Pow(distance / reference, Mathf.Clamp01(_durationFalloff));
            var scaled = _duration * factor;

            var min = Mathf.Max(0.01f, _minDuration);
            var max = Mathf.Max(min, _maxDuration);

            var variance = 1f + UnityEngine.Random.Range(-_durationVariance, _durationVariance);

            return Mathf.Clamp(scaled, min, max) * variance;
        }

        /// Xử lý viên ngọc đáp xuống ô.
        private void Land(Vector2Int cell, int paletteIndex)
        {
            if (_sound != null) _sound.Play(SoundKey.Pop);

            _boardView.RevealCell(cell, paletteIndex);

            OnJewelLanded?.Invoke(cell, paletteIndex);
        }

        private void AddInFlight(int paletteIndex, int delta)
        {
            _inFlightByPalette.TryGetValue(paletteIndex, out var count);

            count += delta;

            if (count <= 0) _inFlightByPalette.Remove(paletteIndex);
            else _inFlightByPalette[paletteIndex] = count;
        }

        /// Cảnh báo một lần duy nhất.
        private void WarnOnce(string message)
        {
            if (!_warnings.Add(message)) return;

            Debug.LogWarning(message);
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.enabled = true;
                return pooled;
            }

            if (_jewelPrefab == null) return null;

            CacheBaseSortingOrder();

            return Instantiate(_jewelPrefab, _root);
        }

        /// Bỏ mọi cú bay đang dở, dùng khi đổi màn hoặc lúc huỷ object.
        private void AbortAllFlights()
        {
            for (var i = 0; i < _flights.Count; i++)
            {
                var flyer = _flights[i].Flyer;
                if (flyer != null) Recycle(flyer);
            }

            _flights.Clear();
            _inFlight.Clear();
            _inFlightByPalette.Clear();
        }

        /// Trả viên ngọc về kho.
        private void Recycle(SpriteRenderer flyer)
        {
            flyer.transform.localScale = Vector3.one;
            flyer.sortingOrder = _baseSortingOrder;

            var color = flyer.color;
            color.a = 1f;
            flyer.color = color;

            flyer.enabled = false;
            _pool.Push(flyer);
        }

        /// Lưu sorting order gốc từ prefab.
        private void CacheBaseSortingOrder()
        {
            if (_hasBaseSortingOrder || _jewelPrefab == null) return;

            _baseSortingOrder = _jewelPrefab.sortingOrder;
            _hasBaseSortingOrder = true;
        }

        private void Prewarm()
        {
            if (_jewelPrefab == null) return;

            CacheBaseSortingOrder();

            while (_pool.Count < _prewarmCount)
            {
                var flyer = Instantiate(_jewelPrefab, _root);
                flyer.enabled = false;
                _pool.Push(flyer);
            }
        }
    }
}
