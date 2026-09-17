#if CHEAT_ENABLED
using System.Collections;
using System.Collections.Generic;
using CoreModules.CheatKit.Ports;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Views;
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// Adapter nối các port của CheatKit với API thật của game.
    public sealed class JewelPainterCheatBridge :
        ILevelCheatService, IFlowCheatService, IProgressCheatService, IJewelPainterCheatService
    {
        private const int CellsPerFrame = 48;

        private readonly ILevelService _levelService;
        private readonly IPaintService _paintService;
        private readonly PaintProgressStore _paintStore;
        private readonly PlayerProgress _progress;
        private readonly PlayerWallet _wallet;
        private readonly HintCredits _hintCredits;
        private readonly FreePaintCredits _freePaintCredits;
        private readonly FillColorCredits _fillColorCredits;
        private readonly CheatRunner _runner;

        private readonly CanvasGroup _hudGroup;

        private readonly HashSet<string> _warnings = new();

        private Coroutine _fill;

        public JewelPainterCheatBridge(
            ILevelService levelService,
            IPaintService paintService,
            PaintProgressStore paintStore,
            PlayerProgress progress,
            PlayerWallet wallet,
            HintCredits hintCredits,
            FreePaintCredits freePaintCredits,
            FillColorCredits fillColorCredits,
            HudView hud,
            CheatRunner runner)
        {
            _levelService = levelService;
            _paintService = paintService;
            _paintStore = paintStore;
            _progress = progress;
            _wallet = wallet;
            _hintCredits = hintCredits;
            _freePaintCredits = freePaintCredits;
            _fillColorCredits = fillColorCredits;
            _runner = runner;

            if (hud != null) _hudGroup = hud.GetComponent<CanvasGroup>();

            if (_hudGroup == null)
            {
                Debug.LogWarning("[Cheat] Không thấy CanvasGroup trên HudView — nút giấu HUD " +
                                 "sẽ xám đi. Thêm CanvasGroup vào object Canvas-HUD.");
            }
        }

        public int Count => _levelService?.Levels?.Count ?? 0;

        public int CurrentIndex
        {
            get
            {
                var levels = _levelService?.Levels;
                var current = _levelService?.CurrentConfig;

                if (levels == null || current == null) return -1;

                for (var i = 0; i < levels.Count; i++)
                {
                    if (levels[i] == current) return i;
                }

                return -1;
            }
        }

        public bool IsReady => _levelService?.CurrentGrid != null;

        public string NameOf(int index)
        {
            var config = ConfigAt(index);

            return config != null ? $"Level {config.LevelId}" : string.Empty;
        }

        public void Load(int index)
        {
            var config = ConfigAt(index);
            if (config == null) return;

            LoadConfig(config);
        }

        public void ReloadCurrent() => LoadConfig(_levelService?.CurrentConfig);

        public CheatGamePhase Phase
        {
            get
            {
                if (!IsReady) return CheatGamePhase.Loading;

                return _paintService.IsComplete ? CheatGamePhase.Win : CheatGamePhase.Playing;
            }
        }

        public bool CanForceWin => Phase == CheatGamePhase.Playing;

        /// Tô nốt mọi ô còn lại, rải qua nhiều frame.
        public void ForceWin() => PaintCells(int.MaxValue);

        public void ForceLose() => WarnOnce(
            "JewelPainter không có trạng thái thua — Force Lose không làm gì. " +
            "Tranh tô dở chỉ nằm chờ, không có lượt đi và không có đồng hồ để hết giờ.");

        public void TriggerNoMoves() => WarnOnce(
            "JewelPainter không có khái niệm hết nước đi — Trigger No Moves không làm gì. " +
            "Ô nào cũng tô được miễn chọn đúng màu, nên không có thế bí để mà cứu.");

        public int Coins => _wallet?.Coins ?? -1;

        public void UnlockAll()
        {
            var highest = HighestLevelId();
            if (highest > 0) _progress?.SetLevel(highest);
        }

        public void SetProgress(int levelIndex)
        {
            var config = ConfigAt(levelIndex);
            if (config != null) _progress?.SetLevel(config.LevelId);
        }

        /// Cộng hoặc trừ xu trong ví.
        public void AddCoins(int amount)
        {
            if (_wallet == null || amount == 0) return;

            if (amount > 0) _wallet.Add(amount);
            else _wallet.TrySpend(-amount);
        }

        /// Xoá tiến độ tô của màn đang chơi rồi nạp lại.
        public void ClearSave()
        {
            _paintStore?.ResetCurrent();

            ReloadCurrent();
        }

        public int RemainingCells
        {
            get
            {
                var used = _paintService?.UsedPaletteIndices;
                if (used == null || !IsReady) return -1;

                var remaining = 0;

                for (var i = 0; i < used.Count; i++) remaining += _paintService.RemainingFor(used[i]);

                return remaining;
            }
        }

        public int HintCredits => _hintCredits?.Remaining ?? -1;

        public int FreePaintCredits => _freePaintCredits?.Remaining ?? -1;

        public int FillColorCredits => _fillColorCredits?.Remaining ?? -1;

        public bool IsFilling => _fill != null;

        public void PaintCells(int count)
        {
            if (count <= 0 || _runner == null || !IsReady) return;

            StopFilling();

            _fill = _runner.StartCoroutine(FillRoutine(count, singleColor: false));
        }

        public void PaintOneColor()
        {
            if (_runner == null || !IsReady) return;

            StopFilling();

            _fill = _runner.StartCoroutine(FillRoutine(int.MaxValue, singleColor: true));
        }

        public void StopFilling()
        {
            if (_fill == null) return;

            _runner.StopCoroutine(_fill);
            _fill = null;
        }

        public void AddHintCredits(int amount) => _hintCredits?.Grant(amount);

        public void AddFreePaintCredits(int amount) => _freePaintCredits?.Grant(amount);

        public void AddFillColorCredits(int amount) => _fillColorCredits?.Grant(amount);

        public bool IsHudHidden => _hudGroup != null && _hudGroup.alpha <= 0.001f;

        /// Ẩn hoặc hiện HUD bằng alpha.
        public void SetHudHidden(bool hidden)
        {
            if (_hudGroup == null) return;

            _hudGroup.alpha = hidden ? 0f : 1f;
        }

        /// Vòng tô hàng loạt.
        private IEnumerator FillRoutine(int count, bool singleColor)
        {
            var level = _levelService.CurrentConfig;

            var restore = _paintService.SelectedPaletteIndex;

            var only = singleColor ? ResolveTargetColor() : -1;

            if (!singleColor || only >= 0)
            {
                var painted = 0;
                var budget = CellsPerFrame;

                while (painted < count && _levelService.CurrentConfig == level)
                {
                    if (!TryPaintNextCell(only)) break;

                    painted++;

                    if (--budget > 0) continue;

                    budget = CellsPerFrame;
                    yield return null;
                }
            }

            if (restore >= 0 && _levelService.CurrentConfig == level) _paintService.SelectColor(restore);

            _fill = null;
        }

        /// Chọn màu để tô: màu đang chọn nếu còn ô, không thì màu đầu tiên còn ô.
        private int ResolveTargetColor()
        {
            var selected = _paintService.SelectedPaletteIndex;

            if (selected >= 0 && _paintService.RemainingFor(selected) > 0) return selected;

            return FirstColorWithRemaining();
        }

        private int FirstColorWithRemaining()
        {
            var used = _paintService.UsedPaletteIndices;
            if (used == null) return -1;

            for (var i = 0; i < used.Count; i++)
            {
                if (_paintService.RemainingFor(used[i]) > 0) return used[i];
            }

            return -1;
        }

        /// Tô ô kế tiếp; false khi không còn ô nào tô được.
        private bool TryPaintNextCell(int only)
        {
            var index = only >= 0 ? only : FirstColorWithRemaining();

            if (index < 0 || _paintService.RemainingFor(index) <= 0) return false;

            _paintService.SelectColor(index);

            if (!_paintService.TryGetUnpaintedCell(index, 0, out var cell)) return false;

            return _paintService.TryPaint(cell.x, cell.y);
        }

        private LevelConfig ConfigAt(int index)
        {
            var levels = _levelService?.Levels;

            if (levels == null || index < 0 || index >= levels.Count) return null;

            return levels[index];
        }

        /// Id màn lớn nhất đang khai.
        private int HighestLevelId()
        {
            var levels = _levelService?.Levels;
            if (levels == null) return 0;

            var highest = 0;

            for (var i = 0; i < levels.Count; i++)
            {
                var config = levels[i];
                if (config != null && config.LevelId > highest) highest = config.LevelId;
            }

            return highest;
        }

        /// Dừng cú tô đang chạy rồi nạp màn khác.
        private void LoadConfig(LevelConfig config)
        {
            if (config == null) return;

            StopFilling();

            _levelService.LoadLevel(config.LevelId);
        }

        /// Cảnh báo một lần cho cheat không làm được.
        private void WarnOnce(string message)
        {
            if (!_warnings.Add(message)) return;

            Debug.LogWarning($"[Cheat] {message}");
        }
    }
}
#endif
