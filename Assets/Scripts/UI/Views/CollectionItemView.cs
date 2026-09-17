using JewelPainter.Gameplay.Config;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô tranh trong popup bộ sưu tập: ảnh màn, số màn, và ổ khoá nếu chưa mở.
    public class CollectionItemView : MonoBehaviour
    {
        [SerializeField] private Image _artwork;

        [Tooltip("Ổ khoá đè lên ảnh.")]
        [SerializeField] private GameObject _lockIcon;

        [Tooltip("Màu nhân vào ảnh khi màn chưa mở.")]
        [SerializeField] private Color _lockedTint = new(0.42f, 0.42f, 0.45f, 1f);

        [Tooltip("Material cho ảnh khi màn chưa mở.")]
        [SerializeField] private Material _lockedMaterial;

        [Tooltip("Số pixel tranh thu vào mỗi phía khi dùng chế độ Inset.")]
        [SerializeField] private float _insetPixels = 20f;

        private Material _unlockedMaterial;
        private bool _hasCachedMaterial;

        private bool _hasWarnedAnchors;

        public void Bind(int levelId, Sprite artwork, bool unlocked, CollectionArtworkFit fit)
        {
            CacheUnlockedMaterial();

            if (_artwork != null)
            {
                _artwork.enabled = artwork != null;
                _artwork.sprite = artwork;
                _artwork.color = unlocked ? Color.white : _lockedTint;

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
        private void ApplyFit(CollectionArtworkFit fit)
        {
            if (_artwork == null) return;

            var rect = _artwork.rectTransform;

            WarnIfNotStretched(rect);

            var inset = fit == CollectionArtworkFit.Inset ? Mathf.Max(0f, _insetPixels) : 0f;

            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// Cảnh báo khi ô Artwork không neo căng kín ô cha.
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

        /// Ghi lại material gốc ở lần Bind đầu tiên.
        private void CacheUnlockedMaterial()
        {
            if (_hasCachedMaterial || _artwork == null) return;

            _unlockedMaterial = _artwork.material;
            _hasCachedMaterial = true;
        }
    }
}
