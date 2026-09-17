using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Một hoạt ảnh đã bake thành dãy sprite: dãy khung hình cộng nhịp phát.
    [CreateAssetMenu(menuName = "JewelPainter/Flipbook Clip", fileName = "FlipbookClip")]
    public class FlipbookClip : ScriptableObject
    {
        [Tooltip("Dãy khung hình theo đúng thứ tự phát.")]
        [SerializeField] private Sprite[] _frames = new Sprite[0];

        [Tooltip("Số khung hình mỗi giây.")]
        [SerializeField] private float _fps = 30f;

        public int FrameCount => _frames != null ? _frames.Length : 0;

        public float Fps => _fps;

        public float Duration => _fps > 0f ? FrameCount / _fps : 0f;

        public Sprite Frame(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Length) return null;
            return _frames[index];
        }

        public bool IsUsable => FrameCount > 0 && _fps > 0f;

    }
}
