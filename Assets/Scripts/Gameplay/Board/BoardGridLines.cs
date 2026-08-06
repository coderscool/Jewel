using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kẻ khung quanh từng ô CÓ MÀU, để phân biệt các ô khi lớp màu đã trong suốt.
    /// Ô rỗng không có khung.
    ///
    /// Khung của mỗi ô nằm GỌN trong khối pixel của chính ô đó, không tràn sang ô bên.
    ///
    /// Vẽ vào một texture dựng MỘT LẦN lúc vào màn rồi không đụng tới nữa — khác
    /// BoardView và HintOverlay vốn phải ghi lại mỗi khi tô. Nhờ tĩnh nên để độ phân
    /// giải cao thoải mái: chi phí chỉ là một lần upload lúc chuyển cảnh.
    ///
    /// Không tự lo phần mờ dần. Gắn thêm một BoardColorFade lên chính object này với
    /// hai mốc đảo ngược so với bảng màu là viền tự hiện ra đúng lúc bảng biến mất.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardGridLines : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Số pixel mỗi ô trong texture viền. Texture dựng một lần lúc vào màn nên " +
                 "để cao cũng không tốn gì lúc chơi. Số này càng lớn thì đường càng MẢNH " +
                 "so với ô: 16 cho đường bằng 1/16 bề rộng ô, 32 cho mảnh gấp đôi thế.")]
        [SerializeField] private int _pixelsPerCell = 16;

        [Tooltip("Độ dày đường viền, tính bằng pixel của texture. Muốn đường mảnh hơn nữa " +
                 "thì giữ 1 và nâng Pixels Per Cell lên.")]
        [SerializeField] private int _lineThickness = 1;

        [Tooltip("Khoảng thụt vào từ mép ô, tính bằng pixel của texture. Để 0 thì khung " +
                 "sát mép ô. Tăng lên để hai ô cạnh nhau có khe hở giữa hai khung.")]
        [SerializeField] private int _inset;

        [SerializeField] private Color32 _lineColor = new Color32(255, 255, 255, 255);

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

            var cellPixels = Mathf.Max(2, _pixelsPerCell);
            var thickness = Mathf.Clamp(_lineThickness, 1, cellPixels / 2);

            var width = grid.Width * cellPixels;
            var height = grid.Height * cellPixels;
            var pixels = new Color32[width * height];

            DrawGrid(grid, pixels, width, cellPixels, thickness);

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(false);

            // pixelsPerUnit = cellPixels để sprite rộng đúng grid.Width world unit,
            // chồng khít lên bảng dù texture mịn hơn.
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                cellPixels);

            _renderer.sprite = _sprite;
        }

        /// Mỗi ô có màu được một khung KHÉP KÍN nằm gọn trong đúng khối pixel của nó.
        ///
        /// Không nhìn sang hàng xóm: khung của ô nào thuộc về ô đó. Hai ô kề nhau sẽ có
        /// hai đường sát nhau — muốn tách rời hẳn thì tăng Inset để chừa khe hở.
        private void DrawGrid(PixelGrid grid, Color32[] pixels, int textureWidth, int cellPixels, int thickness)
        {
            var inset = Mathf.Clamp(_inset, 0, (cellPixels - thickness * 2) / 2);
            var side = cellPixels - inset * 2;

            if (side < thickness * 2) return;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                    var originX = x * cellPixels + inset;
                    // PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
                    var originY = (grid.Height - 1 - y) * cellPixels + inset;

                    // Cạnh dưới và cạnh trên trong không gian texture.
                    FillRect(pixels, textureWidth, originX, originY, side, thickness);
                    FillRect(pixels, textureWidth, originX, originY + side - thickness, side, thickness);

                    // Cạnh trái và cạnh phải, đã trừ phần góc để khỏi ghi đè hai lần.
                    var verticalY = originY + thickness;
                    var verticalHeight = side - thickness * 2;
                    if (verticalHeight <= 0) continue;

                    FillRect(pixels, textureWidth, originX, verticalY, thickness, verticalHeight);
                    FillRect(pixels, textureWidth,
                        originX + side - thickness, verticalY, thickness, verticalHeight);
                }
            }
        }

        private void FillRect(Color32[] pixels, int textureWidth, int originX, int originY, int width, int height)
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
