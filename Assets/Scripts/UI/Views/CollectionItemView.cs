using JewelPainter.Gameplay.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô tranh trong popup bộ sưu tập: ảnh màn, số màn, và ổ khoá nếu chưa mở.
    /// Thuần trình bày — không biết gì về tiến trình, chỉ nhận vào một chữ `unlocked`.
    ///
    /// Tranh LỌT TRONG KHUNG, không phủ kín: cạnh dài chạm khung trước thì dừng ở đó,
    /// cạnh kia hở ra hai dải nền. Cả bức tranh luôn nhìn thấy được, không bị xén.
    ///
    /// Chọn lọt-trong-khung chứ không phủ-kín vì đây là BỘ SƯU TẬP. Người chơi mở nó ra
    /// để ngắm lại thứ mình đã tô xong, mà phủ kín thì bức nào cao hoặc dẹt cũng bị cắt
    /// mất hai đầu — đúng phần người ta muốn xem. Khung đầy đẹp hơn, nhưng nó đẹp bằng
    /// tài sản của người chơi.
    ///
    /// **Mask trên ô cha vẫn cần**, dù không có gì tràn ra ở tranh chữ nhật: tranh VUÔNG
    /// trong ô vuông thì lọt-trong-khung tức là lấp kín tuyệt đối, và bốn góc vuông của nó
    /// thò ra khỏi viền cong của ô. Đó là trường hợp duy nhất cần cắt, nhưng nó xảy ra với
    /// mọi bức tranh vuông nên không phải trường hợp hiếm.
    ///
    /// Muốn đổi sang phủ kín thì thêm AspectRatioFitter (Envelope Parent) lên object
    /// Artwork và tắt preserveAspect ở dưới — hai thứ đó không được bật cùng lúc, vì
    /// fitter phóng khung cho phủ kín rồi preserveAspect lại co ảnh cho lọt vào khung vừa
    /// phóng, ra ảnh nhỏ hơn cả lúc không ai can thiệp.
    public class CollectionItemView : MonoBehaviour
    {
        [SerializeField] private Image _artwork;

        [Tooltip("Ổ khoá đè lên ảnh. Bật khi màn chưa mở.")]
        [SerializeField] private GameObject _lockIcon;

        [Tooltip("Màu nhân vào ảnh khi màn chưa mở. Xám tối để tranh chìm xuống nhưng " +
                 "vẫn đoán được là hình gì.")]
        [SerializeField] private Color _lockedTint = new(0.42f, 0.42f, 0.45f, 1f);

        [Tooltip("Để trống cũng chạy. Muốn ảnh khoá XÁM THẬT (mất hết màu) thì gán một " +
                 "material dùng shader greyscale vào đây — Locked Tint chỉ làm ảnh tối " +
                 "đi chứ không rút màu ra được.")]
        [SerializeField] private Material _lockedMaterial;

        [Tooltip("Màn nào chọn INSET thì tranh thu vào cả bốn phía chừng này pixel.\n\n" +
                 "Con số của GIAO DIỆN, không phải của từng màn: lề thở phải bằng nhau " +
                 "trên mọi ô, không thì cả trang bộ sưu tập trông lỗ chỗ. Từng màn chỉ " +
                 "chọn CÓ hay KHÔNG chừa lề, ở ô Collection Fit bên LevelConfig.")]
        [SerializeField] private float _insetPixels = 20f;

        private Material _unlockedMaterial;
        private bool _hasCachedMaterial;

        /// Ô Artwork có căng kín ô cha không. Đo một lần ở lần Bind đầu.
        private bool _hasWarnedAnchors;

        public void Bind(int levelId, Sprite artwork, bool unlocked, CollectionArtworkFit fit)
        {
            CacheUnlockedMaterial();

            if (_artwork != null)
            {
                // Tắt component thay vì để sprite null: Image không sprite vẫn vẽ một
                // ô trắng đặc, trông như lỗi hiển thị chứ không như ô trống.
                _artwork.enabled = artwork != null;
                _artwork.sprite = artwork;
                _artwork.color = unlocked ? Color.white : _lockedTint;

                // Giữ đúng tỉ lệ tranh: cạnh dài chạm khung trước thì dừng ở khung, cạnh
                // kia co theo. Không có nó thì Image kéo ảnh phủ kín ô, và tranh không
                // vuông bị bóp méo.
                //
                // Đặt bằng CODE chứ không tick trong prefab. Đây là luật hiển thị của bộ
                // sưu tập, không phải lựa chọn thẩm mỹ của từng ô: tranh bị kéo giãn là
                // tranh SAI. Một cái tick trong Inspector thì lần sau ai dựng lại prefab
                // là mất, mà mất thì không có gì báo.
                //
                // Chỉ có tác dụng khi Image Type là Simple hoặc Filled — Sliced và Tiled
                // bỏ qua preserveAspect. Prefab hiện đang để Simple.
                _artwork.preserveAspect = true;

                ApplyFit(fit);

                if (_lockedMaterial != null)
                {
                    _artwork.material = unlocked ? _unlockedMaterial : _lockedMaterial;
                }
            }

            if (_lockIcon != null) _lockIcon.SetActive(!unlocked);
        }

        /// Thu ô Artwork vào bốn phía, hoặc trả nó về ăn kín ô cha.
        ///
        /// Thu bằng RECT chứ không bằng scale: preserveAspect luôn co tranh cho lọt vào
        /// cái rect nó đang có, nên thu rect lại là tranh tự nhỏ theo mà vẫn giữ nguyên tỉ
        /// lệ. Thu bằng localScale thì được đúng cỡ đó nhưng vùng nhận chạm và vùng bị
        /// Mask cắt vẫn là rect cũ — hai thứ lệch nhau, và cái lệch đó chỉ lộ ra ở ô nào
        /// có tranh vuông.
        ///
        /// Đặt lại ở MỖI lần Bind, kể cả khi Full: các ô được tái dùng qua pool, nên một ô
        /// vừa hiện màn Inset có thể lượt sau mang màn Full. Không trả về thì lề 20px của
        /// màn trước dính lại trên một bức đáng lẽ ăn sát mép.
        private void ApplyFit(CollectionArtworkFit fit)
        {
            if (_artwork == null) return;

            var rect = _artwork.rectTransform;

            WarnIfNotStretched(rect);

            var inset = fit == CollectionArtworkFit.Inset ? Mathf.Max(0f, _insetPixels) : 0f;

            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// Ô Artwork phải neo CĂNG KÍN ô cha thì offsetMin/offsetMax mới có nghĩa là lề.
        ///
        /// Neo vào một điểm thì hai giá trị đó là vị trí và kích thước, nên đặt lề 20px sẽ
        /// dịch tranh đi và bóp nó lại theo một kiểu chẳng ai định — mà vẫn không báo lỗi.
        /// Báo đúng một lần cho mỗi ô, và chỉ trong Editor: đây là lỗi dựng prefab, sửa
        /// một lần là xong, không đáng đổ log vào bản phát hành.
        private void WarnIfNotStretched(RectTransform rect)
        {
#if UNITY_EDITOR
            if (_hasWarnedAnchors) return;

            var stretched = Mathf.Approximately(rect.anchorMin.x, 0f)
                            && Mathf.Approximately(rect.anchorMin.y, 0f)
                            && Mathf.Approximately(rect.anchorMax.x, 1f)
                            && Mathf.Approximately(rect.anchorMax.y, 1f);

            if (stretched) return;

            _hasWarnedAnchors = true;

            Debug.LogWarning($"{nameof(CollectionItemView)}: ô Artwork chưa neo căng kín ô " +
                             "cha (Anchor Min 0,0 — Anchor Max 1,1), nên phần chừa lề của " +
                             "kiểu INSET sẽ dịch tranh đi thay vì thu nó lại.", this);
#endif
        }

        /// Ghi lại material gốc ở lần Bind ĐẦU TIÊN. Đọc muộn hơn là đọc nhầm
        /// _lockedMaterial mà chính mình vừa gán vào.
        private void CacheUnlockedMaterial()
        {
            if (_hasCachedMaterial || _artwork == null) return;

            _unlockedMaterial = _artwork.material;
            _hasCachedMaterial = true;
        }
    }
}
