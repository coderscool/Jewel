using System;
using System.Collections.Generic;
using JewelPainter.Core.Persistence;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.UI.Data;
using JewelPainter.UI.Definitions;
using JewelPainter.UI.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace JewelPainter.UI.Managers
{
    /// Báo "vừa mở khoá booster" ở lần vào màn đầu tiên sau khi tiến trình chạm mốc.
    ///
    /// Thuần C#, không phải MonoBehaviour — cùng khuôn với RatePopupPresenter và cùng lý
    /// do: nó không có gì để đặt trong scene, ITickable của VContainer đã cấp nhịp Update.
    ///
    /// Không mở popup ngay trong lượt xử lý OnLevelStarted. Lúc đó màn hình chờ có thể
    /// còn đang che, popup thắng màn trước có thể chưa tan hết, và một popup chen vào
    /// giữa hai thứ đó đọc ra như lỗi chứ không như phần thưởng. Ghi một cờ rồi đợi màn
    /// hình sạch — y hệt cách lời mời đánh giá đang làm.
    public class BoosterUnlockPresenter : ITickable, IDisposable
    {
        /// Chờ thêm ngần này giây sau khi màn hình sạch.
        private const float QuietSeconds = 0.4f;

        private readonly ILevelService _levelService;
        private readonly IPopupService _popupService;
        private readonly BoosterUnlockConfig _config;
        private readonly PlayerProgress _progress;
        private readonly ISaveService _save;

        /// Những booster đã tới mốc mà chưa kịp báo. Xếp hàng chứ không báo gộp: hai
        /// booster mở cùng một màn thì người chơi phải đọc được cả hai, mà một popup chỉ
        /// kể được một cái.
        private readonly Queue<CreditPoolKind> _pending = new();

        private float _quietElapsed;

        public BoosterUnlockPresenter(
            ILevelService levelService,
            IPopupService popupService,
            BoosterUnlockConfig config,
            PlayerProgress progress,
            ISaveService save)
        {
            _levelService = levelService;
            _popupService = popupService;
            _config = config;
            _progress = progress;
            _save = save;

            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        /// VContainer gọi khi scope bị huỷ. Thay cho OnDestroy của MonoBehaviour — thiếu
        /// nó là rò rỉ event đúng nghĩa.
        public void Dispose()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId)
        {
            if (_config == null || _progress == null) return;

            // So với TIẾN TRÌNH CAO NHẤT, không phải màn đang chơi — cùng mốc mà HudView
            // dùng để quyết định nút nào còn khoá. Hai bên lệch mốc thì sẽ có lúc popup
            // báo mở khoá một cái nút vẫn đang đeo ổ khoá.
            var level = _progress.Level;

            foreach (CreditPoolKind booster in Enum.GetValues(typeof(CreditPoolKind)))
            {
                if (!_config.IsUnlocked(booster, level)) continue;
                if (WasShown(booster)) continue;
                if (_pending.Contains(booster)) continue;

                _pending.Enqueue(booster);
            }

            _quietElapsed = 0f;
        }

        public void Tick()
        {
            if (_pending.Count == 0) return;

            // Còn popup nào đang mở thì đếm lại từ đầu — kể cả popup người chơi tự mở.
            if (_popupService.IsAnyVisible())
            {
                _quietElapsed = 0f;
                return;
            }

            // Thời gian KHÔNG theo timeScale: khoảng lặng này là chuyện của người xem.
            _quietElapsed += Time.unscaledDeltaTime;
            if (_quietElapsed < QuietSeconds) return;

            _quietElapsed = 0f;

            var booster = _pending.Dequeue();

            // Ghi cờ NGAY, trước cả khi popup kịp hiện.
            //
            // Popup có thể không mở được — thiếu prefab trong PopupConfig chẳng hạn. Ghi
            // sau thì lần vào màn nào cũng thử lại và hàng chờ không bao giờ vơi, tức là
            // một lỗi cấu hình im lặng biến thành một vòng lặp mỗi frame.
            MarkShown(booster);

            // Mỗi booster một popup riêng, và BẢNG MỐC nói cái nào gọi cái nào. Presenter
            // không tra tên popup theo booster: thêm booster thứ tư thì chỉ phải thêm một
            // dòng vào asset, không phải mở file này ra sửa.
            var key = _config.UnlockPopupFor(booster);

            // None là mở khoá im lặng — nút chỉ đơn giản hết ổ khoá. Vẫn đã ghi cờ ở trên
            // nên nó không quay lại hỏi nữa.
            if (key == PopupKey.None) return;

            _popupService.Show(key);
        }

        private bool WasShown(CreditPoolKind booster) => _save.GetInt(Key(booster), 0) != 0;

        private void MarkShown(CreditPoolKind booster)
        {
            _save.SetInt(Key(booster), 1);
            _save.Save();
        }

        /// Nối SỐ của enum, không nối tên. Xem PreferenceKeys.BoosterUnlockShownPrefix.
        private static string Key(CreditPoolKind booster)
        {
            return PreferenceKeys.BoosterUnlockShownPrefix + (int)booster;
        }
    }
}
