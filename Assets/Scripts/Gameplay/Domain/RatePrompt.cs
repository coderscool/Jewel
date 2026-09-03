using JewelPainter.Core.Persistence;

namespace JewelPainter.Gameplay.Domain
{
    /// Đếm xem đã đến lúc mời người chơi đánh giá chưa.
    ///
    /// Thuần C# — KHÔNG có using UnityEngine, nên test được ở EditMode mà không cần vào
    /// Play Mode. Cùng khuôn với PlayerWallet và HintCredits: giữ một con số, đọc lúc
    /// dựng, ghi mỗi lần đổi.
    ///
    /// Hai trạng thái, và chúng khác nhau:
    ///   _clearedSincePrompt — đã xong bao nhiêu màn kể từ lần mời gần nhất
    ///   _hasRated           — đã bấm đánh giá, và từ đó không bao giờ mời nữa
    ///
    /// Đếm LẠI TỪ ĐẦU sau mỗi lần mời chứ không đếm tổng. Đếm tổng thì từ màn thứ tư trở
    /// đi màn nào cũng thoả "tổng >= 4", và popup hiện sau mỗi màn.
    public class RatePrompt
    {
        private readonly ISaveService _save;
        private readonly int _levelsPerPrompt;

        private int _clearedSincePrompt;
        private bool _hasRated;

        public RatePrompt(ISaveService save, int levelsPerPrompt)
        {
            _save = save;

            // Sàn 1: số 0 hoặc âm nghĩa là mời sau mỗi 0 màn, tức mời mãi mãi.
            _levelsPerPrompt = levelsPerPrompt < 1 ? 1 : levelsPerPrompt;

            _hasRated = _save.GetBool(PreferenceKeys.HasRated);
            _clearedSincePrompt = _save.GetInt(PreferenceKeys.LevelsSinceRatePrompt);
        }

        /// Đã đánh giá rồi thì cả lớp này ngừng hoạt động.
        public bool HasRated => _hasRated;

        /// Gọi mỗi lần người chơi tô xong một màn. true nghĩa là ĐÃ ĐỦ số màn để mời.
        ///
        /// KHÔNG tự đếm lại từ đầu ở đây — việc đó là của MarkPrompted, gọi lúc popup
        /// thật sự hiện ra. Reset ngay tại đây thì người chơi tắt game trong khoảng giữa
        /// "đã đủ màn" và "popup kịp hiện" sẽ mất trắng lượt mời đó và phải xong thêm bốn
        /// màn nữa. Khoảng giữa ấy dài cỡ một cú bấm Continue, nhưng nó có thật.
        public bool RegisterLevelCleared()
        {
            if (_hasRated) return false;

            _clearedSincePrompt++;
            Persist();

            return _clearedSincePrompt >= _levelsPerPrompt;
        }

        /// Popup đã hiện ra rồi. Đếm lại từ đầu, nên bỏ qua lần này thì phải xong thêm
        /// đủ số màn nữa mới bị hỏi lại.
        public void MarkPrompted()
        {
            if (_hasRated || _clearedSincePrompt == 0) return;

            _clearedSincePrompt = 0;
            Persist();
        }

        /// Người chơi đã bấm đánh giá. Không mời lại nữa, cho tới khi dữ liệu game bị xoá.
        public void MarkRated()
        {
            if (_hasRated) return;

            _hasRated = true;
            _clearedSincePrompt = 0;

            _save.SetBool(PreferenceKeys.HasRated, true);
            _save.SetInt(PreferenceKeys.LevelsSinceRatePrompt, 0);
            _save.Save();
        }

        private void Persist()
        {
            _save.SetInt(PreferenceKeys.LevelsSinceRatePrompt, _clearedSincePrompt);
            _save.Save();
        }
    }
}
