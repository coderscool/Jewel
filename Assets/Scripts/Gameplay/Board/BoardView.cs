using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

namespace JewelPainter.Gameplay.Board
{
    /// Dựng toàn bộ ô màu thành texture rồi gắn lên SpriteRenderer.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardView : MonoBehaviour
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        [Tooltip("Renderer lớp ô chưa tô.")]
        [FormerlySerializedAs("_renderer")]
        [SerializeField] private SpriteRenderer _unpaintedRenderer;

        [Tooltip("Renderer lớp ô đã tô.")]
        [SerializeField] private SpriteRenderer _paintedRenderer;

        [Tooltip("Ô chưa tô hiện dạng xám.")]
        [SerializeField] private bool _grayscale = true;

        [Tooltip("Mỗi ô chiếm bao nhiêu texel vuông trong texture bảng.")]
        [Range(1, 8)]
        [SerializeField] private int _texelsPerCell = 4;

        [Tooltip("Sinh mipmap cho hai texture bảng.")]
        [SerializeField] private bool _generateMipmaps = true;

        private ILevelService _levelService;
        private IPaintService _paintService;

        private Texture2D _unpaintedTexture;
        private Texture2D _paintedTexture;
        private Sprite _unpaintedSprite;
        private Sprite _paintedSprite;
        private Color32[] _unpaintedPixels;
        private Color32[] _paintedPixels;

        private bool _isTextureDirty;

        private readonly List<Vector2Int> _dirtyCells = new();
        private readonly HashSet<Vector2Int> _dirtyCellLookup = new();

        private bool _needsFullUpload;

        private Color32[] _cellBlock;

        private const int FullUploadCellThreshold = 192;

        public BoardLayout Layout { get; private set; }
        public PixelGrid Grid { get; private set; }
        public IReadOnlyList<Color32> Colors { get; private set; }

        public IReadOnlyList<Color32> JewelColors { get; private set; }

        public LevelConfig Config { get; private set; }

        public event Action OnBoardRebuilt;

        public bool IsCovered { get; private set; }

        public event Action OnCoverChanged;

        /// Báo bảng đang bị màn hình khác che hoặc không.
        public void SetCovered(bool covered)
        {
            if (IsCovered == covered) return;

            IsCovered = covered;

            OnCoverChanged?.Invoke();
        }

        /// Khởi tạo phụ thuộc.
        public void Init(ILevelService levelService, IPaintService paintService)
        {
            _levelService = levelService;
            _paintService = paintService;

            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;

            ReleaseTextures();
        }

        /// Gộp mọi thay đổi trong một frame thành một lần Apply cho mỗi lớp.
        private void LateUpdate()
        {
            if (!_isTextureDirty) return;

            _isTextureDirty = false;

            UploadDirtyCells();
        }

        private void HandleLevelStarted(int levelId) => Rebuild();

        /// Chuyển một ô từ lớp chưa tô sang lớp đã tô.
        public void RevealCell(Vector2Int cell, int paletteIndex)
        {
            if (Grid == null || Colors == null || _paintedPixels == null) return;
            if (paletteIndex < 0 || paletteIndex >= Colors.Count) return;
            if (cell.x < 0 || cell.x >= Grid.Width || cell.y < 0 || cell.y >= Grid.Height) return;

            WritePixel(_paintedPixels, cell.x, cell.y, Colors[paletteIndex]);

            WritePixel(_unpaintedPixels, cell.x, cell.y, Transparent);

            MarkDirty(cell);
        }

        /// Ghi sổ ô vừa đổi để lần upload kế tiếp biết cần đẩy đúng chỗ nào.
        private void MarkDirty(Vector2Int cell)
        {
            _isTextureDirty = true;

            if (_needsFullUpload) return;

            if (_dirtyCells.Count >= FullUploadCellThreshold)
            {
                _needsFullUpload = true;
                ClearDirtyCells();
                return;
            }

            if (_dirtyCellLookup.Add(cell)) _dirtyCells.Add(cell);
        }

        /// Đẩy lên GPU đúng những ô vừa đổi, thay vì đẩy trọn hai texture.
        private void UploadDirtyCells()
        {
            if (_needsFullUpload || _cellBlock == null)
            {
                _unpaintedTexture.SetPixels32(_unpaintedPixels);
                _paintedTexture.SetPixels32(_paintedPixels);
            }
            else
            {
                for (var i = 0; i < _dirtyCells.Count; i++)
                {
                    var cell = _dirtyCells[i];

                    UploadCell(_unpaintedTexture, _unpaintedPixels, cell.x, cell.y);
                    UploadCell(_paintedTexture, _paintedPixels, cell.x, cell.y);
                }
            }

            _unpaintedTexture.Apply(_generateMipmaps);
            _paintedTexture.Apply(_generateMipmaps);

            _needsFullUpload = false;
            ClearDirtyCells();
        }

        /// Rút khối texel của một ô ra khỏi mảng pixel của cả bảng rồi đẩy đúng khối đó.
        private void UploadCell(Texture2D texture, Color32[] pixels, int x, int y)
        {
            var origin = TexelIndexOf(x, y);
            var width = TextureWidth;

            for (var row = 0; row < _texelsPerCell; row++)
            {
                Array.Copy(pixels, origin + row * width, _cellBlock, row * _texelsPerCell, _texelsPerCell);
            }

            texture.SetPixels32(
                x * _texelsPerCell,
                (Grid.Height - 1 - y) * _texelsPerCell,
                _texelsPerCell,
                _texelsPerCell,
                _cellBlock);
        }

        private void ClearDirtyCells()
        {
            _dirtyCells.Clear();
            _dirtyCellLookup.Clear();
        }

        private void Rebuild()
        {
            ReleaseTextures();

            Config = _levelService.CurrentConfig;

            if (Config == null)
            {
                ClearBoard($"không có LevelConfig nào mang Level Id = {_levelService.CurrentLevel}. " +
                           "Kiểm tra mảng Levels của LevelManager, hoặc tiến trình đã lưu đang " +
                           "trỏ tới một màn chưa tồn tại.");
                return;
            }

            var data = _levelService.CurrentGrid;
            if (data == null)
            {
                ClearBoard($"'{Config.name}' (Level Id = {Config.LevelId}) chưa gán Grid Data");
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

            if (_paintedRenderer == null)
            {
                ClearBoard($"{nameof(BoardView)} chưa gán Painted Renderer");
                return;
            }

            Grid = grid;
            Colors = colors;
            JewelColors = _levelService.CurrentJewelColors;
            Layout = new BoardLayout(grid.Width, grid.Height);

            BuildPixels();

            _unpaintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _unpaintedPixels, _generateMipmaps);
            _paintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _paintedPixels, _generateMipmaps);

            _unpaintedSprite = CreateSprite(_unpaintedTexture);
            _paintedSprite = CreateSprite(_paintedTexture);

            _unpaintedRenderer.sprite = _unpaintedSprite;
            _paintedRenderer.sprite = _paintedSprite;

            WarnOnLayerMisalignment();

            _isTextureDirty = false;

            OnBoardRebuilt?.Invoke();
        }

        private void BuildPixels()
        {
            var count = Grid.Width * Grid.Height * _texelsPerCell * _texelsPerCell;

            _unpaintedPixels = new Color32[count];
            _paintedPixels = new Color32[count];

            _cellBlock = new Color32[_texelsPerCell * _texelsPerCell];

            var reportedOutOfRange = false;

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
                    var index = Grid.GetCell(x, y);

                    var unpaintedColor = Transparent;
                    var paintedColor = Transparent;

                    if (index != PixelGrid.EmptyCell)
                    {
                        if (index >= 0 && index < Colors.Count)
                        {
                            if (_paintService != null && _paintService.IsPainted(x, y))
                            {
                                paintedColor = Colors[index];
                            }
                            else
                            {
                                unpaintedColor = _grayscale
                                    ? BoardColors.ToGrayscale(Colors[index])
                                    : Colors[index];
                            }
                        }
                        else if (!reportedOutOfRange)
                        {
                            Debug.LogWarning(
                                $"Lưới có chỉ số màu {index} nhưng bảng màu chỉ có {Colors.Count} màu. " +
                                "Những ô đó vẽ trong suốt. Sinh lại lưới bằng tool để hết cảnh báo này.");
                            reportedOutOfRange = true;
                        }
                    }

                    WritePixel(_unpaintedPixels, x, y, unpaintedColor);
                    WritePixel(_paintedPixels, x, y, paintedColor);
                }
            }
        }

        /// Cảnh báo khi các lớp texture lệch khỏi vùng của BoardLayout.
        private void WarnOnLayerMisalignment()
        {
            var expected = Layout.WorldBounds;

            WarnIfBoundsWrong(_unpaintedRenderer, expected, "chưa tô");
            WarnIfBoundsWrong(_paintedRenderer, expected, "đã tô");
        }

        private static void WarnIfBoundsWrong(SpriteRenderer renderer, Bounds expected, string label)
        {
            if (renderer == null || renderer.sprite == null) return;

            var actual = renderer.bounds;

            var offset = (Vector2)(actual.center - expected.center);
            var sizeError = (Vector2)(actual.size - expected.size);

            if (offset.sqrMagnitude < 0.0001f && sizeError.sqrMagnitude < 0.0001f) return;

            Debug.LogWarning(
                $"Lớp bảng '{label}' ({renderer.name}) KHÔNG trùng lưới ô: lệch tâm {offset}, " +
                $"chênh kích thước {sizeError}. Bảng phải phủ đúng {expected.size.x} x " +
                $"{expected.size.y} world unit và căn giữa gốc toạ độ.\n" +
                "Kiểm theo thứ tự: Position của chính nó, rồi Position và Scale của MỌI " +
                "object cha — một cái cha lệch thôi là cả hai lớp cùng trượt, và phép so " +
                "hai lớp với nhau sẽ không phát hiện ra.",
                renderer);
        }

        /// Tạo texture cho một lớp của bảng.
        private static Texture2D CreateTexture(int width, int height, Color32[] pixels, bool mipChain)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels32(pixels);
            texture.Apply(mipChain);

            return texture;
        }

        /// Tạo sprite cho một lớp của bảng.
        private Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                _texelsPerCell,
                0,
                SpriteMeshType.FullRect);
        }

        private int TextureWidth => Grid.Width * _texelsPerCell;

        /// Chỉ số texel ở góc dưới trái của một ô, trong mảng pixel.
        private int TexelIndexOf(int x, int y)
        {
            return (Grid.Height - 1 - y) * _texelsPerCell * TextureWidth + x * _texelsPerCell;
        }

        /// Tô cả khối texel của một ô, không phải một texel.
        private void WritePixel(Color32[] pixels, int x, int y, Color32 color)
        {
            var origin = TexelIndexOf(x, y);
            var width = TextureWidth;

            for (var row = 0; row < _texelsPerCell; row++)
            {
                var start = origin + row * width;

                for (var column = 0; column < _texelsPerCell; column++)
                {
                    pixels[start + column] = color;
                }
            }
        }

        private void ClearBoard(string reason)
        {
            Debug.LogWarning($"Không dựng được bảng: {reason}");

            _unpaintedRenderer.sprite = null;
            if (_paintedRenderer != null) _paintedRenderer.sprite = null;

            Grid = null;
            Colors = null;
            JewelColors = null;
            Layout = null;
            _unpaintedPixels = null;
            _paintedPixels = null;

            OnBoardRebuilt?.Invoke();
        }

        private void ReleaseTextures()
        {
            _isTextureDirty = false;

            _needsFullUpload = false;
            ClearDirtyCells();

            DestroyIfAlive(ref _unpaintedSprite);
            DestroyIfAlive(ref _paintedSprite);
            DestroyIfAlive(ref _unpaintedTexture);
            DestroyIfAlive(ref _paintedTexture);
        }

        private void DestroyIfAlive<T>(ref T asset) where T : UnityEngine.Object
        {
            if (asset == null) return;

            Destroy(asset);
            asset = null;
        }
    }
}
