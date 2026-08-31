using JewelPainter.Gameplay.Domain;
using Unity.Collections;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kẻ viền quanh từng ô CÓ MÀU bằng shader, thay cho BoardGridLines vốn nướng sẵn
    /// nét vào một texture cỡ cả bảng.
    ///
    /// Thứ duy nhất gửi lên GPU là MASK: mỗi texel đúng một ô, R = 255 nghĩa là ô đó có
    /// màu. Bảng 72x71 ra 74x73 byte, tức 5 KB — bản nướng sẵn tốn 16 MB cho đúng chừng
    /// ấy thông tin, vì mỗi texel 4 byte mà chỉ mang một bit.
    ///
    /// Được gì ngoài bộ nhớ:
    ///   - bề dày nét đo bằng PIXEL MÀN HÌNH nên không mất nét, không dày mỏng thất
    ///     thường ở bất cứ mức zoom nào;
    ///   - mép nét được khử răng cưa, kéo bảng không thấy rung;
    ///   - mask 5 KB nằm gọn trong cache texture của GPU. Texture 16 MB lấy mẫu Point
    ///     lúc thu nhỏ là trường hợp tệ nhất cho cache di động, và trên máy yếu khoản
    ///     này thường ăn hơn cả việc giảm bộ nhớ;
    ///   - không còn dựng texture cỡ lớn lúc vào màn, bớt một khựng khi chuyển màn.
    ///
    /// Vẫn là một SpriteRenderer nên BoardColorFade gắn kèm hoạt động y như cũ: nó chỉ
    /// ghi alpha, còn RGB do _lineColor đặt một lần lúc dựng bảng.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardGridLinesShaded : MonoBehaviour, IBoardGridLines
    {
        /// Viền đệm một ô rỗng quanh mask, và sprite phủ trọn cả phần đệm.
        ///
        /// Nét nằm chính giữa ranh giới, nên ở rìa bảng nửa ngoài của nó rơi ra ngoài
        /// khối ô. Không có đệm thì nửa đó bị cắt và rìa bảng lại mảnh bằng nửa — đúng
        /// cái lỗi mà cách vẽ này sinh ra để dẹp.
        private const int Padding = 1;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidthPixels");
        private static readonly int SoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int ReferenceHeightId = Shader.PropertyToID("_ReferenceScreenHeight");

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Shader JewelPainter/Board Grid Lines. Kéo file Art/BoardGridLines.shader " +
                 "vào đây — tham chiếu này cũng là thứ giữ shader không bị cắt khỏi bản build.")]
        [SerializeField] private Shader _shader;

        [Tooltip("Bề dày nét tính bằng PIXEL MÀN HÌNH, giữ nguyên ở mọi mức zoom. " +
                 "1 là mảnh nhất còn rõ, 1.5 đến 2 là dễ nhìn trên điện thoại.")]
        [Range(0.5f, 8f)]
        [SerializeField] private float _lineWidthPixels = 1.5f;

        [Tooltip("Bề rộng dải chuyển ở mép nét, tính bằng pixel. 1 là vừa đủ mượt. " +
                 "0 cho mép cứng và răng cưa quay lại, trên 2 thì nét bắt đầu nhoè.")]
        [Range(0f, 3f)]
        [SerializeField] private float _edgeSoftness = 1f;

        [Tooltip("Chiều cao màn hình mà Line Width Pixels được canh theo. Máy phân giải " +
                 "cao hơn thì nét dày lên cùng tỉ lệ, nên nét trông bằng nhau trên mọi " +
                 "máy. Để 0 thì bề dày tính đúng bằng pixel vật lý, và nét sẽ mảnh đi " +
                 "trông thấy trên màn hình dày pixel.")]
        [SerializeField] private float _referenceScreenHeight = 1080f;

        [Tooltip("Màu nét. Alpha ở đây KHÔNG dùng — phần mờ dần do BoardColorFade lo.")]
        [SerializeField] private Color _lineColor = new Color32(159, 159, 159, 255);

        private BoardView _boardView;
        private Texture2D _mask;
        private Sprite _sprite;
        private Material _material;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;

            Release();
        }

#if UNITY_EDITOR
        /// Chỉnh núm trong Inspector lúc đang chạy là thấy ngay, không phải vào lại màn.
        private void OnValidate()
        {
            if (Application.isPlaying) PushMaterialSettings();
        }
#endif

        private void HandleBoardRebuilt()
        {
            Release();

            var grid = _boardView.Grid;
            if (grid == null)
            {
                _renderer.sprite = null;
                return;
            }

            if (_shader == null)
            {
                Debug.LogError(
                    "BoardGridLinesShaded: chưa gán Shader. Kéo Art/BoardGridLines.shader " +
                    "vào ô Shader — không có nó thì viền ô không hiện.", this);
                _renderer.sprite = null;
                return;
            }

            BuildMask(grid);

            // pixelsPerUnit = 1: một texel mask là một ô, mà một ô là một world unit.
            // Sprite vì thế rộng đúng (Width + 2) x (Height + 2) unit và căn giữa gốc
            // toạ độ như bảng — phần thừa chính là viền đệm.
            _sprite = Sprite.Create(
                _mask,
                new Rect(0f, 0f, _mask.width, _mask.height),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);

            _material = new Material(_shader) { name = "BoardGridLines (runtime)" };

            _renderer.sprite = _sprite;
            _renderer.sharedMaterial = _material;

            // Chỉ đặt RGB. Alpha là của BoardColorFade, ghi đè vào đây là khoá luôn
            // phần mờ dần của nó.
            var current = _renderer.color;
            _renderer.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, current.a);

            PushMaterialSettings();
        }

        private void PushMaterialSettings()
        {
            if (_material == null) return;

            _material.SetTexture(MainTexId, _mask);
            _material.SetFloat(LineWidthId, _lineWidthPixels);
            _material.SetFloat(SoftnessId, _edgeSoftness);
            _material.SetFloat(ReferenceHeightId, Mathf.Max(0f, _referenceScreenHeight));
        }

        /// Một texel mỗi ô. Không có gì để vẽ ở đây — hình dạng nét là việc của shader,
        /// chỗ này chỉ trả lời "ô nào có màu".
        private void BuildMask(PixelGrid grid)
        {
            var width = grid.Width + Padding * 2;
            var height = grid.Height + Padding * 2;

            // linear: true — kênh R là CỜ chứ không phải màu. Để Unity hiểu là sRGB thì
            // nó bẻ cong giá trị theo đường gamma, và mọi ngưỡng trong shader lệch theo.
            _mask = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Point,
                // Clamp: ở mép mask, ô hàng xóm lấy về chính nó, nên điều kiện "một
                // trong hai ô có màu" vẫn ra đúng kết quả ở rìa.
                wrapMode = TextureWrapMode.Clamp,
                name = "BoardGridLinesMask",
            };

            var pixels = new NativeArray<byte>(
                width * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                for (var y = 0; y < grid.Height; y++)
                {
                    // PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
                    var row = (grid.Height - 1 - y + Padding) * width;

                    for (var x = 0; x < grid.Width; x++)
                    {
                        if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                        pixels[row + x + Padding] = 255;
                    }
                }

                _mask.SetPixelData(pixels, 0);

                // Giữ bản đọc được trên CPU: mask chỉ vài KB nên bỏ đi chẳng lời được
                // gì, mà lại còn dùng để sửa mask về sau nếu cần.
                _mask.Apply(false);
            }
            finally
            {
                pixels.Dispose();
            }
        }

        private void Release()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
                _sprite = null;
            }

            if (_mask != null)
            {
                Destroy(_mask);
                _mask = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
