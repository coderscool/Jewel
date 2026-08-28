using CoreModules.CheatKit.Ports;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// ADAPTER ví dụ — CASUAL (endless / score-based): không có "tổng số level" cố định.
    /// <see cref="Count"/> trả 0 → panel TỰ ẩn thanh "x / N" (graceful degradation). "Coin" ở đây
    /// là gem; ForceWin = đạt mốc điểm; TriggerNoMoves = reshuffle. Cùng port, ngữ nghĩa khác nhau —
    /// đó là lý do cookbook map port theo từng genre thay vì ép một khuôn.
    /// </summary>
    public sealed class CasualCheatBridge : ILevelCheatService, IFlowCheatService, IProgressCheatService
    {
        private readonly MockGameModel _game;
        public CasualCheatBridge(MockGameModel game) => _game = game;

        // ── ILevelCheatService ── (endless → Count 0, đặt "stage" để nhảy nhanh khi test) ──
        public int Count => _game.Count; // 0 = vô hạn
        public int CurrentIndex => _game.CurrentIndex;
        public bool IsReady => true;
        public string NameOf(int index) => $"Stage {index + 1}";
        public void Load(int index) => _game.Load(index);
        public void ReloadCurrent() => _game.Reload();

        // ── IFlowCheatService ──
        public CheatGamePhase Phase => _game.Phase;
        public bool CanForceWin => _game.Phase == CheatGamePhase.Playing;
        public void ForceWin() => _game.Win();   // = đạt target score
        public void ForceLose() => _game.Lose();
        public void TriggerNoMoves() => _game.NoMoves(); // = reshuffle board

        // ── IProgressCheatService ── (Coins = gems) ──
        public int Coins => _game.Coins;
        public void UnlockAll() => _game.SetUnlocked(_game.CurrentIndex);
        public void SetProgress(int levelIndex) => _game.SetUnlocked(levelIndex);
        public void AddCoins(int amount) => _game.AddCoins(amount);
        public void ClearSave() => _game.ClearSave();
    }
}
