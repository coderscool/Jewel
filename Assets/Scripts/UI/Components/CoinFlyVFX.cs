using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Hiệu ứng tiền bay về icon tiền trên HUD.
    public class CoinFlyVFX : MonoBehaviour
    {
        [Tooltip("Prefab một đồng tiền.")]
        [SerializeField] private RectTransform _coinPrefab;

        [Tooltip("Nơi chứa coin lúc bay.")]
        [SerializeField] private RectTransform _coinsParent;

        [Header("Số lượng và thời gian")]
        [SerializeField] private int _coinCount = 7;

        [Tooltip("Thời gian bay từ chỗ vừa rơi tới đích.")]
        [SerializeField] private float _flyDuration = 0.6f;

        [Tooltip("Độ trễ giữa lúc bắn ra từng coin.")]
        [SerializeField] private float _staggerDelay = 0.04f;

        [Tooltip("Độ cao vòng cầu lúc bay lên, tính bằng pixel UI.")]
        [SerializeField] private float _arcHeight = 150f;

        [Tooltip("Toả ngẫu nhiên quanh điểm xuất phát, tính bằng pixel UI.")]
        [SerializeField] private float _scatterRadius = 60f;

        [Header("Pha vãi ra")]
        [Tooltip("Thời gian coin vãi ra và rơi xuống, trước khi bay lên đích.")]
        [SerializeField] private float _dropDuration = 0.55f;

        [Tooltip("Rơi xuống thấp hơn điểm xuất phát bao nhiêu pixel UI.")]
        [SerializeField] private float _dropDistance = 300f;

        [Tooltip("Độ toả ngang khi vãi ra, lệch ngẫu nhiên ± giá trị này.")]
        [SerializeField] private float _dropSpreadX = 220f;

        [Tooltip("Thời gian nằm chờ sau khi rơi rồi mới bay lên.")]
        [SerializeField] private float _holdAfterDrop = 0.25f;

        [Header("Thứ tự vẽ")]
        [Tooltip("Sorting Layer của coin.")]
        [SerializeField] private string _sortingLayerName = "";

        [SerializeField] private int _sortingOrder = 100;

        private readonly List<Tween> _pending = new();
        private readonly List<RectTransform> _flying = new();

        private readonly Stack<RectTransform> _pool = new();

        public int CoinCount => Mathf.Max(1, _coinCount);

        /// Bắn coin bay về đích.
        public void Play(RectTransform from, RectTransform to, Action onEachArrive = null, Action onAllDone = null)
        {
            if (_coinPrefab == null || _coinsParent == null || from == null || to == null)
            {
                onAllDone?.Invoke();
                return;
            }

            StopAll();

            var remaining = CoinCount;

            for (var i = 0; i < CoinCount; i++)
            {
                _pending.Add(DOVirtual.DelayedCall(i * _staggerDelay, () =>
                {
                    SpawnCoin(from, to, () =>
                    {
                        onEachArrive?.Invoke();

                        remaining--;
                        if (remaining <= 0) onAllDone?.Invoke();
                    });
                }));
            }
        }

        /// Dừng hẳn: huỷ cả coin chưa kịp bắn lẫn coin đang bay.
        public void StopAll()
        {
            foreach (var tween in _pending) tween?.Kill();
            _pending.Clear();

            foreach (var coin in _flying)
            {
                if (coin == null) continue;

                DOTween.Kill(coin);
                Release(coin);
            }

            _flying.Clear();
        }

        private void OnDisable() => StopAll();

        private void OnDestroy() => StopAll();

        /// Dựng sẵn coin và chốt thứ tự vẽ.
        private void Awake()
        {
            ApplySorting();
            Prewarm();
        }

        private void Prewarm()
        {
            if (_coinPrefab == null || _coinsParent == null) return;

            while (_pool.Count < CoinCount) _pool.Push(CreateCoin());
        }

        private RectTransform CreateCoin()
        {
            var coin = Instantiate(_coinPrefab, _coinsParent);

            var image = coin.GetComponent<Image>();
            if (image != null && image.sprite != null) image.SetNativeSize();

            coin.gameObject.SetActive(false);
            return coin;
        }

        private RectTransform Rent()
        {
            var coin = _pool.Count > 0 ? _pool.Pop() : CreateCoin();

            coin.gameObject.SetActive(true);
            return coin;
        }

        private void Release(RectTransform coin)
        {
            if (coin == null) return;

            coin.gameObject.SetActive(false);
            _pool.Push(coin);
        }

        /// Đặt sorting order cho hiệu ứng.
        private void ApplySorting()
        {
            if (string.IsNullOrEmpty(_sortingLayerName) || _coinsParent == null) return;

            var canvas = _coinsParent.GetComponent<Canvas>();
            if (canvas == null) canvas = _coinsParent.gameObject.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.sortingLayerName = _sortingLayerName;
            canvas.sortingOrder = _sortingOrder;
        }

        private void SpawnCoin(RectTransform from, RectTransform to, Action onArrive)
        {
            if (_coinsParent == null)
            {
                onArrive?.Invoke();
                return;
            }

            var coin = Rent();
            _flying.Add(coin);

            var startPos = WorldToLocal(from.position) + UnityEngine.Random.insideUnitCircle * _scatterRadius;
            var endPos = WorldToLocal(to.position);

            var dropPos = startPos + new Vector2(
                UnityEngine.Random.Range(-_dropSpreadX, _dropSpreadX),
                -Mathf.Abs(_dropDistance) * UnityEngine.Random.Range(0.7f, 1.15f));

            coin.anchoredPosition = startPos;
            coin.localScale = Vector3.one * 0.4f;
            coin.DOScale(1f, 0.15f).SetEase(Ease.OutBack);

            var mid = (dropPos + endPos) * 0.5f + Vector2.up * _arcHeight;

            var t = 0f;
            var sequence = DOTween.Sequence().SetTarget(coin);

            sequence.Append(DOTween
                .To(() => coin.anchoredPosition, p => coin.anchoredPosition = p, dropPos, _dropDuration)
                .SetEase(Ease.OutQuad));

            if (_holdAfterDrop > 0f) sequence.AppendInterval(_holdAfterDrop);

            sequence.Append(DOTween
                .To(() => t, x =>
                {
                    t = x;
                    coin.anchoredPosition = QuadraticBezier(dropPos, mid, endPos, t);
                }, 1f, _flyDuration)
                .SetEase(Ease.InQuad));

            sequence.OnComplete(() =>
            {
                _flying.Remove(coin);

                Release(coin);

                onArrive?.Invoke();
            });
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var ab = Vector2.Lerp(a, b, t);
            var bc = Vector2.Lerp(b, c, t);

            return Vector2.Lerp(ab, bc, t);
        }

        /// Đổi toạ độ world sang toạ độ local của canvas.
        private Vector2 WorldToLocal(Vector3 worldPosition)
        {
            var cam = ResolveCamera();
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_coinsParent, screenPoint, cam, out var local);

            return local;
        }

        /// Tìm camera của Canvas gốc.
        private Camera ResolveCamera()
        {
            if (_coinsParent == null) return null;

            var canvas = _coinsParent.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            canvas = canvas.rootCanvas;

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
