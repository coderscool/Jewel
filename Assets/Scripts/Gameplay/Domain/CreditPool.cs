using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Một kho lượt dùng miễn phí: giữ một con số, đọc lúc dựng, ghi mỗi lần đổi.
    public abstract class CreditPool
    {
        private readonly ISaveService _save;
        private readonly string _countKey;
        private int _remaining;

        /// Khởi tạo kho lượt từ bản lưu.
        protected CreditPool(ISaveService save, int startingCredits, string countKey, string grantedKey)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _countKey = countKey ?? throw new ArgumentNullException(nameof(countKey));

            if (grantedKey == null) throw new ArgumentNullException(nameof(grantedKey));

            if (!_save.GetBool(grantedKey))
            {
                _remaining = startingCredits < 0 ? 0 : startingCredits;

                _save.SetBool(grantedKey, true);
                _save.SetInt(_countKey, _remaining);
                _save.Save();

                return;
            }

            _remaining = _save.GetInt(_countKey);
        }

        public int Remaining => _remaining;

        public event Action<int> OnCreditsChanged;

        /// Trừ một lượt; false khi đã hết.
        public bool TrySpend()
        {
            if (_remaining <= 0) return false;

            _remaining--;
            Persist();

            return true;
        }

        /// Cộng thêm lượt.
        public void Grant(int amount)
        {
            if (amount <= 0) return;

            _remaining += amount;
            Persist();
        }

        private void Persist()
        {
            _save.SetInt(_countKey, _remaining);
            _save.Save();

            OnCreditsChanged?.Invoke(_remaining);
        }
    }
}
