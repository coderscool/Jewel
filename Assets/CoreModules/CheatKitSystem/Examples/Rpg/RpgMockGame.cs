using CoreModules.CheatKit.Ports;
using UnityEngine;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// Mock-game RPG: chapter (≈ "level"), party level/XP, gold (≈ "coin"), God Mode, boss/wipe.
    /// THUẦN C# — đại diện cho hệ thống RPG thật mà adapter sẽ map vào port chuẩn + port RPG.
    /// </summary>
    public sealed class RpgMockGame
    {
        public const int ChapterCount = 10;

        public int Chapter { get; private set; }
        public int PartyLevel { get; private set; } = 1;
        public int Xp { get; private set; }
        public int Gold { get; private set; } = 100;
        public bool GodMode { get; set; }
        public CheatGamePhase Phase { get; private set; } = CheatGamePhase.Playing;

        public void AddXp(int amount)
        {
            Xp += Mathf.Max(0, amount);
            while (Xp >= 100) { Xp -= 100; PartyLevel++; }
            CheatLog.Info($"[RPG] Lv {PartyLevel} · XP {Xp}");
        }

        public void LevelUp()
        {
            PartyLevel++;
            Xp = 0;
            CheatLog.Info($"[RPG] Level Up → Lv {PartyLevel}");
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            CheatLog.Info($"[RPG] Gold → {Gold}");
        }

        public void LoadChapter(int index)
        {
            Chapter = Mathf.Clamp(index, 0, ChapterCount - 1);
            Phase = CheatGamePhase.Playing;
            CheatLog.Info($"[RPG] Load Chapter {Chapter + 1} → Playing");
        }

        public void ClearBoss()
        {
            Phase = CheatGamePhase.Win;
            CheatLog.Info("[RPG] Boss cleared → WIN");
        }

        public void PartyWipe()
        {
            if (GodMode) { CheatLog.Info("[RPG] God Mode — party wipe bỏ qua"); return; }
            Phase = CheatGamePhase.Lose;
            CheatLog.Info("[RPG] Party wiped → LOSE");
        }
    }
}
