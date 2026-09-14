using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Components
{
    /// Màn che chuyển cảnh: quét vào che kín màn hình, đổi màn hình ở phía sau, rồi quét
    /// ra. Hình dáng quét do ảnh mask quyết định — xem shader JewelPainter/UI/ScreenTransition.
    ///
    /// Vì sao che rồi mới đổi, chứ không mờ chồng hai màn hình lên nhau: Home dựng lại cả
    /// danh sách màn, đo bố cục, có khi sinh cả ảnh thu nhỏ. Việc đó tốn vài frame và
    /// không frame nào trong số đó đẹp. Mờ chồng là phơi hết ra; che đi là giấu nó vào
    /// đúng lúc không ai nhìn được, và tiện thể cho phần nặng nhất một chỗ để chạy.
    ///
    /// Không giữ trạng thái nào của màn chơi, không biết Home hay popup là gì. Ai cần
    /// chuyển cảnh thì đưa vào một hàm gọi lại — nó chạy ở đúng khoảnh khắc màn hình đang
    /// bị che kín.
    [RequireComponent(typeof(Image))]
    public class ScreenTransition : MonoBehaviour
    {
        [Tooltip("Image phủ TRỌN màn hình, dùng material của shader " +
                 "JewelPainter/UI/ScreenTransition.\n\n" +
                 "**Để trống ô Source Image.** Image không sprite vẫn dựng một tấm vuông với " +
                 "UV chạy 0..1, đúng thứ shader cần. Gán sprite vào là UV nhảy sang toạ độ " +
                 "trong atlas và ảnh mask bị đọc sai chỗ.\n\n" +
                 "Canvas chứa nó phải có Sorting Order CAO NHẤT — nó che mọi thứ, kể cả popup.\n\n" +
                 "Component này nên nằm trên CHÍNH object đó. Lúc rảnh nó tắt Image chứ không " +
                 "tắt GameObject, nên đặt ở đâu cũng chạy.")]
        [SerializeField] private Image _overlay;

        [Tooltip("Thời gian quét VÀO (che kín).")]
        [SerializeField] private float _coverDuration = 0.35f;

        [Tooltip("Thời gian quét RA (lộ màn hình mới).")]
        [SerializeField] private float _revealDuration = 0.4f;

        [Tooltip("Giữ màn hình che kín thêm ngần này giây trước khi quét ra.\n\n" +
                 "Không phải để câu giờ: đây là chỗ duy nhất Home dựng xong danh sách mà " +
                 "người chơi không thấy một frame dở dang nào. Để 0 thì mép quét ra có thể " +
                 "đuổi kịp đúng lúc bố cục còn đang nhảy.")]
        [SerializeField] private float _holdSeconds = 0.08f;

        [Tooltip("Đường cong của cú quét vào.")]
        [SerializeField] private Ease _coverEase = Ease.InQuad;

        [Tooltip("Đường cong của cú quét ra.")]
        [SerializeField] private Ease _revealEase = Ease.OutQuad;

        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        /// Bản sao material của riêng object này.
        ///
        /// PHẢI là bản sao: `_overlay.material` trỏ thẳng tới asset trong Project, và ghi
        /// vào nó là ghi vào file trên đĩa — trong Editor thì giá trị _Cutoff của lần chạy
        /// trước còn nằm lại ở lần sau, còn trong build thì mọi Image dùng chung material
        /// đó cùng bị kéo theo.
        ///
        /// MaterialPropertyBlock không thay thế được ở đây: CanvasRenderer không đọc nó.
        private Material _material;

        private Coroutine _routine;

        /// Đang có một cú chuyển cảnh chạy dở. Bên gọi nên bỏ qua cú bấm thứ hai thay vì
        /// xếp hàng — hai lượt chồng nhau nghĩa là màn hình bị che hai lần liên tiếp.
        public bool IsPlaying => _routine != null;

        private void Awake()
        {
            if (_overlay == null) _overlay = GetComponent<Image>();

            if (_overlay == null) return;

            _material = new Material(_overlay.material);
            _overlay.material = _material;

            SetCutoff(0f);

            // Tắt IMAGE, KHÔNG tắt GameObject.
            //
            // Component này sống trên chính object mang tấm che, nên tắt GameObject là tự
            // tắt mình — và lần đầu ai đó gọi Play thì Unity ném thẳng "Coroutine couldn't
            // be started because the game object is inactive". Lỗi chỉ nổ ra ở đúng cú bấm
            // Continue chứ không nổ lúc dựng, nên nó rất dễ lọt qua.
            //
            // Tắt Image đã đủ: một Graphic disabled không dựng mesh, không đi qua đường vẽ
            // và không nhận raycast. Vẫn đặt raycastTarget về false cho chắc — một tấm phủ
            // kín màn hình mà lỡ nuốt chạm thì cả game đứng, và không có gì báo.
            _overlay.raycastTarget = false;
            _overlay.enabled = false;
        }

        private void OnDestroy()
        {
            // Material tạo bằng new thì không ai dọn hộ — nó rò ra mỗi lần đổi scene.
            if (_material != null) Destroy(_material);
        }

        /// Quét vào cho tới khi che kín, chạy `onCovered`, giữ một nhịp rồi quét ra.
        ///
        /// `onCovered` chạy ở đúng frame màn hình đục hoàn toàn — đây là chỗ để đổi màn
        /// hình, dựng lại danh sách, ẩn popup. Nó được gọi kể cả khi có gì đó hỏng ở giữa
        /// chừng: một cú chuyển cảnh không hoàn hảo còn hơn một người chơi kẹt trước cái
        /// popup không chịu đóng.
        public void Play(Action onCovered)
        {
            if (_overlay == null || _material == null)
            {
                onCovered?.Invoke();
                return;
            }

            if (_routine != null) StopCoroutine(_routine);

            // Lưới an toàn cho trường hợp component được đặt trên object CHA và ai đó tắt
            // object tấm che trong scene. Đặt ở đây chứ không ở Awake vì Awake không bao
            // giờ chạy trên một object đang tắt.
            if (!_overlay.gameObject.activeSelf) _overlay.gameObject.SetActive(true);

            _routine = StartCoroutine(PlayRoutine(onCovered));
        }

        private IEnumerator PlayRoutine(Action onCovered)
        {
            // Đo lại mỗi lần chạy, không đo một lần ở Awake: máy xoay màn, chia đôi màn,
            // hay đơn giản là Game view bị kéo trong Editor đều đổi tỉ lệ này. Một phép
            // chia mỗi cú chuyển cảnh thì không đáng để đi cache.
            UpdateAspect();

            _overlay.enabled = true;

            // Chạm màn hình bị khoá suốt cú chuyển: nửa đường mà bấm trúng nút của màn
            // hình cũ thì nó vẫn ăn, dù người chơi không còn nhìn thấy nút đó nữa.
            _overlay.raycastTarget = true;

            yield return Sweep(0f, 1f, _coverDuration, _coverEase);

            onCovered?.Invoke();

            // Nhường một frame TRƯỚC khi đếm giờ giữ: Home vừa được bật trong onCovered,
            // mà Layout Group và Content Size Fitter tính lại kích thước ở cuối frame.
            yield return null;

            if (_holdSeconds > 0f) yield return new WaitForSecondsRealtime(_holdSeconds);

            yield return Sweep(1f, 0f, _revealDuration, _revealEase);

            _overlay.raycastTarget = false;
            _overlay.enabled = false;

            _routine = null;
        }

        /// Tween thời gian THẬT (SetUpdate(true)): cú chuyển cảnh hay chạy đúng lúc game
        /// đang dừng vì một popup, mà một màn che đứng hình thì không phải chuyển cảnh.
        private IEnumerator Sweep(float from, float to, float duration, Ease ease)
        {
            if (duration <= 0f)
            {
                SetCutoff(to);
                yield break;
            }

            var tween = DOVirtual.Float(from, to, duration, SetCutoff)
                .SetEase(ease)
                .SetUpdate(true);

            yield return tween.WaitForCompletion();
        }

        /// Tỉ lệ rộng/cao của tấm che, để shader giữ mask tròn cho ra hình tròn.
        ///
        /// Đọc từ rect của chính tấm che chứ không từ Screen.width/height: tấm che có thể
        /// không phủ trọn màn (safe area chẳng hạn), và thứ shader cần là tỉ lệ của cái
        /// hình nó đang vẽ lên.
        private void UpdateAspect()
        {
            if (_material == null || _overlay == null) return;

            var rect = _overlay.rectTransform.rect;
            if (rect.height <= 0f) return;

            _material.SetFloat(AspectId, rect.width / rect.height);
        }

        private void SetCutoff(float value)
        {
            if (_material == null) return;

            _material.SetFloat(CutoffId, value);
        }
    }
}
