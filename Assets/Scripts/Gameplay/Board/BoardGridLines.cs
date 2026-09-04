using JewelPainter.Gameplay.Domain;
using Unity.Collections;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kẻ khung quanh từng ô CÓ MÀU, để phân biệt các ô khi lớp màu đã trong suốt.
    /// Ô rỗng không có khung.
    ///
    /// Khung của mỗi ô nằm GỌN trong khối pixel của chính ô đó, không tràn sang ô bên,
    /// và mỗi ranh giới chỉ được kẻ MỘT lần nên mọi đường dày như nhau — xem DrawGrid.
    ///
    /// Vẽ vào một texture dựng MỘT LẦN lúc vào màn rồi không đụng tới nữa — khác
    /// BoardView và HintOverlay vốn phải ghi lại mỗi khi tô. Nhờ tĩnh nên để độ phân
    /// giải cao thoải mái: chi phí chỉ là một lần upload lúc chuyển cảnh.
    ///
    /// Không tự lo phần mờ dần. Gắn thêm một BoardColorFade lên chính object này với
    /// hai mốc đảo ngược so với bảng màu là viền tự hiện ra đúng lúc bảng biến mất.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardGridLines : MonoBehaviour, IBoardGridLines
    {
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Số pixel mỗi ô trong texture viền. Số này càng lớn thì đường càng MẢNH " +
                 "so với ô: 16 cho đường bằng 1/16 bề rộng ô, 32 cho mảnh gấp đôi thế.\n\n" +
                 "ĐỪNG để lớn hơn số pixel MÀN HÌNH mà một ô chiếm ở mức zoom viền hiện rõ. " +
                 "Vượt qua đó là texture bị THU NHỎ khi vẽ, mà lọc Point thì mỗi pixel màn " +
                 "hình chỉ lấy đúng một texel — nguyên những đường kẻ dày 1 texel bị bỏ rơi, " +
                 "và đó chính là cảnh 'chỗ có đường chỗ không'. Max Texture Size bên dưới " +
                 "cắt hộ phần lố, nhưng cắt theo cỡ bảng chứ không biết màn hình.")]
        [SerializeField] private int _pixelsPerCell = 16;

        [Tooltip("Cạnh dài nhất cho phép của texture viền, tính bằng pixel.\n\n" +
                 "Vừa là trần bộ nhớ, vừa là chặn trên cho Pixels Per Cell: bảng 72 ô với " +
                 "64 pixel/ô ra texture 4608x4608, tức 84 MB RGBA — quá sức máy yếu, mà lại " +
                 "mịn hơn màn hình nên phần thừa chỉ tổ làm đường kẻ bị bỏ rơi lúc thu nhỏ. " +
                 "2048 cho bảng 72 ô là 28 pixel/ô, vẫn to hơn số texel màn hình cần.")]
        [SerializeField] private int _maxTextureSize = 2048;

        [Tooltip("Độ dày đường viền, tính bằng pixel của texture. Muốn đường mảnh hơn nữa " +
                 "thì giữ 1 và nâng Pixels Per Cell lên.")]
        [SerializeField] private int _lineThickness = 1;

        [Tooltip("Khoảng thụt vào từ mép ô, tính bằng pixel của texture.\n\n" +
                 "0 — mỗi RANH GIỚI được kẻ đúng MỘT lần, nên mọi đường dày như nhau. " +
                 "Đây là chế độ nên dùng.\n\n" +
                 "Lớn hơn 0 — quay lại lối mỗi ô một khung khép kín riêng, hai ô cạnh nhau " +
                 "thành hai đường cách nhau 2*inset texel. Chỉ dùng khi thật sự muốn thấy " +
                 "khe hở giữa các ô.")]
        [SerializeField] private int _inset;

        [SerializeField] private Color32 _lineColor = new Color32(255, 255, 255, 255);

        [Tooltip("Sinh mipmap cho texture viền. ĐỂ BẬT.\n\n" +
                 "Đây là thứ chữa đúng cảnh 'chỗ có đường chỗ không' mà tooltip của Pixels " +
                 "Per Cell mô tả. Không mipmap thì lúc thu nhỏ, lọc Point lấy đúng MỘT texel " +
                 "cho mỗi pixel màn hình — đường kẻ dày 1 texel nằm giữa 15 texel trống nên " +
                 "phần lớn bị bốc trượt, chỉ vài đường sống sót và hiện ra lỗ chỗ.\n\n" +
                 "Có mipmap thì mức thu nhỏ đã TRUNG BÌNH sẵn đường kẻ với chỗ trống, nên " +
                 "MỌI ranh giới đều còn dấu vết, chỉ là nhạt dần theo mức zoom. Đường mờ đều " +
                 "nhìn đúng hơn hẳn đường sắc nét mọc lỗ chỗ.\n\n" +
                 "Giữ lọc Point: Unity vẫn chọn mipmap, chỉ là không nội suy trong một mức — " +
                 "nên phóng to vẫn sắc cạnh y như cũ.\n\n" +
                 "Cái giá là thêm một phần ba bộ nhớ texture. Rẻ ở đây vì viền chỉ upload " +
                 "MỘT lần lúc vào màn, khác hẳn hai lớp của BoardView vốn bị ghi lại mỗi " +
                 "lần tô — đó là lý do bên kia tắt mipmap còn bên này bật.")]
        [SerializeField] private bool _generateMipmaps = true;

        private BoardView _boardView;
        private Texture2D _texture;
        private Sprite _sprite;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;

            ReleaseTexture();
        }

        private void HandleBoardRebuilt()
        {
            ReleaseTexture();

            var grid = _boardView.Grid;
            if (grid == null)
            {
                _renderer.sprite = null;
                return;
            }

            var cellPixels = ResolveCellPixels(grid);
            var thickness = Mathf.Clamp(_lineThickness, 1, cellPixels / 2);

            var width = grid.Width * cellPixels;
            var height = grid.Height * cellPixels;

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, _generateMipmaps)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BoardGridLines",
            };

            // NativeArray chứ không phải Color32[]: bảng 72 ô ra mảng 16 MB, tức là một
            // cục nằm thẳng vào Large Object Heap mỗi lần vào màn. Bộ nhớ native nằm
            // ngoài tầm GC nên trả lại là xong, không để lại vết cho lần thu gom sau.
            var pixels = new NativeArray<Color32>(
                width * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                DrawGrid(grid, pixels, width, cellPixels, thickness);

                _texture.SetPixelData(pixels, 0);

                // pixelsPerUnit = cellPixels để sprite rộng đúng grid.Width world unit,
                // chồng khít lên bảng dù texture mịn hơn.
                // FullRect: mesh Tight sinh từ đám đường kẻ mảnh này là một đa giác cả
                // nghìn đỉnh, tốn lúc tạo mà bounds lại co về ôm sát nét vẽ thay vì phủ
                // đúng bảng. Nó cũng là lý do dựng sprite được TRƯỚC khi bỏ bản đọc
                // được ngay dưới: FullRect không cần đọc pixel nào.
                _sprite = Sprite.Create(
                    _texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f),
                    cellPixels,
                    0,
                    SpriteMeshType.FullRect);

                // Apply dựng nốt các mức mipmap từ mức 0 mà SetPixelData vừa ghi.
                //
                // makeNoLongerReadable: viền dựng một lần rồi không ai đọc lại, nên bỏ
                // bản sao trên CPU đi — nửa số bộ nhớ của texture này biến mất ở đây.
                _texture.Apply(_generateMipmaps, true);
            }
            finally
            {
                pixels.Dispose();
            }

            _renderer.sprite = _sprite;
        }

        /// Texel của texture viền không được NHỎ hơn pixel màn hình. Nhỏ hơn thì lúc vẽ
        /// texture bị thu nhỏ, và lọc Point lấy đúng một texel cho mỗi pixel màn hình —
        /// đường kẻ dày 1 texel rơi vào khe giữa hai lần lấy mẫu là biến mất hẳn, chỗ
        /// còn chỗ mất, và đổi chỗ mỗi lần kéo hay zoom.
        ///
        /// Ở đây không biết màn hình lẫn mức zoom, nên chặn bằng thứ biết được: cỡ
        /// texture. Trần này đồng thời giữ bộ nhớ trong tầm — texture viền là RGBA32
        /// không nén, cạnh gấp đôi là bộ nhớ gấp bốn.
        private int ResolveCellPixels(PixelGrid grid)
        {
            var requested = Mathf.Max(2, _pixelsPerCell);

            var longestSide = Mathf.Max(grid.Width, grid.Height);
            var allowed = Mathf.Max(2, Mathf.Max(64, _maxTextureSize) / longestSide);

            if (requested <= allowed) return requested;

            Debug.LogWarning(
                $"BoardGridLines: Pixels Per Cell {requested} cho bảng {grid.Width}x{grid.Height} " +
                $"ra texture {grid.Width * requested}x{grid.Height * requested}, quá Max Texture " +
                $"Size {_maxTextureSize} — đã hạ xuống {allowed}.");

            return allowed;
        }

        /// Mỗi RANH GIỚI được kẻ đúng MỘT lần, nên mọi đường dày đúng bằng thickness.
        ///
        /// Lối cũ vẽ cho mỗi ô một khung khép kín riêng, và đó là nguồn của lỗi "chỗ dày
        /// chỗ mỏng": ranh giới giữa hai ô CÙNG có màu được kẻ hai lần, một lần từ mỗi
        /// phía, thành nét dày GẤP ĐÔI; còn mép ngoài của hình chỉ được kẻ một lần nên
        /// mảnh bằng nửa. Thu nhỏ bằng lọc Point thì nét đôi gần như luôn sống sót còn
        /// nét đơn ở rìa hay bị bỏ rơi — thành ra rìa ngoài trông như mất hẳn viền.
        ///
        /// Cách tránh: cạnh TRÁI và cạnh TRÊN thì ô nào cũng kẻ, cạnh PHẢI và cạnh DƯỚI
        /// chỉ kẻ khi hàng xóm bên đó rỗng. Ranh giới trong lòng hình do ô bên phải /
        /// bên dưới nhận, ranh giới ngoài cùng do chính ô ở rìa nhận — không chỗ nào bị
        /// kẻ hai lần, cũng không chỗ nào bị bỏ sót. Nét vẫn nằm gọn trong khối pixel
        /// của ô kẻ nó, không tràn sang ô bên.
        ///
        /// Inset > 0 là chủ ý tách rời hai khung nên giữ nguyên lối cũ: lúc đó hai đường
        /// cách nhau 2*inset texel, không dính lại thành nét dày.
        private void DrawGrid(PixelGrid grid, NativeArray<Color32> pixels, int textureWidth, int cellPixels, int thickness)
        {
            var inset = Mathf.Clamp(_inset, 0, (cellPixels - thickness * 2) / 2);
            var side = cellPixels - inset * 2;

            if (side < thickness * 2) return;

            var separateFrames = inset > 0;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                    var originX = x * cellPixels + inset;
                    // PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
                    var originY = (grid.Height - 1 - y) * cellPixels + inset;

                    // Cạnh TRÊN của ô nằm ở phía y lớn trong texture, ứng với hàng xóm y - 1.
                    FillRect(pixels, textureWidth, originX, originY + side - thickness, side, thickness);
                    FillRect(pixels, textureWidth, originX, originY, thickness, side);

                    if (separateFrames || IsEmpty(grid, x, y + 1))
                    {
                        FillRect(pixels, textureWidth, originX, originY, side, thickness);
                    }

                    if (separateFrames || IsEmpty(grid, x + 1, y))
                    {
                        FillRect(pixels, textureWidth,
                            originX + side - thickness, originY, thickness, side);
                    }
                }
            }
        }

        /// Ngoài bảng cũng tính là rỗng: rìa bảng phải có viền như rìa hình.
        private static bool IsEmpty(PixelGrid grid, int x, int y)
        {
            if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height) return true;

            return grid.GetCell(x, y) == PixelGrid.EmptyCell;
        }

        private void FillRect(NativeArray<Color32> pixels, int textureWidth, int originX, int originY, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                var row = (originY + y) * textureWidth;

                for (var x = 0; x < width; x++)
                {
                    pixels[row + originX + x] = _lineColor;
                }
            }
        }

        private void ReleaseTexture()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
                _sprite = null;
            }

            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
