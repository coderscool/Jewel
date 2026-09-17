using JewelPainter.Gameplay.Domain;
using Unity.Collections;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kẻ khung quanh từng ô có màu, để phân biệt các ô khi lớp màu đã trong suốt.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardGridLines : MonoBehaviour, IBoardGridLines
    {
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Số pixel mỗi ô trong texture viền.")]
        [SerializeField] private int _pixelsPerCell = 16;

        [Tooltip("Cạnh dài nhất cho phép của texture viền, tính bằng pixel.")]
        [SerializeField] private int _maxTextureSize = 2048;

        [Tooltip("Độ dày đường viền, tính bằng pixel của texture.")]
        [SerializeField] private int _lineThickness = 1;

        [Tooltip("Khoảng thụt vào từ mép ô, tính bằng pixel của texture.")]
        [SerializeField] private int _inset;

        [SerializeField] private Color32 _lineColor = new Color32(255, 255, 255, 255);

        [Tooltip("Sinh mipmap cho texture viền.")]
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

            var pixels = new NativeArray<Color32>(
                width * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                DrawGrid(grid, pixels, width, cellPixels, thickness);

                _texture.SetPixelData(pixels, 0);

                _sprite = Sprite.Create(
                    _texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f),
                    cellPixels,
                    0,
                    SpriteMeshType.FullRect);

                _texture.Apply(_generateMipmaps, true);
            }
            finally
            {
                pixels.Dispose();
            }

            _renderer.sprite = _sprite;
        }

        /// Tính số texel mỗi ô của texture viền.
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

        /// Kẻ viền quanh các ô có màu.
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
                    var originY = (grid.Height - 1 - y) * cellPixels + inset;

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

        /// Ô rỗng hoặc nằm ngoài bảng.
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
