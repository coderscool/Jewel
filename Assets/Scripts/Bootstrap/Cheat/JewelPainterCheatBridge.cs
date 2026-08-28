#if CHEAT_ENABLED
using System.Collections;
using System.Collections.Generic;
using CoreModules.CheatKit.Ports;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using UnityEngine;

namespace JewelPainter.Bootstrap.Cheat
{
    /// ADAPTER — map ba port chuẩn của CheatKit cộng port riêng của game sang API thật.
    ///
    /// Đây là chỗ DUY NHẤT biết cả hai phía. CheatKit không hề biết JewelPainter tồn tại,
    /// còn Gameplay không hề biết có cheat — bê kit sang project khác chỉ là viết lại
    /// đúng file này.
    ///
    /// Ba chỗ ngữ nghĩa của game KHÔNG khớp thẳng với port, và đó mới là phần đáng đọc:
    ///
    ///   - Port đánh số màn từ 0, game đánh theo LevelId trong LevelConfig (bắt đầu từ 1
    ///     và không có gì bắt phải liên tục). Nên index của port là VỊ TRÍ trong danh sách
    ///     Levels, còn LevelId chỉ xuất hiện ở phía game.
    ///
    ///   - ForceWin không được đi tắt. Bảng chỉ coi là xong khi PaintState đã ghi đủ ô, mà
    ///     luồng thắng thì treo trên OnJewelLanded. Ép thắng vì thế là TÔ THẬT từng ô qua
    ///     TryPaint — chậm hơn nhưng đi qua đúng mọi thứ người chơi đi qua: hiệu ứng ngọc
    ///     bay, loé màu xong, ăn mừng, ghi tiến trình, popup.
    ///
    ///   - JewelPainter KHÔNG THUA được. ForceLose và TriggerNoMoves là no-op có báo một
    ///     lần, chứ không phải bịa ra một trạng thái thua không tồn tại.
    public sealed class JewelPainterCheatBridge :
        ILevelCheatService, IFlowCheatService, IProgressCheatService, IJewelPainterCheatService
    {
        /// Số ô tô trong MỘT frame khi tô hàng loạt.
        ///
        /// Đủ nhỏ để frame không nghẹn, đủ lớn để tô kín bảng 72x72 (5184 ô) xong trong
        /// khoảng hai giây — người xem thấy tranh hiện dần, đúng thứ muốn nhìn.
        private const int CellsPerFrame = 48;

        private readonly ILevelService _levelService;
        private readonly IPaintService _paintService;
        private readonly PaintProgressStore _paintStore;
        private readonly PlayerProgress _progress;
        private readonly PlayerWallet _wallet;
        private readonly HintCredits _hintCredits;
        private readonly CheatRunner _runner;

        private readonly HashSet<string> _warnings = new();

        private Coroutine _fill;

        public JewelPainterCheatBridge(
            ILevelService levelService,
            IPaintService paintService,
            PaintProgressStore paintStore,
            PlayerProgress progress,
            PlayerWallet wallet,
            HintCredits hintCredits,
            CheatRunner runner)
        {
            _levelService = levelService;
            _paintService = paintService;
            _paintStore = paintStore;
            _progress = progress;
            _wallet = wallet;
            _hintCredits = hintCredits;
            _runner = runner;
        }

        // ────────────────────────────── ILevelCheatService ──────────────────────────────

        /// Đếm cả ô để trống trong Inspector: index của port phải khớp VỊ TRÍ trong danh
        /// sách, nên bỏ qua ô null sẽ làm mọi index lệch đi từ chỗ đó trở về sau.
        public int Count => _levelService?.Levels?.Count ?? 0;

        /// Vị trí của màn ĐANG NẠP, không phải mốc tiến trình.
        ///
        /// Hai con số đó tách nhau từ khi Home cho chơi lại màn cũ: CurrentLevel là mốc
        /// tiến trình, còn CurrentConfig mới là bảng đang nằm trên màn hình.
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

        /// Hỏi theo LƯỚI chứ không theo config: config đã gán mà thiếu Grid Data thì bảng
        /// vẫn trống và mọi lệnh cheat đều vô nghĩa.
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

        // ────────────────────────────── IFlowCheatService ───────────────────────────────

        public CheatGamePhase Phase
        {
            get
            {
                if (!IsReady) return CheatGamePhase.Loading;

                return _paintService.IsComplete ? CheatGamePhase.Win : CheatGamePhase.Playing;
            }
        }

        public bool CanForceWin => Phase == CheatGamePhase.Playing;

        /// Tô nốt MỌI ô còn lại, rải qua nhiều frame.
        ///
        /// Không gọi thẳng LevelManager.CompleteCurrentLevel: hàm đó chỉ nhích tiến trình.
        /// Nhích tiến trình không phải là thắng — bảng vẫn còn ô chưa tô, popup không mở,
        /// ăn mừng không chạy, và đúng những thứ người ta cần xem lại là những thứ bị bỏ
        /// qua. Đi đường dài qua TryPaint thì mọi mắt xích đều chạy thật.
        public void ForceWin() => PaintCells(int.MaxValue);

        public void ForceLose() => WarnOnce(
            "JewelPainter không có trạng thái thua — Force Lose không làm gì. " +
            "Tranh tô dở chỉ nằm chờ, không có lượt đi và không có đồng hồ để hết giờ.");

        public void TriggerNoMoves() => WarnOnce(
            "JewelPainter không có khái niệm hết nước đi — Trigger No Moves không làm gì. " +
            "Ô nào cũng tô được miễn chọn đúng màu, nên không có thế bí để mà cứu.");

        // ───────────────────────────── IProgressCheatService ────────────────────────────

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

        /// PlayerWallet chia hai đường cộng và trừ, còn port chỉ có một hàm nhận số âm.
        public void AddCoins(int amount)
        {
            if (_wallet == null || amount == 0) return;

            if (amount > 0) _wallet.Add(amount);
            else _wallet.TrySpend(-amount);
        }

        /// Xoá tiến độ tô của màn đang chơi rồi nạp lại — bảng trở về trắng tinh.
        public void ClearSave()
        {
            _paintStore?.ResetCurrent();

            ReloadCurrent();
        }

        // ──────────────────────── IJewelPainterCheatService (riêng) ─────────────────────

        /// Cộng dồn theo TỪNG MÀU thay vì quét cả lưới: một màn chỉ có vài màu, còn lưới
        /// 72x72 là 5184 ô — mà con số này bị hỏi lại mỗi frame để cập nhật chữ trên panel.
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

        // ─────────────────────────────────── nội bộ ────────────────────────────────────

        /// Vòng tô hàng loạt. Mỗi frame tô CellsPerFrame ô rồi nhường lại một frame.
        private IEnumerator FillRoutine(int count, bool singleColor)
        {
            // Chốt lại màn đang chơi. Đổi màn giữa chừng thì toạ độ ô của màn cũ không còn
            // nghĩa gì nữa, mà coroutine thì không tự biết chuyện đó — nó vẫn chạy tiếp và
            // tô bừa lên bảng mới.
            var level = _levelService.CurrentConfig;

            // Màu người chơi đang chọn phải được trả về đúng chỗ cũ. Cheat được phép đổi
            // trạng thái game, nhưng chỉ những trạng thái người bấm CÓ YÊU CẦU đổi.
            var restore = _paintService.SelectedPaletteIndex;

            // Chỉ tô một màu thì chốt màu NGAY TỪ ĐẦU. Hỏi lại mỗi ô thì tô xong màu này
            // nó nhảy sang màu kế và thành ra tô cả bảng.
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

            // Chỉ trả màu về khi vẫn còn ở đúng màn đó — màn khác thì chỉ số màu cũ trỏ
            // vào một màu hoàn toàn khác.
            if (restore >= 0 && _levelService.CurrentConfig == level) _paintService.SelectColor(restore);

            _fill = null;
        }

        /// Màu để tô khi chỉ tô MỘT màu: ưu tiên màu đang chọn nếu nó còn ô, không thì màu
        /// đầu tiên còn ô. -1 khi bảng đã tô kín.
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

        /// false khi không còn ô nào tô được — vòng lặp dừng ở đó.
        private bool TryPaintNextCell(int only)
        {
            var index = only >= 0 ? only : FirstColorWithRemaining();

            if (index < 0 || _paintService.RemainingFor(index) <= 0) return false;

            // TryPaint chỉ tô bằng MÀU ĐANG CHỌN nên phải chọn trước. SelectColor tự bỏ
            // qua khi trùng màu cũ, nên gọi ở mỗi ô cũng không tốn gì.
            _paintService.SelectColor(index);

            // Luôn lấy ô chưa tô ĐẦU TIÊN: ô vừa tô rơi khỏi danh sách, nên lần sau
            // ordinal 0 đã là ô kế tiếp. Tăng dần ordinal mới là cái sai — nó bỏ cách ô.
            if (!_paintService.TryGetUnpaintedCell(index, 0, out var cell)) return false;

            return _paintService.TryPaint(cell.x, cell.y);
        }

        private LevelConfig ConfigAt(int index)
        {
            var levels = _levelService?.Levels;

            if (levels == null || index < 0 || index >= levels.Count) return null;

            return levels[index];
        }

        /// Không giả định LevelId chạy liên tục hay đúng thứ tự trong danh sách — mở khoá
        /// hết nghĩa là mở tới id LỚN NHẤT, và chỉ có quét mới biết id đó là bao nhiêu.
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

        /// Dừng cú tô đang chạy TRƯỚC khi đổi bảng — không thì frame sau nó tô tiếp lên
        /// bảng mới bằng toạ độ của bảng cũ.
        private void LoadConfig(LevelConfig config)
        {
            if (config == null) return;

            StopFilling();

            _levelService.LoadLevel(config.LevelId);
        }

        /// Bấm một nút không làm gì mà cũng không nói gì là kiểu cheat tệ nhất: người test
        /// tưởng chức năng hỏng và đi báo lỗi. Nói một lần rồi thôi.
        private void WarnOnce(string message)
        {
            if (!_warnings.Add(message)) return;

            Debug.LogWarning($"[Cheat] {message}");
        }
    }
}
#endif
