using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Một hoạt ảnh đã bake thành dãy sprite: dãy khung hình cộng nhịp phát.
    ///
    /// Gói lại thành asset thay vì để mảng Sprite trần trong Inspector của kho hiệu ứng
    /// vì hai lý do. Một, bake xong ba chục khung mà phải kéo tay từng cái vào mảng thì
    /// không ai làm nổi — công cụ bake tự điền vào đây, người dùng kéo đúng MỘT asset.
    /// Hai, nhịp phát thuộc về chính hoạt ảnh chứ không thuộc về nơi phát nó: cùng một
    /// cú loé, dùng ở chỗ ngọc đáp hay chỗ tô xong màu thì vẫn phải chạy đúng tốc độ mà
    /// artist đã làm.
    [CreateAssetMenu(menuName = "JewelPainter/Flipbook Clip", fileName = "FlipbookClip")]
    public class FlipbookClip : ScriptableObject
    {
        [Tooltip("Dãy khung hình theo đúng thứ tự phát. Công cụ Bake Spine → Flipbook " +
                 "tự điền, thường không phải sửa tay.")]
        [SerializeField] private Sprite[] _frames = new Sprite[0];

        [Tooltip("Số khung hình mỗi giây. Đây là nhịp lúc BAKE — đổi số này là phát " +
                 "nhanh/chậm hơn bản gốc chứ không bake lại.")]
        [SerializeField] private float _fps = 30f;

        public int FrameCount => _frames != null ? _frames.Length : 0;

        public float Fps => _fps;

        /// Dài bao nhiêu giây khi phát ở tốc độ 1. Kho hiệu ứng dùng số này để biết lúc
        /// nào thu về, nên nó phải trả 0 khi chưa có khung nào — thu về ngay còn hơn giữ
        /// một ô trống chiếm chỗ trong trần đồng thời.
        public float Duration => _fps > 0f ? FrameCount / _fps : 0f;

        public Sprite Frame(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Length) return null;
            return _frames[index];
        }

        public bool IsUsable => FrameCount > 0 && _fps > 0f;

#if UNITY_EDITOR
        /// Chỉ dành cho công cụ bake. Không gọi lúc chơi.
        public void EditorFill(Sprite[] frames, float fps)
        {
            _frames = frames;
            _fps = fps;
        }
#endif
    }
}
