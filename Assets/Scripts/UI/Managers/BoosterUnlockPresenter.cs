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
    /// Báo booster vừa mở khoá khi vào màn đạt mốc.
    public class BoosterUnlockPresenter : ITickable, IDisposable
    {
        private const float QuietSeconds = 0.4f;

        private readonly ILevelService _levelService;
        private readonly IPopupService _popupService;
        private readonly BoosterUnlockConfig _config;
        private readonly PlayerProgress _progress;
        private readonly ISaveService _save;

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

        /// Huỷ đăng ký sự kiện khi scope bị huỷ.
        public void Dispose()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        private void HandleLevelStarted(int levelId)
        {
            if (_config == null || _progress == null) return;

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

            if (_popupService.IsAnyVisible())
            {
                _quietElapsed = 0f;
                return;
            }

            _quietElapsed += Time.unscaledDeltaTime;
            if (_quietElapsed < QuietSeconds) return;

            _quietElapsed = 0f;

            var booster = _pending.Dequeue();

            MarkShown(booster);

            var key = _config.UnlockPopupFor(booster);

            if (key == PopupKey.None) return;

            _popupService.Show(key);
        }

        private bool WasShown(CreditPoolKind booster) => _save.GetInt(Key(booster), 0) != 0;

        private void MarkShown(CreditPoolKind booster)
        {
            _save.SetInt(Key(booster), 1);
            _save.Save();
        }

        /// Key lưu trữ cho một booster.
        private static string Key(CreditPoolKind booster)
        {
            return PreferenceKeys.BoosterUnlockShownPrefix + (int)booster;
        }
    }
}
