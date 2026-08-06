using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Dựng toàn bộ ô màu thành MỘT texture rồi gắn lên một SpriteRenderer.
    /// pixelsPerUnit = 1 nên một pixel là một ô là một world unit.
    ///
    /// Ô tô xong cũng chỉ là ghi lại một pixel — chi phí KHÔNG phụ thuộc số ô,
    /// nên lưới 64x64 (4096 ô) tốn đúng bằng lưới 16x16.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardView : MonoBehaviour
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Ô chưa tô hiện dạng xám. Ô tô đúng sẽ hiện màu thật. " +
                 "Tắt để đối chiếu với ảnh gốc.")]
        [SerializeField] private bool _grayscale = true;

        private ILevelService _levelService;

        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;
        private bool _isTextureDirty;

        public BoardLayout Layout { get; private set; }
        public PixelGrid Grid { get; private set; }
        public IReadOnlyList<Color32> Colors { get; private set; }

        /// Cấu hình màn đang chơi. BoardColorFade đọc mốc mờ từ đây — hai instance của
        /// nó không đi qua DI được (RegisterComponentInHierarchy chỉ tìm thấy một), nên
        /// lấy chung qua BoardView vốn đã có sẵn tham chiếu.
        public LevelConfig Config { get; private set; }

        public event Action OnBoardRebuilt;

        /// Bootstrap gọi. Chỉ đăng ký lắng nghe — bảng dựng khi màn chơi được nạp.
        public void Init(ILevelService levelService)
        {
            _levelService = levelService;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;

            ReleaseTexture();
        }

        /// Gộp mọi thay đổi trong một frame thành MỘT lần Apply.
        /// Kéo tay tô mười ô trong một frame vẫn chỉ upload texture một lần.
        private void LateUpdate()
        {
            if (!_isTextureDirty) return;

            _isTextureDirty = false;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private void HandleLevelStarted(int levelId) => Rebuild();

        /// Đổi ô từ xám sang màu thật.
        ///
        /// KHÔNG tự nghe OnCellPainted: ô phải đợi viên ngọc bay tới rồi mới đổi màu,
        /// nếu không màu hiện trước lúc hiệu ứng kết thúc và cú bay thành vô nghĩa.
        /// JewelFlyEffect gọi hàm này khi viên đáp xuống.
        public void RevealCell(Vector2Int cell, int paletteIndex)
        {
            if (Grid == null || Colors == null || _pixels == null) return;
            if (paletteIndex < 0 || paletteIndex >= Colors.Count) return;
            if (cell.x < 0 || cell.x >= Grid.Width || cell.y < 0 || cell.y >= Grid.Height) return;

            WritePixel(cell.x, cell.y, Colors[paletteIndex]);
            _isTextureDirty = true;
        }

        private void Rebuild()
        {
            ReleaseTexture();

            // Gán trước mọi nhánh thoát: kể cả khi lưới hỏng, các lớp khác vẫn cần biết
            // cấu hình của màn hiện tại.
            Config = _levelService.CurrentConfig;

            var data = _levelService.CurrentGrid;
            if (data == null)
            {
                ClearBoard("LevelConfig của màn này chưa gán GridData");
                return;
            }

            var grid = data.ToGrid();
            if (grid == null)
            {
                ClearBoard($"'{data.name}' chưa được tool sinh dữ liệu lưới");
                return;
            }

            var colors = data.Colors;
            if (colors.Count == 0)
            {
                ClearBoard($"'{data.name}' không có màu nào — sinh lại bằng tool");
                return;
            }

            Grid = grid;
            Colors = colors;
            Layout = new BoardLayout(grid.Width, grid.Height);

            BuildPixels();

            _texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, grid.Width, grid.Height),
                new Vector2(0.5f, 0.5f),
                1f);

            _renderer.sprite = _sprite;
            _isTextureDirty = false;

            OnBoardRebuilt?.Invoke();
        }

        private void BuildPixels()
        {
            _pixels = new Color32[Grid.Width * Grid.Height];
            var reportedOutOfRange = false;

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
                    var index = Grid.GetCell(x, y);
                    var color = Transparent;

                    if (index != PixelGrid.EmptyCell)
                    {
                        if (index >= 0 && index < Colors.Count)
                        {
                            color = _grayscale ? BoardColors.ToGrayscale(Colors[index]) : Colors[index];
                        }
                        else if (!reportedOutOfRange)
                        {
                            Debug.LogWarning(
                                $"Lưới có chỉ số màu {index} nhưng bảng màu chỉ có {Colors.Count} màu. " +
                                "Những ô đó vẽ trong suốt. Sinh lại lưới bằng tool để hết cảnh báo này.");
                            reportedOutOfRange = true;
                        }
                    }

                    WritePixel(x, y, color);
                }
            }
        }

        /// PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
        private void WritePixel(int x, int y, Color32 color)
        {
            _pixels[(Grid.Height - 1 - y) * Grid.Width + x] = color;
        }

        private void ClearBoard(string reason)
        {
            Debug.LogWarning($"Không dựng được bảng: {reason}");

            _renderer.sprite = null;
            Grid = null;
            Colors = null;
            Layout = null;
            _pixels = null;

            OnBoardRebuilt?.Invoke();
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
