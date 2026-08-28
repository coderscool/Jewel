using CoreModules.CheatKit.Ports;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// ADAPTER ví dụ — PUZZLE (kiểu WordJigsort): game level-based, thắng = hoàn thành mục tiêu.
    /// Map cả 3 port chuẩn vào <see cref="MockGameModel"/>. Một class implement 3 interface vì cùng
    /// nguồn state, nhưng panel chỉ thấy từng interface tách biệt (ISP). Bê sang game thật = đổi mock
    /// thành service thật, giữ nguyên hình dạng.
    /// </summary>
    public sealed class PuzzleCheatBridge : ILevelCheatService, IFlowCheatService, IProgressCheatService
    {
        private readonly MockGameModel _game;
        public PuzzleCheatBridge(MockGameModel game) => _game = game;

        // ── ILevelCheatService ──────────────────────────────────────────────
        public int Count => _game.Count;
        public int CurrentIndex => _game.CurrentIndex;
        public bool IsReady => true;
        public string NameOf(int index) => $"Level {index + 1}";
        public void Load(int index) => _game.Load(index);
        public void ReloadCurrent() => _game.Reload();

        // ── IFlowCheatService ───────────────────────────────────────────────
        public CheatGamePhase Phase => _game.Phase;
        public bool CanForceWin => _game.Phase == CheatGamePhase.Playing;
        public void ForceWin() => _game.Win();
        public void ForceLose() => _game.Lose();
        public void TriggerNoMoves() => _game.NoMoves();

        // ── IProgressCheatService ───────────────────────────────────────────
        public int Coins => _game.Coins;
        public void UnlockAll() => _game.SetUnlocked(_game.Count - 1);
        public void SetProgress(int levelIndex) => _game.SetUnlocked(levelIndex);
        public void AddCoins(int amount) => _game.AddCoins(amount);
        public void ClearSave() => _game.ClearSave();
    }
}
