using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số tiền của người chơi. Thuần C# — KHÔNG có using UnityEngine, nên test được ở
    /// EditMode mà không cần vào Play Mode.
    ///
    /// Cùng khuôn với PlayerProgress: giữ một con số, đọc lúc dựng, ghi mỗi lần đổi.
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

        /// Bắn khi số tiền đổi, để chỗ hiển thị không phải hỏi lại mỗi frame.
        public event Action<int> OnCoinsChanged;

        /// Số âm hoặc 0 thì không làm gì — cộng thưởng bằng 0 mà vẫn ghi đĩa và bắn sự
        /// kiện là tốn công vô ích.
        public void Add(int amount)
        {
            if (amount <= 0) return;

            _coins += amount;

            _save.SetInt(PreferenceKeys.Coins, _coins);
            _save.Save();

            OnCoinsChanged?.Invoke(_coins);
        }

        /// false khi không đủ tiền. Chưa ai gọi, nhưng có nó thì chỗ tiêu tiền sau này
        /// không phải tự trừ tay rồi quên ghi.
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
