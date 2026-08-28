namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// CUSTOM PORT (OCP) — cheat ĐẶC THÙ RPG, KHÔNG nằm trong kit core. Genre tự sở hữu port của
    /// mình; kit không cần biết tới nó. Module RPG nhận port này qua <c>CheatServices.Get&lt;IRpgCheatService&gt;()</c>
    /// → thêm cheat genre = thêm port + module, không sửa CheatKit (Open/Closed).
    /// </summary>
    public interface IRpgCheatService
    {
        bool GodMode { get; set; }
        int PartyLevel { get; }
        int Xp { get; }
        int Gold { get; }

        void AddXp(int amount);
        void LevelUp();
        void AddGold(int amount);
    }
}
