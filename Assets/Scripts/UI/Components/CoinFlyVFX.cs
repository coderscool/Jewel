using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Tiền bay: bắn ra một nắm icon coin, vãi xuống rồi bị hút theo vòng cầu về icon
    /// tiền trên HUD.
    ///
    /// Hai pha có chủ đích. Bay thẳng lên đích ngay thì mắt đọc ra là "chuyển một con
    /// số", còn vãi xuống trước rồi mới bị hút lên thì đọc ra là "nhận được một đống
    /// tiền" — cùng một quãng đường, cảm giác khác hẳn.
    ///
    /// Gắn vào một GameObject nằm TRONG Canvas, cùng Canvas với from/to.
    public class CoinFlyVFX : MonoBehaviour
    {
        [Tooltip("Prefab một đồng tiền: RectTransform có Image. Pivot và anchor để 0.5, 0.5.")]
        [SerializeField] private RectTransform _coinPrefab;

        [Tooltip("Nơi chứa coin lúc bay. Phải là một RectTransform PHỦ KÍN màn hình và " +
                 "KHÔNG bị Mask hay Content Size Fitter nào cắt — coin bay ra ngoài khung " +
                 "cha sẽ bị xén mất nửa đường.")]
        [SerializeField] private RectTransform _coinsParent;

        [Header("Số lượng và thời gian")]
        [SerializeField] private int _coinCount = 7;

        [Tooltip("Thời gian bay từ chỗ vừa rơi tới đích.")]
        [SerializeField] private float _flyDuration = 0.6f;

        [Tooltip("Độ trễ giữa lúc bắn ra từng coin. 0 là cả nắm bay thành một khối cứng.")]
        [SerializeField] private float _staggerDelay = 0.04f;

        [Tooltip("Độ cao vòng cầu lúc bay lên, tính bằng pixel UI.")]
        [SerializeField] private float _arcHeight = 150f;

        [Tooltip("Toả ngẫu nhiên quanh điểm xuất phát, tính bằng pixel UI.")]
        [SerializeField] private float _scatterRadius = 60f;

        [Header("Pha vãi ra")]
        [Tooltip("Thời gian coin vãi ra và rơi xuống, TRƯỚC khi bay lên đích.")]
        [SerializeField] private float _dropDuration = 0.55f;

        [Tooltip("Rơi xuống thấp hơn điểm xuất phát bao nhiêu pixel UI.")]
        [SerializeField] private float _dropDistance = 300f;

        [Tooltip("Độ toả ngang khi vãi ra, lệch ngẫu nhiên ± giá trị này.")]
        [SerializeField] private float _dropSpreadX = 220f;

        [Tooltip("Nằm chờ bao lâu sau khi rơi rồi mới bay lên, cho người chơi kịp thấy " +
                 "coin đã vãi ra.")]
        [SerializeField] private float _holdAfterDrop = 0.25f;

        [Header("Thứ tự vẽ")]
        [Tooltip("Sorting Layer của coin. Để trống thì không đụng tới thứ tự vẽ mặc định.")]
        [SerializeField] private string _sortingLayerName = "";

        [SerializeField] private int _sortingOrder = 100;

        private readonly List<Tween> _pending = new();
        private readonly List<RectTransform> _flying = new();

        /// Số coin bắn ra mỗi lần Play. Bên gọi dùng để chia đều số tiền cho từng coin,
        /// cho con số trên HUD tăng đúng nhịp coin bay tới.
        public int CoinCount => Mathf.Max(1, _coinCount);

        /// onEachArrive gọi MỖI LẦN một coin tới đích. onAllDone gọi khi coin cuối cùng
        /// xong, hoặc ngay lập tức nếu thiếu tham chiếu — bên gọi không phải tự phòng
        /// trường hợp hiệu ứng không chạy được.
        public void Play(RectTransform from, RectTransform to, Action onEachArrive = null, Action onAllDone = null)
        {
            if (_coinPrefab == null || _coinsParent == null || from == null || to == null)
            {
                onAllDone?.Invoke();
                return;
            }

            ApplySorting();
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
        ///
        /// Cần thiết vì mỗi coin tới đích đều bắn onEachArrive. Không dọn thì bấm nút
        /// đóng popup xong, số tiền vẫn tiếp tục nhảy lẹt đẹt trên màn hình sau.
        public void StopAll()
        {
            foreach (var tween in _pending) tween?.Kill();
            _pending.Clear();

            foreach (var coin in _flying)
            {
                if (coin == null) continue;

                DOTween.Kill(coin);
                Destroy(coin.gameObject);
            }

            _flying.Clear();
        }

        private void OnDisable() => StopAll();

        private void OnDestroy() => StopAll();

        /// UI vẽ theo thứ tự Canvas, còn particle và Spine vẽ theo Sorting Layer — hai hệ
        /// khác nhau, không so sánh trực tiếp được. Gắn một Canvas CON với overrideSorting
        /// là cách duy nhất kéo UI vào cùng hệ để đặt nó nằm trên.
        ///
        /// KHÔNG thêm GraphicRaycaster: coin chỉ để nhìn, thêm raycaster phủ kín màn hình
        /// là chắn luôn nút bên dưới.
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
            // Popup có thể đã tắt trong lúc chờ tới lượt stagger.
            if (_coinsParent == null)
            {
                onArrive?.Invoke();
                return;
            }

            var coin = Instantiate(_coinPrefab, _coinsParent);
            coin.gameObject.SetActive(true);
            _flying.Add(coin);

            // SetNativeSize đọc pixel thật của sprite kèm Pixels Per Unit, khỏi phải khai
            // kích thước bằng tay ở prefab.
            var image = coin.GetComponent<Image>();
            if (image != null && image.sprite != null) image.SetNativeSize();

            var startPos = WorldToLocal(from.position) + UnityEngine.Random.insideUnitCircle * _scatterRadius;
            var endPos = WorldToLocal(to.position);

            var dropPos = startPos + new Vector2(
                UnityEngine.Random.Range(-_dropSpreadX, _dropSpreadX),
                -Mathf.Abs(_dropDistance) * UnityEngine.Random.Range(0.7f, 1.15f));

            coin.anchoredPosition = startPos;
            coin.localScale = Vector3.one * 0.4f;
            coin.DOScale(1f, 0.15f).SetEase(Ease.OutBack);

            // Điểm điều khiển của đường cong nằm giữa hai đầu rồi đẩy lên — đó là thứ làm
            // coin bay thành vòng cầu thay vì kéo một đường thẳng.
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

                if (coin != null) Destroy(coin.gameObject);

                onArrive?.Invoke();
            });
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var ab = Vector2.Lerp(a, b, t);
            var bc = Vector2.Lerp(b, c, t);

            return Vector2.Lerp(ab, bc, t);
        }

        /// Đi vòng qua screen point thay vì trừ toạ độ world: công thức này đúng với mọi
        /// kiểu Canvas, kể cả Screen Space - Camera và World Space.
        private Vector2 WorldToLocal(Vector3 worldPosition)
        {
            var cam = ResolveCamera();
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_coinsParent, screenPoint, cam, out var local);

            return local;
        }

        /// Tự dò Canvas gốc lúc chạy thay vì bắt gán camera bằng tay: popup là prefab,
        /// mà prefab thì không kéo được camera của scene vào.
        /// Overlay có worldCamera null — đúng ý, kiểu đó không cần camera.
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
