using CoreModules.CheatKit.Ports;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// ADAPTER ví dụ — ACTION. Map port level→checkpoint, flow→wave/death, và port đặc thù
    /// <see cref="IActionCheatService"/>. KHÔNG implement <see cref="IProgressCheatService"/> —
    /// game này không có tiền tệ/progress lưu, nên bootstrap truyền <c>progress: null</c> (ISP:
    /// chỉ cung cấp port thực sự dùng; panel tự ẩn phần progress/coin).
    /// </summary>
    public sealed class ActionCheatBridge :
        ILevelCheatService, IFlowCheatService, IActionCheatService
    {
        private readonly ActionMockGame _game;
        public ActionCheatBridge(ActionMockGame game) => _game = game;

        // ── ILevelCheatService (checkpoint) ──
        public int Count => ActionMockGame.CheckpointCount;
        public int CurrentIndex => _game.Checkpoint;
        public bool IsReady => true;
        public string NameOf(int index) => $"Checkpoint {index + 1}";
        public void Load(int index) => _game.LoadCheckpoint(index);
        public void ReloadCurrent() => _game.LoadCheckpoint(_game.Checkpoint);

        // ── IFlowCheatService (wave / death) ──
        public CheatGamePhase Phase => _game.Phase;
        public bool CanForceWin => _game.Phase == CheatGamePhase.Playing;
        public void ForceWin() => _game.ClearWave();
        public void ForceLose() => _game.Die();
        public void TriggerNoMoves() { /* Action không có no-moves */ }

        // ── IActionCheatService (custom) ──
        public bool InfiniteAmmo { get => _game.InfiniteAmmo; set => _game.InfiniteAmmo = value; }
        public bool SlowMotion { get => _game.SlowMotion; set => _game.SlowMotion = value; }
        public int Ammo => _game.Ammo;
        public int Enemies => _game.Enemies;
        public void RefillAmmo() => _game.RefillAmmo();
        public void SpawnEnemy() => _game.SpawnEnemy();
        public void KillAll() => _game.KillAll();
    }
}
