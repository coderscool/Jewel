using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Làm mờ lớp màu theo mức zoom, để lộ số nằm dưới.
    ///
    /// Hai mốc là orthographicSize tuyệt đối, đặt sẵn trên component. LevelConfig có ô
    /// Fade Switch Size ghi đè một trong hai — dùng khi từng màn cần mốc khác nhau.
    ///
    /// Chiều mờ do thứ tự hai mốc quyết định, không có cờ bật tắt:
    ///   Opaque 0 (dùng mức lúc vào màn) / Transparent 12 → mờ dần khi phóng to,
    ///     mất hẳn ở size 12, zoom sát hơn nữa vẫn trong suốt
    ///   Opaque 12 / Transparent 20 → đục khi phóng sát, mờ dần khi kéo ra xa
    ///
    /// Lưu ý: BoardCamera kẹp mức zoom xa nhất đúng bằng mức lúc vào màn, nên
    /// orthographicSize không bao giờ vượt quá mốc đó.
    ///
    /// Một việc duy nhất: đọc mức zoom, tính alpha, ghi vào SpriteRenderer.
    /// Không đặt trong BoardCamera vì camera không nên với tay vào renderer của bảng,
    /// cũng không đặt trong BoardView vì view không nên biết camera tồn tại.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardColorFade : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private BoardView _boardView;

        [Tooltip("orthographicSize mà tại đó ảnh ĐỤC hoàn toàn (alpha 1). " +
                 "Để 0 nghĩa là dùng mức zoom lúc mới vào màn, tức mức xa nhất.")]
        [SerializeField] private float _opaqueSize;

        [Tooltip("orthographicSize mà tại đó ảnh MẤT HẲN (alpha 0). " +
                 "Để 0 nghĩa là dùng mức zoom lúc mới vào màn, tức mức xa nhất. " +
                 "Zoom qua khỏi mức này thì vẫn trong suốt, không mờ thêm nữa. " +
                 "Đặt LỚN hơn Opaque Size thì chiều mờ đảo lại: đục khi phóng sát, " +
                 "mờ dần khi kéo ra xa.")]
        [SerializeField] private float _transparentSize = 12f;

        [Tooltip("Ô Fade Switch Size trong LevelConfig sẽ ghi đè mốc nào của component này. " +
                 "Bỏ tick cho lớp màu (nó là mốc TAN HẾT), tick cho lớp viền ô " +
                 "(với viền thì cùng con số đó lại là mốc HIỆN ĐỦ).")]
        [SerializeField] private bool _levelSizeIsOpaque;

        /// Mức zoom lúc mới vào màn, dùng khi Opaque Size để 0.
        private float _baseSize = -1f;

        private bool _needsBaseCapture = true;
        private float _lastOrthographicSize = -1f;

        private void OnEnable()
        {
            _needsBaseCapture = true;
            _lastOrthographicSize = -1f;

            if (_boardView != null) _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDisable()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt() => _needsBaseCapture = true;

        private void LateUpdate()
        {
            if (_camera == null || _renderer == null) return;

            if (_needsBaseCapture)
            {
                // Lấy ở LateUpdate chứ không lấy ngay trong handler, để BoardCamera kịp
                // đặt lại mức zoom cho bảng mới trước đã. Không phụ thuộc thứ tự đăng ký event.
                _baseSize = _camera.orthographicSize;
                _needsBaseCapture = false;
                _lastOrthographicSize = -1f;
            }

            if (Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return;

            _lastOrthographicSize = _camera.orthographicSize;

            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            var color = _renderer.color;
            color.a = AlphaFor(_camera.orthographicSize);
            _renderer.color = color;
        }

        private float AlphaFor(float currentSize)
        {
            var opaque = _opaqueSize;
            var transparent = _transparentSize;

            // LevelConfig ghi đè một trong hai mốc, tuỳ vai của component này.
            var levelSize = LevelFadeSwitchSize();
            if (levelSize > 0f)
            {
                if (_levelSizeIsOpaque) opaque = levelSize;
                else transparent = levelSize;
            }

            // Mốc để 0 nghĩa là "dùng mức zoom lúc vào màn", và điều đó đúng với CẢ HAI
            // mốc: lớp màu dùng nó làm mốc ĐỤC, lớp viền dùng nó làm mốc TRONG SUỐT.
            //
            // Trước đây chỉ thay cho mốc đục, nên lớp viền đặt Transparent Size = 0 bị
            // hiểu là "trong suốt tại orthographicSize = 0" — một mức camera không bao
            // giờ tới. Kết quả là chiều mờ của viền lộn ngược: hiện rõ lúc kéo xa nhất
            // và mờ dần khi phóng to, đúng ngược với ý đồ.
            if (opaque <= 0f) opaque = _baseSize;
            if (transparent <= 0f) transparent = _baseSize;

            if (opaque <= 0f || transparent <= 0f) return 1f;

            // Hai mốc trùng nhau thì không có dải để nội suy — giữ đục.
            if (Mathf.Approximately(opaque, transparent)) return 1f;

            // InverseLerp chạy đúng cả khi mốc đầu lớn hơn mốc sau, và tự kẹp về 0..1.
            // Nhờ vậy một công thức phục vụ được cả hai chiều mờ, không cần rẽ nhánh.
            return 1f - Mathf.InverseLerp(opaque, transparent, currentSize);
        }

        /// 0 khi màn chưa nạp hoặc LevelConfig để trống ô đó.
        private float LevelFadeSwitchSize()
        {
            if (_boardView == null) return 0f;

            var config = _boardView.Config;

            return config != null ? config.FadeSwitchSize : 0f;
        }
    }
}
