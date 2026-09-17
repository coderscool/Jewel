using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Một ô màn chơi trong danh sách ở màn hình Home.
    public class HomeLevelItemView : MonoBehaviour
    {
        [Tooltip("Ảnh của màn.")]
        [SerializeField] private Image _thumbnail;

        [Tooltip("Object hiện khi màn chưa mở khoá.")]
        [SerializeField] private GameObject _lockedPlaceholder;

        [Header("Tuỳ chọn — để trống cũng chạy")]
        [Tooltip("Số màn.")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("Dấu hiệu cho màn đang chơi dở.")]
        [SerializeField] private GameObject _currentHighlight;

        [Header("Chọn")]
        [Tooltip("Viền hiện khi ô đang được chọn.")]
        [SerializeField] private GameObject _outline;

        [Tooltip("Vùng bấm để chọn ô.")]
        [SerializeField] private Button _button;

        private Action<int> _onClicked;
        private bool _hasWarned;

        public int LevelId { get; private set; }

        public RectTransform ThumbnailRect =>
            _thumbnail != null ? (RectTransform)_thumbnail.transform : null;

        public Sprite ThumbnailSprite => _thumbnail != null ? _thumbnail.sprite : null;

        /// Đăng ký sự kiện nút.
        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(int levelId, Sprite thumbnail, bool isUnlocked, bool isCurrent, Action<int> onClicked)
        {
            WarnOnMissingReferences();

            LevelId = levelId;
            _onClicked = onClicked;

            if (_button != null) _button.interactable = isUnlocked;

            if (_levelText != null)
            {
                _levelText.gameObject.SetActive(!isUnlocked);

                if (!isUnlocked) _levelText.SetText("{0}", levelId);
            }

            if (_thumbnail != null)
            {
                _thumbnail.enabled = isUnlocked && thumbnail != null;
                _thumbnail.sprite = thumbnail;
            }

            if (_lockedPlaceholder != null) _lockedPlaceholder.SetActive(!isUnlocked);
            if (_currentHighlight != null) _currentHighlight.SetActive(isCurrent);
        }

        public void SetSelected(bool selected)
        {
            if (_outline == null) return;

            if (_outline.activeSelf != selected) _outline.SetActive(selected);
        }

        private void HandleClicked() => _onClicked?.Invoke(LevelId);

        /// Cảnh báo khi thiếu reference trong Inspector.
        private void WarnOnMissingReferences()
        {
            if (_hasWarned) return;
            if (_thumbnail != null && _lockedPlaceholder != null) return;

            _hasWarned = true;

            Debug.LogWarning($"{nameof(HomeLevelItemView)} trên '{name}' còn ô chưa gán: " +
                             $"Thumbnail={(_thumbnail != null ? "ok" : "TRỐNG")}, " +
                             $"Locked Placeholder={(_lockedPlaceholder != null ? "ok" : "TRỐNG")}.",
                this);
        }
    }
}
