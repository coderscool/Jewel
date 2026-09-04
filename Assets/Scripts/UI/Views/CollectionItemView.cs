using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô tranh trong popup bộ sưu tập: ảnh màn, số màn, và ổ khoá nếu chưa mở.
    /// Thuần trình bày — không biết gì về tiến trình, chỉ nhận vào một chữ `unlocked`.
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

        private Material _unlockedMaterial;
        private bool _hasCachedMaterial;

        public void Bind(int levelId, Sprite artwork, bool unlocked)
        {
            CacheUnlockedMaterial();

            if (_artwork != null)
            {
                // Tắt component thay vì để sprite null: Image không sprite vẫn vẽ một
                // ô trắng đặc, trông như lỗi hiển thị chứ không như ô trống.
                _artwork.enabled = artwork != null;
                _artwork.sprite = artwork;
                _artwork.color = unlocked ? Color.white : _lockedTint;

                // Giữ đúng tỉ lệ tranh: cạnh chạm khung trước thì dừng ở khung, cạnh kia
                // co theo. Không có nó thì Image kéo ảnh phủ kín ô, và tranh không vuông
                // bị bóp méo.
                //
                // Đặt bằng CODE chứ không tick trong prefab. Đây là luật hiển thị của bộ
                // sưu tập, không phải lựa chọn thẩm mỹ của từng ô: tranh bị kéo giãn là
                // tranh SAI. Một cái tick trong Inspector thì lần sau ai dựng lại prefab
                // là mất, mà mất thì không có gì báo.
                //
                // Chỉ có tác dụng khi Image Type là Simple hoặc Filled — Sliced và Tiled
                // bỏ qua preserveAspect. Prefab hiện đang để Simple.
                _artwork.preserveAspect = true;

                if (_lockedMaterial != null)
                {
                    _artwork.material = unlocked ? _unlockedMaterial : _lockedMaterial;
                }
            }

            if (_lockIcon != null) _lockIcon.SetActive(!unlocked);
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
