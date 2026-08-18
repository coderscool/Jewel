using System;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Viên ngọc bay từ ô màu trên thanh chọn tới ô vừa tô.
    ///
    /// Là NGUỒN SỰ THẬT cho việc "ô này đã có ngọc chưa": JewelLayer chờ sự kiện
    /// OnJewelLanded chứ không nghe thẳng OnCellPainted. Nhờ vậy ngọc chỉ hiện khi
    /// viên bay đáp xuống, đúng cảm giác lấy ngọc từ khay gắn vào tranh.
    ///
    /// Mọi đường thoát đều phải bắn OnJewelLanded — không sinh được viên bay, hết chỗ
    /// trong hạn mức, hay thiếu điểm xuất phát đều bắn NGAY. Thiếu một đường thoát là
    /// ô đó kẹt vĩnh viễn không bao giờ có ngọc.
    public class JewelFlyEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _jewelPrefab;
        [SerializeField] private Transform _root;

        [Header("Đường bay")]
        [Tooltip("Thời gian bay ứng với Reference Distance. Quãng ngắn hơn thì nhanh " +
                 "hơn, dài hơn thì chậm hơn, luôn kẹp trong Min/Max Duration.")]
        [SerializeField] private float _duration = 0.4f;

        [Tooltip("Khoảng cách (world unit) mà tại đó viên bay đúng bằng Duration. " +
                 "Đây là thứ giữ cho TỐC ĐỘ đều nhau giữa các cú bay xa gần khác nhau.")]
        [SerializeField] private float _referenceDistance = 8f;

        [Tooltip("Sàn thời gian bay. Đây là ô chặn cảm giác 'ô gần bay vụt một cái là " +
                 "xong' — mắt đọc nhịp theo THỜI GIAN chứ không theo quãng đường.")]
        [SerializeField] private float _minDuration = 0.42f;

        [SerializeField] private float _maxDuration = 0.62f;

        [Tooltip("Thời gian bay bám theo quãng đường CHẶT tới đâu.\n\n" +
                 "1 là tỉ lệ thẳng — quãng nửa thì thời gian nửa, và ô sát thanh màu bay " +
                 "vụt một cái.\n" +
                 "0.25 là mặc định: quãng xa vẫn lâu hơn quãng gần, nhưng chỉ chừng 1.5 " +
                 "lần thay vì gấp đôi.\n" +
                 "0 là MỌI quãng cùng một thời gian.")]
        [Range(0f, 1f)]
        [SerializeField] private float _durationFalloff = 0.25f;

        [Tooltip("Xê dịch ngẫu nhiên thời gian bay, theo tỉ lệ. 0.08 là ±8%. Kéo tay tô " +
                 "một loạt ô thì các viên không đi thành hàng lối cứng nhắc nữa.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float _durationVariance = 0.08f;

        [Tooltip("Nhịp của quãng bay XA. OutCubic: vọt ra nhanh rồi hạ dần — phản hồi " +
                 "tức thì mà vẫn đáp êm.")]
        [SerializeField] private Ease _moveEase = Ease.OutCubic;

        [Tooltip("Nhịp của quãng bay GẦN.\n\n" +
                 "OutCubic có tốc độ ĐỈNH bằng 3 lần tốc độ trung bình, và cả cú vọt đó " +
                 "dồn vào ngay lúc rời thanh màu. Quãng dài thì không sao vì còn cả đoạn " +
                 "sau để hạ dần, nhưng quãng ngắn thì người chơi chỉ kịp thấy đúng cú " +
                 "vọt — đó là cảm giác 'búng một cái'.\n\n" +
                 "InOutSine có đỉnh chỉ 1.57 lần, và đỉnh nằm ở GIỮA quãng nên hai đầu " +
                 "đều êm.")]
        [SerializeField] private Ease _nearMoveEase = Ease.InOutSine;

        [Tooltip("Quãng ngắn hơn ngần này PHẦN của Reference Distance thì dùng Near Move " +
                 "Ease. 0.8 với Reference Distance 8 nghĩa là dưới 6.4 world unit.\n\n" +
                 "Đổi nhịp đột ngột qua ngưỡng không nhìn ra được: mỗi cú bay là một sự " +
                 "kiện riêng, không có hai cú cạnh nhau để mà so.")]
        [Range(0f, 1f)]
        [SerializeField] private float _nearEaseReach = 0.8f;

        [Header("Cỡ viên")]
        [Tooltip("Cỡ viên lúc rời thanh màu khi bay quãng XA (từ Reference Distance trở " +
                 "lên), so với cỡ ô. Lớn hơn 1 rồi nhỏ dần cho cảm giác bay từ gần ra xa.")]
        [SerializeField] private float _startScale = 2.6f;

        [Tooltip("Cỡ viên lúc rời thanh màu khi bay quãng RẤT NGẮN. Quãng ở giữa thì nội " +
                 "suy giữa hai giá trị.\n\n" +
                 "Đây là ô chữa đúng cái cảm giác 'ô sát thanh màu bay giật': quãng ngắn " +
                 "chỉ có chừng 0.3 giây, mà vẫn phải co từ cỡ 5 về 1 thì mắt đọc ra là " +
                 "búng chứ không phải bay.")]
        [SerializeField] private float _nearStartScale = 1.2f;

        [Tooltip("Cỡ viên ở thời điểm chạm ô, trước khi nở về đúng 1. Hơi nhỏ hơn 1 rồi " +
                 "giãn ra là thứ làm cú đáp đọc ra 'êm' thay vì 'dừng phựt'.")]
        [SerializeField] private float _settleScale = 0.92f;

        [Tooltip("Phần cuối của quãng bay dành cho pha nở về 1, tính theo tỉ lệ. " +
                 "Để 0 là bỏ hẳn pha đáp.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _settlePortion = 0.18f;

        [SerializeField] private Ease _scaleEase = Ease.InOutSine;

        [Header("Hiện dần")]
        [Tooltip("Phần đầu quãng bay dành cho việc hiện dần từ trong suốt, tính theo " +
                 "tỉ lệ. Để 0 là hiện ngay tức khắc.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _fadeInPortion = 0.2f;

        [Header("Giới hạn")]
        [Tooltip("Số viên bay cùng lúc tối đa. Vượt quá thì ô vẫn được tô, chỉ là " +
                 "ngọc hiện ngay không có hiệu ứng.")]
        [SerializeField] private int _maxConcurrent = 24;

        [SerializeField] private int _prewarmCount = 24;

        [Tooltip("Order in Layer của viên NGỌC ĐANG BAY, để nó nổi trên mọi lớp của bảng. " +
                 "Đáp xuống thì trả về giá trị gốc của prefab.")]
        [SerializeField] private int _flyingSortingOrder = 15;

        private readonly Stack<SpriteRenderer> _pool = new();
        private readonly Dictionary<SpriteRenderer, Tween> _tweens = new();
        private readonly HashSet<Vector2Int> _inFlight = new();

        /// Đếm số viên đang bay theo TỪNG MÀU. Cần đếm riêng vì "màu này đã xong chưa"
        /// không suy được từ _inFlight — muốn biết thì phải tra màu của từng ô đang bay.
        private readonly Dictionary<int, int> _inFlightByPalette = new();

        private readonly HashSet<string> _warnings = new();

        private BoardView _boardView;
        private IPaintService _paintService;
        private IPaintOriginProvider _originProvider;

        /// Order in Layer gốc của prefab, đọc một lần để trả về đúng giá trị đó.
        private int _baseSortingOrder;
        private bool _hasBaseSortingOrder;

        /// Bắn khi ô đã thật sự có ngọc — JewelLayer nghe cái này.
        public event Action<Vector2Int, int> OnJewelLanded;

        public void Init(BoardView boardView, IPaintService paintService, IPaintOriginProvider originProvider)
        {
            _boardView = boardView;
            _paintService = paintService;
            _originProvider = originProvider;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _paintService.OnCellPainted += HandleCellPainted;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
            if (_paintService != null) _paintService.OnCellPainted -= HandleCellPainted;

            KillAllTweens();
        }

        /// Ô đang có viên bay tới thì JewelLayer chưa được hiện ngọc ở đó.
        public bool IsInFlight(Vector2Int cell) => _inFlight.Contains(cell);

        /// Màu này còn viên nào đang bay giữa trời không.
        ///
        /// Khác hẳn RemainingFor của IPaintService: con số đó giảm ngay lúc BẤM, còn cái
        /// này chỉ về 0 khi viên cuối cùng đã ĐÁP XUỐNG. Hiệu ứng ăn mừng phải hỏi cái
        /// này, không thì nó nổ trong lúc vài viên vẫn đang trên đường.
        public bool HasInFlight(int paletteIndex)
        {
            return _inFlightByPalette.TryGetValue(paletteIndex, out var count) && count > 0;
        }

        private void HandleBoardRebuilt()
        {
            KillAllTweens();
            _inFlight.Clear();
            _inFlightByPalette.Clear();
            Prewarm();
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            if (!TryStartFlight(cell, paletteIndex)) Land(cell, paletteIndex);
        }

        private bool TryStartFlight(Vector2Int cell, int paletteIndex)
        {
            var layout = _boardView.Layout;
            var colors = _boardView.Colors;

            if (layout == null || colors == null) return false;
            if (paletteIndex < 0 || paletteIndex >= colors.Count) return false;
            if (_tweens.Count >= _maxConcurrent) return false;

            if (_originProvider == null || !_originProvider.TryGetOriginWorldPosition(paletteIndex, out var origin))
            {
                // Không có điểm xuất phát thì ngọc hiện ngay, không bay. Im lặng ở đây
                // là kiểu hỏng khó chịu nhất: game vẫn chạy, chỉ mất hiệu ứng mà không
                // biết vì sao. Báo một lần rồi thôi.
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

            // Cỡ xuất phát đi theo quãng đường, không phải một hằng số.
            //
            // Quãng ngắn dù có nới thời gian tới đâu cũng chỉ được vài phần mười giây,
            // mà nếu vẫn phải co từ cỡ 5 về 1 thì TỐC ĐỘ ĐỔI CỠ vọt lên gấp mấy lần một
            // cú bay dài. Mắt bắt nhịp đó chứ không bắt quãng đường, nên nó đọc ra là
            // búng chứ không phải bay.
            flyer.transform.localScale = Vector3.one * Mathf.Lerp(_nearStartScale, _startScale, reach);

            _inFlight.Add(cell);
            AddInFlight(paletteIndex, 1);

            var duration = ResolveDuration(distance);
            var settleTime = duration * Mathf.Clamp01(_settlePortion);
            var travelTime = duration - settleTime;

            // DOMove chứ không DOJump: DOJump tách trục Y ra tween riêng với easing khác
            // trục X, nên kể cả đặt jumpPower = 0 thì hai trục vẫn chạy lệch nhịp và
            // đường bay vẫn cong. DOMove nội suy thẳng giữa hai điểm.
            var sequence = DOTween.Sequence();

            var moveEase = reach <= _nearEaseReach ? _nearMoveEase : _moveEase;

            sequence.Insert(0f, flyer.transform.DOMove(target, duration).SetEase(moveEase));

            // Hai tween scale nối đuôi nhau, KHÔNG chồng thời gian: co về settleScale
            // suốt quãng bay, rồi nở về 1 ở đoạn cuối. Chồng nhau thì DOTween để tween
            // sau đè tween trước và pha co bị nuốt mất.
            if (settleTime > 0f && !Mathf.Approximately(_settleScale, 1f))
            {
                sequence.Insert(0f, flyer.transform.DOScale(_settleScale, travelTime).SetEase(_scaleEase));
                sequence.Insert(travelTime, flyer.transform.DOScale(1f, settleTime).SetEase(Ease.OutSine));
            }
            else
            {
                sequence.Insert(0f, flyer.transform.DOScale(1f, duration).SetEase(_scaleEase));
            }

            var fadeTime = duration * Mathf.Clamp01(_fadeInPortion);
            if (fadeTime > 0f) sequence.Insert(0f, CreateFadeIn(flyer, fadeTime));

            sequence.OnComplete(() =>
            {
                Release(flyer);

                // Gỡ khỏi sổ TRƯỚC khi bắn sự kiện: người nghe hỏi ngay "màu này còn
                // viên nào đang bay không", mà lúc đó chính viên này đã hạ cánh rồi.
                _inFlight.Remove(cell);
                AddInFlight(paletteIndex, -1);

                Land(cell, paletteIndex);
            });

            _tweens[flyer] = sequence;
            return true;
        }

        /// Giữ TỐC ĐỘ đều thay vì giữ thời gian đều. Thời gian cố định làm ô ngay sát
        /// thanh màu bay lừ đừ còn ô ở mép bảng thì lao vun vút — mắt đọc ra ngay là
        /// hai chuyển động khác nhau, và đó chính là cái làm hiệu ứng thấy gợn.
        private float ResolveDuration(float distance)
        {
            var reference = Mathf.Max(0.01f, _referenceDistance);

            // Số mũ < 1 làm đường cong LÕM: quãng ngắn được chia phần thời gian rộng
            // rãi hơn tỉ lệ của nó. Tỉ lệ thẳng (số mũ 1) thì ô sát thanh màu chỉ được
            // 1/8 thời gian của ô ở mép bảng — quá gấp để đọc ra là một cú bay.
            var factor = Mathf.Pow(distance / reference, Mathf.Clamp01(_durationFalloff));
            var scaled = _duration * factor;

            var min = Mathf.Max(0.01f, _minDuration);
            var max = Mathf.Max(min, _maxDuration);

            var variance = 1f + UnityEngine.Random.Range(-_durationVariance, _durationVariance);

            return Mathf.Clamp(scaled, min, max) * variance;
        }

        /// Hiện dần bằng DOTween.To trên alpha thay vì SpriteRenderer.DOFade: DOFade cho
        /// SpriteRenderer nằm trong module Sprite của DOTween, mà project cố ý chỉ dùng
        /// phần core để khỏi phải khai thêm assembly.
        private static Tween CreateFadeIn(SpriteRenderer flyer, float duration)
        {
            var color = flyer.color;
            var targetAlpha = color.a;

            color.a = 0f;
            flyer.color = color;

            return DOTween.To(
                    () => flyer.color.a,
                    alpha =>
                    {
                        var current = flyer.color;
                        current.a = alpha;
                        flyer.color = current;
                    },
                    targetAlpha,
                    duration)
                .SetEase(Ease.OutSine);
        }

        /// Ô chỉ đổi từ xám sang màu thật ở đây, không phải lúc người chơi bấm.
        private void Land(Vector2Int cell, int paletteIndex)
        {
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

        /// Tô là hành động lặp liên tục — cảnh báo mỗi lần sẽ ngập Console.
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
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_jewelPrefab == null) return null;

            CacheBaseSortingOrder();

            return Instantiate(_jewelPrefab, _root);
        }

        /// Tween phải chết TRƯỚC khi viên quay về kho. Object tái sử dụng mang theo
        /// tween cũ sẽ bị kéo về vị trí của lần bay trước.
        ///
        /// Trả lại đủ những gì đã đổi lúc thuê: tween, scale, sortingOrder. Sót một cái
        /// là lần bay sau thừa hưởng trạng thái cũ.
        private void Release(SpriteRenderer flyer)
        {
            if (_tweens.TryGetValue(flyer, out var tween))
            {
                tween.Kill();
                _tweens.Remove(flyer);
            }

            Recycle(flyer);
        }

        private void KillAllTweens()
        {
            foreach (var pair in _tweens)
            {
                pair.Value?.Kill();

                if (pair.Key == null) continue;

                Recycle(pair.Key);
            }

            _tweens.Clear();
            _inFlightByPalette.Clear();
        }

        /// MỘT nơi duy nhất trả viên về kho, cho cả hai đường (đáp xuống và đổi màn).
        ///
        /// Trước đây hai đường tự reset riêng và đã có lần sót sortingOrder. Mỗi lần
        /// thêm một thứ bị đổi lúc thuê — scale, sortingOrder, giờ là alpha — là một
        /// lần nữa phải nhớ sửa cả hai chỗ. Gộp lại thì không còn chỗ để sót.
        private void Recycle(SpriteRenderer flyer)
        {
            flyer.transform.localScale = Vector3.one;
            flyer.sortingOrder = _baseSortingOrder;

            var color = flyer.color;
            color.a = 1f;
            flyer.color = color;

            flyer.gameObject.SetActive(false);
            _pool.Push(flyer);
        }

        /// Đọc từ prefab, không đọc từ viên đang dùng — viên đó đã bị đổi sang
        /// _flyingSortingOrder rồi, lấy về là lưu nhầm giá trị bay làm giá trị gốc.
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
                flyer.gameObject.SetActive(false);
                _pool.Push(flyer);
            }
        }
    }
}
