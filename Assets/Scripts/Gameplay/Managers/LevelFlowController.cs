using System;
using System.Collections;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// Nối các mảnh đã có lại thành luồng thắng màn: phát hiện tô xong, chạy màn ăn
    /// mừng, chờ một nhịp, rồi báo ra ngoài rằng đã thắng.
    ///
    /// Ghi nhận đã qua màn NGAY lúc phát hiện tô xong, không đợi người chơi bấm nút —
    /// tô xong là đã xong. Nút trong popup chỉ còn việc điều hướng về Home, nên thoát
    /// game trong lúc popup đang mở vẫn giữ được màn vừa thắng.
    ///
    /// KHÔNG tự NẠP màn kế. Gameplay vì thế không cần biết popup hay Home tồn tại.
    ///
    /// Tách khỏi LevelManager vì điều phối LUỒNG màn chơi là việc khác với giữ DỮ LIỆU
    /// màn chơi. LevelManager không cần biết điều gì khiến một màn kết thúc.
    public class LevelFlowController : MonoBehaviour, ILevelFlowService
    {
        [Tooltip("Bao lâu kể từ lúc viên ngọc cuối đáp xuống thì hiện popup thắng màn, " +
                 "tính bằng giây. Đây là con số TUYỆT ĐỐI, không cộng dồn với thời lượng " +
                 "của WinCelebration.\n\n" +
                 "Đặt ngắn hơn màn ăn mừng thì popup hiện đè lên lúc dải lấp lánh còn " +
                 "đang quét — đôi khi đó lại là thứ bạn muốn.")]
        [SerializeField] private float _popupDelaySeconds = 2f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;
        private WinCelebration _winCelebration;

        /// Đã báo thắng cho màn này rồi. Giữ lại vì OnJewelLanded còn nổ thêm vài lần
        /// nữa sau ô cuối, khi những viên đang bay lần lượt đáp xuống.
        private bool _hasAnnounced;

        /// Màn đang thật sự được nạp. Từ khi Home cho chọn màn, con số này KHÔNG còn luôn
        /// bằng CurrentLevel — người chơi chọn chơi lại một màn cũ thì hai bên tách nhau.
        private int _loadedLevel = -1;

        public event Action OnLevelCleared;

        public int ClearedLevel { get; private set; } = -1;

        /// Hỏi theo MÀN VỪA XONG, không theo màn hiện tại: lúc popup mở thì tiến trình
        /// đã nhích sang màn kế rồi, nên đọc CurrentLevel + 1 là hỏi về màn sau nữa.
        public bool IsLastLevel
        {
            get
            {
                if (_levelService == null) return true;

                var reference = ClearedLevel >= 0 ? ClearedLevel : _levelService.CurrentLevel;

                return !_levelService.HasLevel(reference + 1);
            }
        }

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            JewelFlyEffect flyEffect,
            WinCelebration winCelebration)
        {
            _levelService = levelService;
            _paintService = paintService;
            _flyEffect = flyEffect;
            _winCelebration = winCelebration;

            // Nghe lúc viên ngọc ĐÁP XUỐNG, không phải lúc bấm tô. Xét sớm thì màn ăn
            // mừng bắt đầu trong khi viên cuối vẫn đang bay giữa trời.
            _flyEffect.OnJewelLanded += HandleJewelLanded;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;
        }

        /// Ghi nhận đã qua màn. Chạy NGAY lúc phát hiện tô xong, không đợi người chơi bấm.
        ///
        /// Nhích cả khi vừa xong MÀN CUỐI. Bản trước dừng lại ở đó vì sợ lần mở game sau
        /// nạp một màn không tồn tại — nỗi lo có thật, nhưng chặn ở đây là chữa sai chỗ và
        /// làm hỏng ba thứ khác cùng lúc: màn cuối không bao giờ vào được bộ sưu tập
        /// (IsCompleted đọc "id < CurrentLevel"), bản lưu ô đã tô của nó không bao giờ được
        /// dọn, và vào lại nó thì popup thắng bật lên lần nữa.
        ///
        /// Chỗ đúng để chữa là lúc ĐỌC: ILevelService.CurrentLevel tự kẹp về màn cuối cùng
        /// còn tồn tại, còn IsCompleted đọc con số thô bên dưới. Nhờ vậy tiến trình vẫn nói
        /// thật là "đã xong hết" mà không ai ở ngoài nhìn thấy một màn không tồn tại, và
        /// thêm màn mới vào bản sau là người chơi tiếp tục đúng chỗ.
        ///
        /// Tô xong là đã xong — bấm nút chỉ là chuyện đi tiếp. Để việc ghi nhận nằm sau
        /// cú bấm thì người chơi thắng màn rồi thoát game trong lúc popup đang mở sẽ mất
        /// trắng màn vừa qua, và lần mở sau lại phải tô lại từ đầu.
        private void AdvanceProgress()
        {
            if (_levelService == null) return;

            if (!_levelService.HasLevel(_levelService.CurrentLevel + 1))
            {
                Debug.Log($"Đã hoàn thành màn cuối ({_levelService.CurrentLevel}). " +
                          "Tiến trình vẫn nhích bên trong để màn này vào được bộ sưu tập; " +
                          "CurrentLevel tự kẹp lại nên bên ngoài không thấy con số vượt ngưỡng.");
            }

            _levelService.CompleteCurrentLevel();
        }

        /// Bắt lại trường hợp màn mở ra ĐÃ tô kín sẵn.
        ///
        /// Xảy ra khi người chơi tô xong ô cuối rồi thoát game trước khi bấm nút trong
        /// popup: tiến trình chưa nhích, nhưng bản lưu những ô đã tô thì đã ghi đủ. Lần
        /// mở sau, màn cũ hiện ra hoàn thiện mà KHÔNG còn ô nào để tô — OnJewelLanded
        /// không bao giờ nổ nữa, nên popup không bao giờ hiện và không còn đường nào sang
        /// màn kế. Người chơi kẹt vĩnh viễn ở một bức tranh đã xong.
        ///
        /// Ở đây chỉ mở lại popup, KHÔNG chạy WinCelebration: dải quét và cú thu camera
        /// là phần thưởng cho khoảnh khắc vừa đặt viên ngọc cuối, chiếu lại ở một phiên
        /// khác thì nó chỉ là quãng chờ.
        ///
        /// Vẫn đi qua nút bấm chứ không tự sang màn: luật "chỉ nhích tiến trình khi người
        /// chơi bấm" giữ nguyên.
        private void HandleLevelStarted(int levelId)
        {
            StopAllCoroutines();

            _hasAnnounced = false;
            ClearedLevel = -1;
            _loadedLevel = levelId;

            // PaintManager Init TRƯỚC lớp này nên nó đã khôi phục xong trạng thái tô lúc
            // handler này chạy. Thứ tự đó được cắm ở GameEntryPoint.
            if (_paintService == null || !_paintService.IsComplete) return;

            // Màn ĐÃ ĐƯỢC GHI NHẬN rồi thì bảng tô kín chỉ có nghĩa là "đã từng chơi xong",
            // không phải "vừa thắng mà chưa kịp nhận". Chơi lại một màn cũ không được mở
            // popup thắng.
            //
            // Đây mới là câu hỏi đúng, và lưới an toàn bên dưới lúc viết ra đã hỏi nhầm.
            // Hồi đó tiến trình chỉ nhích khi người chơi BẤM nút trong popup, nên "bảng
            // kín mà tiến trình chưa nhích" đúng là cảnh kẹt. Từ khi BeginClear nhích ngay
            // lúc phát hiện tô xong, cảnh đó chỉ còn xảy ra ở lượt CHƠI LẠI — tức lưới an
            // toàn chỉ còn bắt đúng những lượt nó không nên bắt.
            //
            // Bảng tô kín ở đây là CỐ Ý: PaintManager gọi PaintAll cho mọi màn đã ghi nhận,
            // để người chơi mở lại xem bức tranh mình đã tô. Không phải dữ liệu hỏng, và
            // không có gì để dọn.
            if (_levelService.IsCompleted(levelId)) return;

            // KHÔNG chạy WinCelebration: dải quét và cú thu camera là phần thưởng cho
            // khoảnh khắc vừa đặt viên ngọc cuối. Chiếu lại ở một phiên khác thì nó chỉ
            // còn là quãng chờ.
            BeginClear(playCelebration: false);
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_hasAnnounced) return;
            if (!_paintService.IsComplete) return;

            BeginClear(playCelebration: true);
        }

        private void BeginClear(bool playCelebration)
        {
            _hasAnnounced = true;

            // Chụp lại TRƯỚC khi nhích: sau AdvanceProgress thì CurrentLevel đã là màn kế,
            // mà popup và màn ăn mừng ở Home đều cần biết màn nào vừa xong.
            ClearedLevel = _loadedLevel >= 0 ? _loadedLevel : _levelService.CurrentLevel;

            // Màn này đã từng xong TRƯỚC lượt chơi vừa rồi chưa.
            //
            // Phải chụp ở ĐÂY, trước AdvanceProgress. Nhích xong thì IsCompleted(ClearedLevel)
            // thành true cho cả lượt thắng LẦN ĐẦU — hỏi muộn một dòng là không màn nào
            // còn được mở popup nữa.
            var isReplay = _levelService.IsCompleted(ClearedLevel);

            // CHỈ nhích khi màn này CHƯA từng được ghi nhận.
            //
            // Home cho chọn chơi lại màn cũ, mà tiến trình chỉ đi tới. Không có phép hỏi
            // này thì tô xong màn 2 lúc đang ở màn 5 sẽ đẩy tiến trình lên màn 6 — người
            // chơi được thưởng hai màn cho một lần chơi, và mất luôn màn 5 chưa đụng tới.
            //
            // Hỏi isReplay chứ KHÔNG so ClearedLevel với CurrentLevel. Hai câu đó trùng
            // nhau ở mọi màn trừ màn cuối: xong hết rồi thì CurrentLevel bị kẹp lại đúng
            // bằng màn cuối, nên phép so cũ thấy chúng bằng nhau và nhích tiến trình THÊM
            // một lần nữa mỗi lượt chơi lại màn đó.
            //
            // Nhánh else báo "đã tô xong" mà không nhích tiến trình. Đây là đường của lượt
            // CHƠI LẠI: nút Tô lại mở một lượt mới trên màn đã xong, và khi tô hết lần nữa
            // thì bản lưu rỗng kia phải được dọn để màn trở về chế độ xem tranh. Thiếu nó
            // thì bản lưu của lượt chơi lại nằm lại vĩnh viễn.
            if (!isReplay) AdvanceProgress();
            else _levelService.MarkLevelFinished(ClearedLevel);

            // Ăn mừng chạy NGAY, còn popup đếm giờ song song. Hai thứ độc lập nhau về
            // thời gian: đổi thời lượng dải quét không kéo theo lúc popup hiện, và
            // ngược lại.
            if (playCelebration && _winCelebration != null) _winCelebration.Play();

            // Chơi lại thì DỪNG Ở ĐÂY: dải lấp lánh vẫn chạy vì nó là phần thưởng cho cú
            // đặt viên ngọc cuối, nhưng không bắn OnLevelCleared.
            //
            // Sự kiện đó không chỉ mở popup — HudView và thanh chọn màu cũng nghe nó để tự
            // ẩn, dọn chỗ cho popup đứng một mình. Không có popup mà vẫn bắn thì người chơi
            // ngồi trước một bức tranh xong xuôi với HUD biến mất và không còn đường nào
            // bấm tiếp.
            //
            // Phần thưởng cũng đã trao từ lần thắng đầu rồi: tiến trình không nhích, tiền
            // không cộng thêm. Mở lại popup ăn mừng cho một lượt không thưởng gì là hứa
            // nhầm với người chơi.
            if (isReplay) return;

            StartCoroutine(AnnounceCleared());
        }

        private IEnumerator AnnounceCleared()
        {
            // Đếm thẳng một con số thay vì đợi WinCelebration báo xong. Đổi lại là bạn
            // phải tự canh nó với thời lượng màn ăn mừng, nhưng bù lại thời điểm popup
            // hiện ra nằm gọn trong một ô Inspector chứ không phải suy từ ba ô khác.
            if (_popupDelaySeconds > 0f) yield return new WaitForSeconds(_popupDelaySeconds);

            OnLevelCleared?.Invoke();
        }
    }
}
