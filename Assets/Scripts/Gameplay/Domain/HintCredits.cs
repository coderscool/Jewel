using System;
using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Số lượt gợi ý miễn phí còn lại. Thuần C# — KHÔNG có using UnityEngine, nên test
    /// được ở EditMode mà không cần vào Play Mode.
    ///
    /// Cùng khuôn với PlayerProgress và PlayerWallet: giữ một con số, đọc lúc dựng, ghi
    /// mỗi lần đổi.
    ///
    /// Đếm cho CẢ GAME chứ không theo màn: người chơi mới được ba lượt để hiểu nút gợi ý
    /// làm gì, hết là hết. Đó cũng là lý do không có hàm nào tự nạp lại — muốn thêm lượt
    /// thì phải đi qua Grant, và Grant chỉ được gọi sau khi người chơi xem quảng cáo hoặc
    /// trả tiền.
    public class HintCredits
    {
        private readonly ISaveService _save;
        private int _remaining;

        /// Cờ đánh dấu đã phát lượt khởi đầu.
        ///
        /// Cần một cờ RIÊNG, không suy từ "số lượt đang là 0": người chơi dùng hết ba lượt
        /// rồi thoát game thì lần mở sau con số cũng là 0, và nếu suy từ nó thì họ được
        /// phát lại ba lượt nữa — mỗi lần khởi động lại thêm ba lượt.
        public HintCredits(ISaveService save, int startingCredits)
        {
            _save = save;

            if (!_save.GetBool(PreferenceKeys.HintCreditsGranted))
            {
                _remaining = startingCredits < 0 ? 0 : startingCredits;

                _save.SetBool(PreferenceKeys.HintCreditsGranted, true);
                _save.SetInt(PreferenceKeys.HintCredits, _remaining);
                _save.Save();

                return;
            }

            _remaining = _save.GetInt(PreferenceKeys.HintCredits);
        }

        public int Remaining => _remaining;

        public bool HasCredit => _remaining > 0;

        /// Bắn khi số lượt đổi, để chỗ hiển thị không phải hỏi lại mỗi frame.
        public event Action<int> OnCreditsChanged;

        /// false khi đã hết lượt — bên gọi dùng nó để mở popup thay vì chạy gợi ý.
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
            _save.SetInt(PreferenceKeys.HintCredits, _remaining);
            _save.Save();

            OnCreditsChanged?.Invoke(_remaining);
        }
    }
}
