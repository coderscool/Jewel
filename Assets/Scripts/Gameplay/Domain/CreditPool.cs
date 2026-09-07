using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Một kho lượt dùng miễn phí: giữ một con số, đọc lúc dựng, ghi mỗi lần đổi.
    ///
    /// Thuần C# — KHÔNG có using UnityEngine, nên test được ở EditMode mà không cần vào
    /// Play Mode. Cùng khuôn với PlayerProgress và PlayerWallet.
    ///
    /// Tách thành lớp CHA vì booster thứ hai cần đúng một cái kho như thế, khác mỗi KEY
    /// LƯU. Chép tay lần hai thì hai bản sẽ lệch nhau ở lần sửa đầu tiên — mà chỗ dễ lệch
    /// nhất lại đúng là chỗ tinh tế nhất: cái cờ "đã phát lượt khởi đầu" ngay dưới đây.
    ///
    /// Đếm cho CẢ GAME chứ không theo màn: người chơi mới được vài lượt để hiểu cái nút
    /// làm gì, hết là hết. Đó cũng là lý do không có hàm nào tự nạp lại — muốn thêm lượt
    /// thì phải đi qua Grant, và Grant chỉ được gọi sau khi người chơi xem quảng cáo hoặc
    /// trả tiền.
    public abstract class CreditPool
    {
        private readonly ISaveService _save;
        private readonly string _countKey;
        private int _remaining;

        /// Cờ đánh dấu đã phát lượt khởi đầu.
        ///
        /// Cần một cờ RIÊNG, không suy từ "số lượt đang là 0": người chơi dùng hết lượt
        /// rồi thoát game thì lần mở sau con số cũng là 0, và nếu suy từ nó thì họ được
        /// phát lại một mẻ nữa — mỗi lần khởi động lại thêm một mẻ.
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

        public bool HasCredit => _remaining > 0;

        /// Bắn khi số lượt đổi, để chỗ hiển thị không phải hỏi lại mỗi frame.
        public event Action<int> OnCreditsChanged;

        /// false khi đã hết lượt — bên gọi dùng nó để mở popup thay vì chạy booster.
        public bool TrySpend()
        {
            if (_remaining <= 0) return false;

            _remaining--;
            Persist();

            return true;
        }

        /// Cộng thêm lượt. Chỗ gọi là sau khi xem quảng cáo hoặc trả tiền.
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
