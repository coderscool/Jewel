using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số tiền của người chơi.
    public class PlayerWallet
    {
        private readonly ISaveService _save;
        private int _coins;

        public PlayerWallet(ISaveService save)
        {
            _save = save;
            _coins = _save.GetInt(PreferenceKeys.Coins);
        }

        public int Coins => _coins;

        public event Action<int> OnCoinsChanged;

        /// Cộng tiền vào ví.
        public void Add(int amount)
        {
            if (amount <= 0) return;

            _coins += amount;

            _save.SetInt(PreferenceKeys.Coins, _coins);
            _save.Save();

            OnCoinsChanged?.Invoke(_coins);
        }

        /// false khi không đủ tiền.
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || _coins < amount) return false;

            _coins -= amount;

            _save.SetInt(PreferenceKeys.Coins, _coins);
            _save.Save();

            OnCoinsChanged?.Invoke(_coins);
            return true;
        }
    }
}
