using DG.Tweening;
using JewelPainter.Core.Services;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Base cho mọi popup. Mờ dần vào và mờ dần ra, KHÔNG Destroy —
    /// PopupManager giữ instance để tái dùng.
    ///
    /// Muốn thêm chuyển động riêng thì override Show/Hide ở class con và gọi base.
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Tiếng phát khi popup này ĐÓNG. Cancel là mặc định cho mọi popup; đổi " +
                 "sang None ở những popup mà cú đóng đã có tiếng riêng — popup thắng màn " +
                 "chẳng hạn, nút Continue của nó phát tiếng Direction.")]
        [SerializeField] private SoundKey _closeSound = SoundKey.Cancel;

        [Tooltip("Thời gian mờ dần VÀO, tính bằng giây. Để 0 là hiện tức khắc như bản cũ.")]
        [SerializeField] private float _fadeInDuration = 0.15f;

        [Tooltip("Thời gian mờ dần RA. Để 0 là tắt tức khắc.\n\n" +
                 "Trong suốt quãng này popup đã KHÔNG nhận chạm nữa — nó chỉ còn là hình " +
                 "ảnh đang tan đi. Nhờ vậy bấm hai lần vào nút đóng không chạy hai lần.")]
        [SerializeField] private float _fadeOutDuration = 0.15f;

        /// Lượt mờ đang chạy. Mở lại giữa lúc đang đóng thì huỷ lượt cũ.
        private Tween _fade;

        /// Trạng thái LOGIC, không phải trạng thái của GameObject.
        ///
        /// Tách ra vì trong lúc mờ ra, object vẫn còn bật nhưng popup coi như đã đóng:
        /// PopupManager.HideAll duyệt qua và gọi Hide lần nữa lên chính nó, mà lần gọi đó
        /// sẽ khởi động lại cú mờ từ alpha đang dở.
        private bool _isShown;

        public bool IsVisible => _isShown;

        /// Popup này có CHẶN chạm xuống phần phía sau nó không.
        ///
        /// Mặc định CÓ, và đó là mặc định an toàn: một popup đang mở mà người chơi vẫn
        /// quẹt trúng bức tranh phía sau là tô nhầm — một cú chạm không cố ý làm hỏng ô
        /// màu, và không có gì báo cho họ biết điều đó vừa xảy ra.
        ///
        /// Ô này KHÔNG phải ô "làm tối nền". Trước đây nó tên là _dimsBackground và bị
        /// hiểu là chuyện thẩm mỹ, nên khi cả game bỏ lớp tối thì mọi popup cùng bỏ tick
        /// — và tấm chặn chạm biến mất theo, vì nó chính là tấm nền đó. Hai việc phải
        /// tách ra: ô này quyết định CHẶN, còn tối hay không là alpha của chính tấm chặn
        /// bên PopupManager (game này để 0 — nền sau popup là bức tranh đang tô, thứ đẹp
        /// nhất trên màn hình, phủ tối lên nó là đánh đổi sai chiều).
        ///
        /// Tên mới cố ý KHÔNG dùng [FormerlySerializedAs]: giá trị cũ của mọi prefab đều
        /// là false, giữ lại là giữ đúng cái lỗi này. Bỏ hẳn tên cũ thì prefab nào cũng
        /// nhận mặc định true.
        ///
        /// Property để virtual, không phải để chiều ý ai: popup nào KHÔNG phủ kín màn
        /// hình thì việc nó không chặn chạm là SỰ THẬT VỀ CẤU TRÚC của nó, không phải
        /// một lựa chọn trong Inspector. Lời nhắc dạng toast là đúng ca đó — nó nổi lên
        /// một góc, người chơi vẫn phải tô tiếp bên dưới. Để nó phụ thuộc vào một ô tick
        /// nghĩa là ai đó dựng lại prefab, quên tick, và cả game khoá cứng vì một câu
        /// nhắc dài một giây. Chặn hay không của những popup như thế thuộc về class.
        [Tooltip("Chặn chạm xuống màn chơi phía sau khi popup này đang mở.\n\n" +
                 "Mặc định BẬT. Bỏ tick cho popup KHÔNG phủ kín màn hình — lời nhắc dạng " +
                 "toast chẳng hạn — vì nó vẫn phải cho người chơi tô tiếp.\n\n" +
                 "Đây không phải ô làm tối nền: tối hay không là do alpha của tấm chặn " +
                 "bên PopupManager.")]
        [SerializeField] private bool _blocksBackground = true;

        public virtual bool BlocksBackground => _blocksBackground;

        /// Tiếng phát lúc popup ĐÓNG. Virtual vì có popup mà việc im lặng là sự thật về
        /// cấu trúc của nó chứ không phải một lựa chọn trong Inspector — lời nhắc dạng
        /// toast tự bật tự tắt là đúng ca đó.
        protected virtual SoundKey CloseSound => _closeSound;

        protected CanvasGroup CanvasGroup => _canvasGroup;

        /// null cho tới khi PopupManager trao vào. Mọi chỗ dùng phải tự kiểm null —
        /// popup đặt sẵn trong scene để thử nghiệm sẽ không bao giờ nhận được nó.
        protected ISoundService Sound { get; private set; }

        /// PopupManager gọi ngay sau khi tạo instance.
        ///
        /// Trao bằng một hàm chứ không dùng [Inject]: gần hết popup con đã có sẵn một
        /// hàm Construct mang [Inject] của riêng nó, và chồng thêm một hàm [Inject] ở
        /// lớp cha là loại phụ thuộc vào hành vi của container mà không ai kiểm được
        /// bằng mắt. Một lời gọi tường minh thì đọc phát ra ngay.
        public void SetSoundService(ISoundService sound) => Sound = sound;

        public virtual void Show()
        {
            KillFade();

            _isShown = true;

            gameObject.SetActive(true);

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            var duration = Mathf.Max(0f, _fadeInDuration);

            if (duration <= 0f)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            _canvasGroup.alpha = 0f;
            _fade = FadeTo(1f, duration);
        }

        public virtual void Hide() => Close(playCloseSound: true);

        /// Đóng mà KHÔNG kêu tiếng đóng. Dùng ở những đường ra đã có tiếng riêng: nút
        /// Home trong popup Cài đặt phát Direction, kêu thêm Cancel là hai tiếng chồng lên
        /// nhau trong cùng một cú bấm.
        public void HideSilently() => Close(playCloseSound: false);

        private void Close(bool playCloseSound)
        {
            KillFade();

            // Tiếng đóng phát ở đây chứ không ở từng nút đóng: mỗi popup có mấy đường ra
            // khác nhau — nút X, nút Back, HideAll gọi từ chỗ khác — mà tất cả đều đi qua
            // đúng chỗ này.
            //
            // Phát TRƯỚC nhánh thoát bên dưới thì popup đã đóng sẵn vẫn kêu thêm một
            // tiếng nữa, nên nó nằm sau.
            var closeSound = CloseSound;

            if (playCloseSound && _isShown && closeSound != SoundKey.None && Sound != null)
            {
                Sound.Play(closeSound);
            }

            // Đã đóng rồi thì chỉ chốt lại cho chắc. Không có nhánh này thì HideAll gọi
            // lên một popup đang mờ dở sẽ khởi động lại cú mờ từ giữa chừng.
            if (!_isShown)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            _isShown = false;

            // Khoá chạm NGAY, không đợi mờ xong: suốt quãng đang tan, popup chỉ còn là
            // hình ảnh. Bấm thêm lần nữa vào nút đóng vì thế không chạy thêm lần nào.
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var duration = Mathf.Max(0f, _fadeOutDuration);

            if (duration <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            _fade = FadeTo(0f, duration).OnComplete(() =>
            {
                _fade = null;

                // CanvasGroup nằm cùng object, nên nó chết là object cũng chết. Không có
                // chốt này thì đổi scene giữa lúc popup đang tan sẽ ném lỗi ở dòng dưới.
                if (_canvasGroup == null) return;

                gameObject.SetActive(false);
            });
        }

        /// DOVirtual.Float chứ KHÔNG phải CanvasGroup.DOFade.
        ///
        /// DOFade nằm trong DOTweenModuleUI.cs — một file .cs rời trong Plugins, nên nó
        /// biên dịch vào Assembly-CSharp, mà asmdef JewelPainter.UI không với tới assembly
        /// đó. Chỉ DOTween.dll là auto-reference. Cùng cái bẫy đã ghi ở WinPopupView.
        ///
        /// SetUpdate(true) — chạy theo thời gian KHÔNG phụ thuộc timeScale. Popup rất hay
        /// mở đúng lúc game bị dừng, mà một cú mờ đứng hình thì vô nghĩa.
        private Tween FadeTo(float target, float duration)
        {
            var from = _canvasGroup.alpha;

            return DOVirtual.Float(from, target, duration, value =>
                {
                    if (_canvasGroup != null) _canvasGroup.alpha = value;
                })
                .SetUpdate(true);
        }

        /// CỐ Ý không có OnDisable/OnDestroy ở lớp cha.
        ///
        /// Unity gọi mấy hàm vòng đời theo TÊN, không qua bảng ảo. Năm popup con đang khai
        /// `private void OnDestroy()` của riêng chúng, và một hàm cùng tên ở lớp cha sẽ bị
        /// che im lặng — Unity chỉ gọi bản con, phần dọn dẹp ở đây không bao giờ chạy. Đó
        /// là kiểu hỏng không có lỗi biên dịch nào chỉ ra.
        ///
        /// Không cần chúng: KillFade đã chạy ở đầu cả Show lẫn Hide, nên không có đường
        /// nào để hai lượt mờ chồng lên nhau. Còn tween sống sót qua lúc object bị tắt thì
        /// vô hại — lượt Show kế tiếp đặt lại alpha trước khi nó kịp ghi gì.
        private void KillFade()
        {
            if (_fade == null) return;

            if (_fade.IsActive()) _fade.Kill();

            _fade = null;
        }
    }
}
