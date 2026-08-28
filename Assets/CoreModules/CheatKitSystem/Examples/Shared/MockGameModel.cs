using CoreModules.CheatKit.Ports;
using UnityEngine;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// Mock-game tối giản dùng chung cho ví dụ Puzzle &amp; Casual: level/stage, coin, phase chơi.
    /// THUẦN C# (không MonoBehaviour, không asset, không alloc mỗi frame) → đại diện cho "API thật"
    /// mà adapter (bridge) sẽ map vào port. Đổi <paramref name="endless"/> để mô phỏng game vô tận
    /// (Count=0 → panel ẩn thanh "x / N" — minh hoạ graceful degradation của <see cref="ILevelCheatService"/>).
    /// </summary>
    public sealed class MockGameModel
    {
        private readonly int _levelCount;
        private readonly bool _endless;

        public MockGameModel(int levelCount, int coins, bool endless = false)
        {
            _levelCount = Mathf.Max(0, levelCount);
            _endless = endless;
            Coins = Mathf.Max(0, coins);
            CurrentIndex = 0;
            UnlockedMax = 0;
            Phase = CheatGamePhase.Playing;
        }

        public int Count => _endless ? 0 : _levelCount;
        public int CurrentIndex { get; private set; }
        public int UnlockedMax { get; private set; }
        public int Coins { get; private set; }
        public CheatGamePhase Phase { get; private set; }

        public void Load(int index)
        {
            CurrentIndex = Clamp(index);
            Phase = CheatGamePhase.Playing;
            CheatLog.Info($"[MockGame] Load #{CurrentIndex} → Playing");
        }

        public void Reload()
        {
            Phase = CheatGamePhase.Playing;
            CheatLog.Info($"[MockGame] Reload #{CurrentIndex}");
        }

        public void Win()
        {
            Phase = CheatGamePhase.Win;
            CheatLog.Info($"[MockGame] WIN #{CurrentIndex}");
        }

        public void Lose()
        {
            Phase = CheatGamePhase.Lose;
            CheatLog.Info($"[MockGame] LOSE #{CurrentIndex}");
        }

        public void NoMoves() => CheatLog.Info("[MockGame] No-moves rescue triggered");

        public void AddCoins(int amount)
        {
            Coins = Mathf.Max(0, Coins + amount);
            CheatLog.Info($"[MockGame] Coins → {Coins}");
        }

        public void SetUnlocked(int index)
        {
            UnlockedMax = Clamp(index);
            CheatLog.Info($"[MockGame] Unlocked up to #{UnlockedMax}");
        }

        public void ClearSave() => CheatLog.Info($"[MockGame] Save cleared (#{CurrentIndex})");

        private int Clamp(int index) =>
            _endless ? Mathf.Max(0, index) : Mathf.Clamp(index, 0, Mathf.Max(0, _levelCount - 1));
    }
}
