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
