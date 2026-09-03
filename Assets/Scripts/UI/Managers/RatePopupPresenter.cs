using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace JewelPainter.UI.Managers
{
    /// Mở popup mời đánh giá sau mỗi vài màn — nhưng CHỜ tới lúc màn hình sạch.
    ///
    /// Không mở ngay ở OnLevelCleared: đúng lúc đó popup thắng màn cũng đang bật lên, và
    /// hai popup chồng nhau thì cái nào cũng đọc không ra. Cái ở dưới còn ăn mất cú chạm
    /// của cái ở trên. Nên chỉ ghi một cờ "đang chờ", rồi đợi người chơi đóng hết mọi
    /// popup mới hỏi.
    ///
    /// Class thuần C#, KHÔNG phải MonoBehaviour: nó không có gì để đặt trong scene, và
    /// ITickable của VContainer cho nó nhịp Update mà không cần dựng một GameObject rỗng
    /// chỉ để treo script. Khác WinPopupPresenter ở chỗ đó, và khác là có lý do.
    public class RatePopupPresenter : ITickable, IDisposable
    {
        /// Chờ thêm ngần này giây sau khi màn hình sạch. Popup nhảy ra đúng khoảnh khắc
        /// popup trước vừa biến mất đọc ra như một cú lỗi, không phải như một lời mời.
        private const float QuietSeconds = 0.6f;

        private readonly ILevelFlowService _levelFlow;
        private readonly IPopupService _popupService;
        private readonly RatePrompt _prompt;

        private bool _isPending;
        private float _quietElapsed;

        public RatePopupPresenter(
            ILevelFlowService levelFlow,
            IPopupService popupService,
            RatePrompt prompt)
        {
            _levelFlow = levelFlow;
            _popupService = popupService;
            _prompt = prompt;

            _levelFlow.OnLevelCleared += HandleLevelCleared;
        }

        /// VContainer gọi khi scope bị huỷ. Chỗ này thay cho OnDestroy của MonoBehaviour —
        /// thiếu nó là rò rỉ event đúng nghĩa.
        public void Dispose()
        {
            if (_levelFlow != null) _levelFlow.OnLevelCleared -= HandleLevelCleared;
        }

        private void HandleLevelCleared()
        {
            if (_prompt == null || !_prompt.RegisterLevelCleared()) return;

            _isPending = true;
            _quietElapsed = 0f;
        }

        public void Tick()
        {
            if (!_isPending) return;

            // Còn popup nào đang mở thì đếm lại từ đầu — kể cả popup người chơi tự mở như
            // Cài đặt hay Bộ sưu tập. Lời mời đánh giá không được chen ngang việc gì cả.
            if (_popupService.IsAnyVisible())
            {
                _quietElapsed = 0f;
                return;
            }

            // Thời gian KHÔNG theo timeScale: khoảng lặng này là chuyện của người xem,
            // không phải chuyện của thế giới trong game.
            _quietElapsed += Time.unscaledDeltaTime;
            if (_quietElapsed < QuietSeconds) return;

            _isPending = false;
            _quietElapsed = 0f;

            // Đếm lại từ đầu ĐÚNG LÚC popup hiện, không phải lúc đủ màn — xem chú thích
            // của RatePrompt.RegisterLevelCleared.
            _prompt.MarkPrompted();

            _popupService.Show(PopupKey.Rate);
        }
    }
}
