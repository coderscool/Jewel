using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Màn hình chờ che cú dựng bàn chơi — MỌI lần nạp màn, không riêng lúc mở game.
    ///
    /// Không tự gọi LoadLevel nữa. Nó chỉ nghe hai mốc mà LevelManager bắn ra:
    ///   OnLevelLoadStarted — có yêu cầu nạp, bàn chưa dựng → hiện lên
    ///   OnLevelStarted     — bàn đã dựng xong             → đếm nốt thời gian tối thiểu rồi tắt
    ///
    /// Nhờ vậy nút Play ở Home, nút chơi lại và cheat đều được che như nhau, mà không
    /// nơi nào phải biết màn hình chờ tồn tại.
    ///
    /// Dùng Update chứ không dùng coroutine: SetVisible có thể tắt chính GameObject này
    /// khi Content để trống, mà coroutine trên một object đã tắt thì dừng giữa chừng.
    public class LoadingScreenView : MonoBehaviour
    {
        [Tooltip("Object bị ẩn khi xong. Để trống thì ẩn chính object này.")]
        [SerializeField] private GameObject _content;

        [Tooltip("Giữ màn chờ thêm ngần này giây TÍNH TỪ LÚC BÀN ĐÃ DỰNG XONG — không " +
                 "phải từ lúc màn chờ hiện lên.\n\n" +
                 "Đo từ lúc hiện lên là cái bẫy đã làm thanh tiến trình đứng im: cú dựng " +
                 "bàn chiếm trọn khoảng đó, nên đến lúc chạy được dòng cập nhật đầu tiên " +
                 "thì thời gian đã hết và thanh nhảy thẳng từ 0 sang tắt.\n\n" +
                 "Đo từ lúc dựng xong thì khoảng này LUÔN là khoảng thanh chạy thật, dù " +
                 "máy chậm tới đâu. Nó cũng chính là khoảng che cho phần dựng sẵn đang " +
                 "trải ra nhiều frame ở lớp số, lớp ngọc và lớp gợi ý.")]
        [SerializeField] private float _minimumSeconds = 0.8f;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Thanh tiến trình. Image phải đặt Image Type = Filled — code chỉ gán " +
                 "fillAmount.")]
        [SerializeField] private Image _progressFill;

        /// Phần dải dành cho quãng "đang dựng bàn". Quãng đó không cập nhật được thanh
        /// — nó nằm gọn trong một frame bị chiếm — nên thanh đứng yên ở đây một nhịp
        /// ngắn rồi mới chạy tiếp phần còn lại.
        private const float BuildProgressShare = 0.25f;

        private ILevelService _levelService;

        private bool _isShowing;
        private bool _isBoardBuilt;
        private float _builtAt;

        /// GameEntryPoint gọi một lần lúc nối dây. Từ đó về sau màn chờ tự chạy theo
        /// sự kiện, không ai phải gọi nó nữa.
        public void Bind(ILevelService levelService)
        {
            if (_levelService != null) Unbind();

            _levelService = levelService;

            _levelService.OnLevelLoadStarted += HandleLoadStarted;
            _levelService.OnLevelStarted += HandleBoardBuilt;

            SetVisible(false);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_levelService == null) return;

            _levelService.OnLevelLoadStarted -= HandleLoadStarted;
            _levelService.OnLevelStarted -= HandleBoardBuilt;

            _levelService = null;
        }

        private void HandleLoadStarted(int levelId)
        {
            _isShowing = true;
            _isBoardBuilt = false;

            SetVisible(true);
            SetProgress(0f);
        }

        /// Bàn đã dựng xong. ĐÂY mới là mốc bấm giờ cho khoảng giữ màn chờ.
        ///
        /// Bấm giờ từ lúc màn chờ hiện lên thì cú dựng bàn ăn hết khoảng đó, và dòng cập
        /// nhật thanh đầu tiên chỉ chạy được sau khi đã hết giờ — thanh nằm im ở 0 suốt
        /// lúc máy bận, rồi tắt ngay. Đó đúng là cảnh "thanh loading đơ rồi vào game luôn".
        private void HandleBoardBuilt(int levelId)
        {
            _isBoardBuilt = true;
            _builtAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (!_isShowing) return;

            // Chưa dựng xong: chỉ vài frame, giữ thanh ở đầu dải cho có gì đó khác 0.
            if (!_isBoardBuilt)
            {
                SetProgress(BuildProgressShare * 0.5f);
                return;
            }

            var minimum = Mathf.Max(0f, _minimumSeconds);
            var elapsed = Time.unscaledTime - _builtAt;
            var t = minimum > 0f ? Mathf.Clamp01(elapsed / minimum) : 1f;

            SetProgress(BuildProgressShare + (1f - BuildProgressShare) * t);

            if (elapsed < minimum) return;

            _isShowing = false;

            SetProgress(1f);
            SetVisible(false);
        }

        private void SetProgress(float value)
        {
            if (_progressFill == null) return;

            _progressFill.fillAmount = Mathf.Clamp01(value);
        }

        private void SetVisible(bool visible)
        {
            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
