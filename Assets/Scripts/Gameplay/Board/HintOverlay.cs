using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Đánh dấu những ô tô được bằng màu đang chọn.
    ///
    /// Một texture phủ lên bảng, mặc định trong suốt; ô nào tô được thì stamp hoạ tiết
    /// vào đúng khối pixel của ô đó. Chi phí KHÔNG phụ thuộc số ô — chọn màu phủ 2000 ô
    /// vẫn chỉ là một lần ghi mảng cộng một lần Apply.
    ///
    /// Texture2D không cập nhật được một phần: mỗi Apply là đẩy nguyên tấm lên GPU.
    /// Nên PixelsPerCell quyết định băng thông — xem tooltip của nó.
    [RequireComponent(typeof(SpriteRenderer))]
    public class HintOverlay : MonoBehaviour
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Hoạ tiết lặp ở mỗi ô tô được. Nên là hình TRẮNG trên nền trong suốt " +
                 "để Hint Color nhuộm ra màu gì cũng đúng. Ảnh lớn hơn Pixels Per Cell " +
                 "sẽ được thu nhỏ bằng nearest-neighbor, giữ nguyên cạnh sắc. " +
                 "Để trống thì tô kín ô bằng Hint Color.")]
        [SerializeField] private Texture2D _hintPattern;

        [Tooltip("Số pixel mỗi ô trong texture gợi ý. Mỗi lần tô là một lần đẩy CẢ tấm " +
                 "texture lên GPU, nên số này càng lớn càng tốn băng thông. " +
                 "Lưới 64 ô: 4 cho ~256KB mỗi lần, 12 cho ~2.25MB. Lưới nhỏ thì nâng thoải mái.")]
        [SerializeField] private int _pixelsPerCell = 4;

        [Tooltip("Màu nhuộm lên hoạ tiết. Alpha vừa phải để còn thấy lớp màu bên dưới.")]
        [SerializeField] private Color32 _hintColor = new Color32(255, 255, 255, 140);

        private BoardView _boardView;
        private IPaintService _paintService;

        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;

        /// Hoạ tiết đã thu về đúng cỡ ô và nhuộm sẵn, dài PixelsPerCell * PixelsPerCell.
        private Color32[] _stamp;
        private int _cellPixels = 1;

        private bool _isTextureDirty;

        public void Init(BoardView boardView, IPaintService paintService)
        {
            _boardView = boardView;
            _paintService = paintService;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;

            if (_paintService != null)
            {
                _paintService.OnColorSelected -= HandleColorSelected;
                _paintService.OnCellPainted -= HandleCellPainted;
            }

            ReleaseTexture();
        }

        /// Gộp mọi thay đổi trong một frame thành một lần Apply.
        private void LateUpdate()
        {
            if (!_isTextureDirty) return;

            _isTextureDirty = false;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
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

            _cellPixels = Mathf.Max(1, _pixelsPerCell);
            BuildStamp();

            var width = grid.Width * _cellPixels;
            var height = grid.Height * _cellPixels;

            _pixels = new Color32[width * height];

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            // pixelsPerUnit = _cellPixels để sprite vẫn rộng đúng grid.Width world unit,
            // khớp chồng khít lên bảng dù texture mịn hơn.
            // FullRect, không để mặc định Tight. Tight dò alpha NGAY LÚC TẠO SPRITE rồi
            // đóng băng mesh ở đó — mà texture này lúc tạo còn TRONG SUỐT HOÀN TOÀN, nên
            // mesh không phủ gì cả và hoạ tiết đóng dấu sau đó có thể không bao giờ vẽ ra.
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                _cellPixels,
                0,
                SpriteMeshType.FullRect);

            _renderer.sprite = _sprite;
        }

        /// Thu hoạ tiết về đúng cỡ ô bằng nearest-neighbor và nhuộm sẵn theo Hint Color.
        /// Lấy mẫu điểm chứ không nội suy — hoạ tiết khối vuông giữ nguyên cạnh sắc.
        private void BuildStamp()
        {
            _stamp = new Color32[_cellPixels * _cellPixels];

            if (_hintPattern == null || !_hintPattern.isReadable)
            {
                if (_hintPattern != null)
                {
                    Debug.LogWarning(
                        $"'{_hintPattern.name}' chưa bật Read/Write Enabled trong Import Settings — " +
                        "tạm tô kín ô bằng Hint Color.");
                }

                for (var i = 0; i < _stamp.Length; i++) _stamp[i] = _hintColor;
                return;
            }

            var source = _hintPattern.GetPixels32();
            var sourceWidth = _hintPattern.width;
            var sourceHeight = _hintPattern.height;

            for (var y = 0; y < _cellPixels; y++)
            {
                for (var x = 0; x < _cellPixels; x++)
                {
                    var sourceX = Mathf.Min(sourceWidth - 1, x * sourceWidth / _cellPixels);
                    var sourceY = Mathf.Min(sourceHeight - 1, y * sourceHeight / _cellPixels);

                    var pixel = source[sourceY * sourceWidth + sourceX];

                    // Nhân hoạ tiết với màu gợi ý: ảnh trắng thì ra đúng Hint Color,
                    // vùng trong suốt của ảnh vẫn trong suốt.
                    _stamp[y * _cellPixels + x] = new Color32(
                        (byte)(pixel.r * _hintColor.r / 255),
                        (byte)(pixel.g * _hintColor.g / 255),
                        (byte)(pixel.b * _hintColor.b / 255),
                        (byte)(pixel.a * _hintColor.a / 255));
                }
            }
        }

        private void HandleColorSelected(int paletteIndex)
        {
            var grid = _boardView.Grid;
            if (grid == null || _pixels == null) return;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var isHinted = grid.GetCell(x, y) == paletteIndex && !_paintService.IsPainted(x, y);

                    if (isHinted) StampCell(grid, x, y);
                    else ClearCell(grid, x, y);
                }
            }

            _isTextureDirty = true;
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            var grid = _boardView.Grid;
            if (grid == null || _pixels == null) return;

            ClearCell(grid, cell.x, cell.y);
            _isTextureDirty = true;
        }

        private void StampCell(PixelGrid grid, int cellX, int cellY)
        {
            WriteBlock(grid, cellX, cellY, _stamp);
        }

        private void ClearCell(PixelGrid grid, int cellX, int cellY)
        {
            WriteBlock(grid, cellX, cellY, null);
        }

        /// block == null nghĩa là xoá về trong suốt.
        /// PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
        private void WriteBlock(PixelGrid grid, int cellX, int cellY, Color32[] block)
        {
            var textureWidth = grid.Width * _cellPixels;
            var originX = cellX * _cellPixels;
            var originY = (grid.Height - 1 - cellY) * _cellPixels;

            for (var y = 0; y < _cellPixels; y++)
            {
                var row = (originY + y) * textureWidth + originX;

                for (var x = 0; x < _cellPixels; x++)
                {
                    _pixels[row + x] = block != null ? block[y * _cellPixels + x] : Transparent;
                }
            }
        }

        private void ReleaseTexture()
        {
            _isTextureDirty = false;

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
