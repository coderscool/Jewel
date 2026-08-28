using CoreModules.CheatKit.Ports;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// ADAPTER ví dụ — RPG. Map port CHUẨN (level→chapter, flow→boss/wipe, progress→gold) VÀ
    /// port ĐẶC THÙ <see cref="IRpgCheatService"/> (god/XP/level/gold) vào <see cref="RpgMockGame"/>.
    /// Bootstrap đưa <c>this</c> vào CheatServices.extras để module RPG lấy ra qua Get&lt;IRpgCheatService&gt;().
    /// </summary>
    public sealed class RpgCheatBridge :
        ILevelCheatService, IFlowCheatService, IProgressCheatService, IRpgCheatService
    {
        private readonly RpgMockGame _game;
        public RpgCheatBridge(RpgMockGame game) => _game = game;

        // ── ILevelCheatService (chapter) ──
        public int Count => RpgMockGame.ChapterCount;
        public int CurrentIndex => _game.Chapter;
        public bool IsReady => true;
        public string NameOf(int index) => $"Chapter {index + 1}";
        public void Load(int index) => _game.LoadChapter(index);
        public void ReloadCurrent() => _game.LoadChapter(_game.Chapter);

        // ── IFlowCheatService (boss / party wipe) ──
        public CheatGamePhase Phase => _game.Phase;
        public bool CanForceWin => _game.Phase == CheatGamePhase.Playing;
        public void ForceWin() => _game.ClearBoss();
        public void ForceLose() => _game.PartyWipe();
        public void TriggerNoMoves() { /* RPG không có no-moves */ }

        // ── IProgressCheatService (gold = coin) ──
        public int Coins => _game.Gold;
        public void UnlockAll() => _game.LoadChapter(RpgMockGame.ChapterCount - 1);
        public void SetProgress(int levelIndex) => _game.LoadChapter(levelIndex);
        public void AddCoins(int amount) => _game.AddGold(amount);
        public void ClearSave() => _game.LoadChapter(0);

        // ── IRpgCheatService (custom) ──
        public bool GodMode { get => _game.GodMode; set => _game.GodMode = value; }
        public int PartyLevel => _game.PartyLevel;
        public int Xp => _game.Xp;
        public int Gold => _game.Gold;
        public void AddXp(int amount) => _game.AddXp(amount);
        public void LevelUp() => _game.LevelUp();
        public void AddGold(int amount) => _game.AddGold(amount);
    }
}
