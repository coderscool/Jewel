using CoreModules.CheatKit.Ports;
using UnityEngine;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// Mock-game ACTION: checkpoint (≈ "level"), ammo, số enemy, slow-mo, infinite ammo, clear-wave/die.
    /// KHÔNG có tiền tệ → adapter trả Coins = -1 / progress = null (ISP: bỏ port không dùng).
    /// </summary>
    public sealed class ActionMockGame
    {
        public const int CheckpointCount = 8;
        private const int MaxAmmo = 30;

        public int Checkpoint { get; private set; }
        public int Ammo { get; private set; } = MaxAmmo;
        public int Enemies { get; private set; } = 5;
        public bool InfiniteAmmo { get; set; }
        public bool SlowMotion { get; set; }
        public CheatGamePhase Phase { get; private set; } = CheatGamePhase.Playing;

        public void RefillAmmo()
        {
            Ammo = MaxAmmo;
            CheatLog.Info($"[Action] Ammo refilled → {Ammo}");
        }

        public void SpawnEnemy()
        {
            Enemies++;
            CheatLog.Info($"[Action] Spawn → {Enemies} enemies");
        }

        public void KillAll()
        {
            Enemies = 0;
            CheatLog.Info("[Action] Kill all enemies");
        }

        public void LoadCheckpoint(int index)
        {
            Checkpoint = Mathf.Clamp(index, 0, CheckpointCount - 1);
            Phase = CheatGamePhase.Playing;
            CheatLog.Info($"[Action] Load Checkpoint {Checkpoint + 1} → Playing");
        }

        public void ClearWave()
        {
            Phase = CheatGamePhase.Win;
            CheatLog.Info("[Action] Wave cleared → WIN");
        }

        public void Die()
        {
            Phase = CheatGamePhase.Lose;
            CheatLog.Info("[Action] Player died → LOSE");
        }
    }
}
