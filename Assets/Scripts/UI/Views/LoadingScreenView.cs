using System.Collections;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Màn hình chờ lúc mở game: hiện lên trước, nạp màn đang chơi dở, rồi tắt.
    ///
    /// Việc nạp màn hiện nay chạy đồng bộ và gần như tức thì, nên màn này chủ yếu để
    /// che cú dựng bàn chơi và cho người chơi một nhịp chuyển. Khi nào có nạp asset
    /// thật thì chỗ chờ đã sẵn ở đây rồi.
    public class LoadingScreenView : MonoBehaviour
    {
        [Tooltip("Object bị ẩn khi xong. Để trống thì ẩn chính object này.")]
        [SerializeField] private GameObject _content;

        [Tooltip("Hiện ít nhất ngần này giây, kể cả khi nạp xong sớm hơn. Loé một cái " +
                 "rồi tắt còn khó chịu hơn là không có màn chờ nào.")]
        [SerializeField] private float _minimumSeconds = 0.8f;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Thanh tiến trình. Image phải đặt Image Type = Filled — code chỉ gán " +
                 "fillAmount.")]
        [SerializeField] private Image _progressFill;

        private ILevelService _levelService;

        /// GameEntryPoint gọi. Đây là thứ thay cho lời gọi LoadLevel lúc khởi động.
        public void Begin(ILevelService levelService)
        {
            _levelService = levelService;

            SetVisible(true);
            SetProgress(0f);

            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            // Nhường MỘT frame trước khi nạp. Nạp màn chạy đồng bộ, nên gọi thẳng ở đây
            // là cả việc hiện màn chờ lẫn việc dựng bàn rơi vào cùng một frame — người
            // chơi không bao giờ thấy màn chờ, chỉ thấy game đứng hình một nhịp.
            yield return null;

            var startTime = Time.unscaledTime;

            _levelService.LoadLevel(_levelService.CurrentLevel);

            var minimum = Mathf.Max(0f, _minimumSeconds);

            while (true)
            {
                var elapsed = Time.unscaledTime - startTime;

                SetProgress(minimum > 0f ? elapsed / minimum : 1f);

                if (elapsed >= minimum) break;

                yield return null;
            }

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
