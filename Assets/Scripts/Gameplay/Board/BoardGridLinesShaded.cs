using JewelPainter.Gameplay.Domain;
using Unity.Collections;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kẻ viền quanh từng ô có màu bằng shader, thay cho BoardGridLines vốn nướng sẵn nét vào một texture cỡ cả bảng.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardGridLinesShaded : MonoBehaviour, IBoardGridLines
    {
        private const int Padding = 1;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidthPixels");
        private static readonly int SoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int ReferenceHeightId = Shader.PropertyToID("_ReferenceScreenHeight");

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Shader JewelPainter/Board Grid Lines.")]
        [SerializeField] private Shader _shader;

        [Tooltip("Bề dày nét tính bằng pixel màn hình.")]
        [Range(0.5f, 8f)]
        [SerializeField] private float _lineWidthPixels = 1.5f;

        [Tooltip("Bề rộng dải chuyển ở mép nét, tính bằng pixel.")]
        [Range(0f, 3f)]
        [SerializeField] private float _edgeSoftness = 1f;

        [Tooltip("Chiều cao màn hình mà Line Width Pixels được canh theo.")]
        [SerializeField] private float _referenceScreenHeight = 1080f;

        [Tooltip("Màu nét.")]
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
        /// Áp lại thông số khi chỉnh trong Inspector.
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

        /// Dựng texture mask, mỗi ô một texel.
        private void BuildMask(PixelGrid grid)
        {
            var width = grid.Width + Padding * 2;
            var height = grid.Height + Padding * 2;

            _mask = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BoardGridLinesMask",
            };

            var pixels = new NativeArray<byte>(
                width * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                for (var y = 0; y < grid.Height; y++)
                {
                    var row = (grid.Height - 1 - y + Padding) * width;

                    for (var x = 0; x < grid.Width; x++)
                    {
                        if (grid.GetCell(x, y) == PixelGrid.EmptyCell) continue;

                        pixels[row + x + Padding] = 255;
                    }
                }

                _mask.SetPixelData(pixels, 0);

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
