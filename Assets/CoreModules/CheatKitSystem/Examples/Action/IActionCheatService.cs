namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// CUSTOM PORT (OCP) — cheat ĐẶC THÙ ACTION (ammo / enemy / slow-mo). Như RPG, port này thuộc
    /// genre chứ không thuộc kit. Module Action lấy qua <c>CheatServices.Get&lt;IActionCheatService&gt;()</c>.
    /// </summary>
    public interface IActionCheatService
    {
        bool InfiniteAmmo { get; set; }
        bool SlowMotion { get; set; }
        int Ammo { get; }
        int Enemies { get; }

        void RefillAmmo();
        void SpawnEnemy();
        void KillAll();
    }
}
