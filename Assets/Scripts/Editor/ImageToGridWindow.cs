using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ chuyển ảnh thành lưới ô màu.
    /// Chỉ là lớp vỏ: thu thập input, gọi ImageToGridGenerator, hiện preview, ghi asset.
    public class ImageToGridWindow : EditorWindow
    {
        private const int MinCells = 1;
        private const int MaxCells = 256;
        private const int MinColors = 2;
        private const int MaxColors = 64;
        private const float MaxMergeDistance = 120f;
        private const int PreviewMaxSize = 320;

        private Texture2D _sourceTexture;
        private int _gridWidth = 32;
        private int _gridHeight = 32;
        private int _maxColors = 32;
        private float _mergeDistance = 24f;

        private GridGenerationResult _result;
        private Texture2D _previewTexture;
        private string _message;
        private MessageType _messageType = MessageType.None;

        [MenuItem("JewelPainter/Ảnh thành lưới ô")]
        public static void Open()
        {
            var window = GetWindow<ImageToGridWindow>();
            window.titleContent = new GUIContent("Ảnh thành lưới ô");
            window.minSize = new Vector2(380, 520);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Nguồn", EditorStyles.boldLabel);

            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Ảnh", _sourceTexture, typeof(Texture2D), false);

            _gridWidth = EditorGUILayout.IntSlider("Số ô ngang", _gridWidth, MinCells, MaxCells);
            _gridHeight = EditorGUILayout.IntSlider("Số ô dọc", _gridHeight, MinCells, MaxCells);

            DrawFitAspectButton();

            _maxColors = EditorGUILayout.IntSlider(
                "Số màu tối đa", _maxColors, MinColors, MaxColors);

            _mergeDistance = EditorGUILayout.Slider(
                "Gộp màu gần giống", _mergeDistance, 0f, MaxMergeDistance);

            EditorGUILayout.HelpBox(
                "Màu rút thẳng từ ảnh. Ảnh ít màu hơn số tối đa thì bảng màu tự ngắn lại.\n" +
                "Gộp màu: 0 là không gộp, 20 gộp các sắc độ rất sát, 60 gộp mạnh tay. " +
                "Số màu thật sự dùng hiện ở phần Kết quả bên dưới.",
                MessageType.None);

            DrawSizePreview();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_sourceTexture == null))
            {
                if (GUILayout.Button("Sinh lưới", GUILayout.Height(28))) Generate();
            }

            if (_sourceTexture == null)
            {
                EditorGUILayout.HelpBox("Chọn ảnh trước khi sinh.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _messageType);
            }

            if (!_result.IsValid) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kết quả", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Kích thước lưới",
                $"{_result.Grid.Width} x {_result.Grid.Height} ({_result.Grid.Width * _result.Grid.Height} ô)");
            EditorGUILayout.LabelField("Số màu rút được", _result.Palette.Length.ToString());

            DrawPreview();
            DrawPaletteStrip();

            EditorGUILayout.Space();
            if (GUILayout.Button("Lưu thành asset", GUILayout.Height(28))) Save();
        }

        /// Ghi đè hai ô nhập bằng kích thước giữ đúng tỉ lệ ảnh, lấy cạnh dài hiện tại
        /// làm mốc. Để người dùng có điểm khởi đầu hợp lý rồi tự tinh chỉnh.
        private void DrawFitAspectButton()
        {
            using (new EditorGUI.DisabledScope(_sourceTexture == null))
            {
                if (!GUILayout.Button("Theo tỉ lệ ảnh")) return;
            }

            if (_sourceTexture == null) return;

            var longest = Mathf.Max(_gridWidth, _gridHeight);
            var size = ImageToGridGenerator.CalculateGridSize(
                _sourceTexture.width, _sourceTexture.height, longest);

            _gridWidth = size.x;
            _gridHeight = size.y;
        }

        private void DrawSizePreview()
        {
            if (_sourceTexture == null) return;

            var imageAspect = (float)_sourceTexture.width / _sourceTexture.height;
            var gridAspect = (float)_gridWidth / _gridHeight;

            if (Mathf.Abs(imageAspect - gridAspect) > 0.01f)
            {
                EditorGUILayout.HelpBox(
                    "Tỉ lệ lưới khác tỉ lệ ảnh nên hình sẽ bị kéo méo. " +
                    "Bấm \"Theo tỉ lệ ảnh\" nếu không cố ý.",
                    MessageType.Warning);
            }

            if (_gridWidth > _sourceTexture.width || _gridHeight > _sourceTexture.height)
            {
                EditorGUILayout.HelpBox(
                    $"Lưới ({_gridWidth} x {_gridHeight}) mịn hơn ảnh " +
                    $"({_sourceTexture.width} x {_sourceTexture.height} pixel), " +
                    "nên nhiều ô cạnh nhau sẽ trùng màu.",
                    MessageType.Warning);
            }
        }

        private void Generate()
        {
            try
            {
                _result = ImageToGridGenerator.Generate(
                    _sourceTexture, _gridWidth, _gridHeight, _maxColors, _mergeDistance);

                if (!_result.IsValid)
                {
                    ClearPreview();
                    SetMessage("Ảnh không có pixel đục nào — lưới rỗng hoàn toàn.", MessageType.Warning);
                    return;
                }

                RebuildPreview();
                SetMessage(
                    $"Đã sinh lưới {_result.Grid.Width} x {_result.Grid.Height} với {_result.Palette.Length} màu.",
                    MessageType.Info);
            }
            catch (System.Exception exception)
            {
                _result = default;
                ClearPreview();
                SetMessage(exception.Message, MessageType.Error);
            }
        }

        private void RebuildPreview()
        {
            ClearPreview();

            var grid = _result.Grid;
            var palette = _result.Palette;

            _previewTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    var color = index == PixelGrid.EmptyCell
                        ? new Color32(0, 0, 0, 0)
                        : palette[index];

                    // Texture2D có y = 0 ở dưới cùng, PixelGrid có y = 0 ở trên cùng
                    _previewTexture.SetPixel(x, grid.Height - 1 - y, color);
                }
            }

            _previewTexture.Apply();
        }

        private void DrawPreview()
        {
            if (_previewTexture == null) return;

            var scale = Mathf.Min(
                PreviewMaxSize / (float)_previewTexture.width,
                PreviewMaxSize / (float)_previewTexture.height);

            var rect = GUILayoutUtility.GetRect(
                _previewTexture.width * scale,
                _previewTexture.height * scale,
                GUILayout.ExpandWidth(false));

            EditorGUI.DrawTextureTransparent(rect, _previewTexture, ScaleMode.ScaleToFit);
        }

        /// Dải màu rút được, để mắt thường kiểm xem tool có bắt đúng tông ảnh không.
        private void DrawPaletteStrip()
        {
            var palette = _result.Palette;
            var rect = GUILayoutUtility.GetRect(PreviewMaxSize, 24f, GUILayout.ExpandWidth(false));
            var swatchWidth = rect.width / palette.Length;

            for (var i = 0; i < palette.Length; i++)
            {
                var swatch = new Rect(rect.x + i * swatchWidth, rect.y, swatchWidth, rect.height);
                EditorGUI.DrawRect(swatch, palette[i]);
            }
        }

        private void Save()
        {
            var defaultName = _sourceTexture != null ? $"{_sourceTexture.name}GridData" : "LevelGridData";

            var path = EditorUtility.SaveFilePanelInProject(
                "Lưu dữ liệu lưới", defaultName, "asset", "Chọn nơi lưu asset");

            if (string.IsNullOrEmpty(path)) return;

            var data = CreateInstance<LevelGridData>();
            data.SetData(_result.Grid.Width, _result.Grid.Height, _result.Palette, _result.Grid.ToArray());

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(data);
            SetMessage($"Đã lưu vào {path}", MessageType.Info);
        }

        private void SetMessage(string message, MessageType type)
        {
            _message = message;
            _messageType = type;
        }

        private void ClearPreview()
        {
            if (_previewTexture == null) return;

            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private void OnDisable()
        {
            ClearPreview();
        }
    }
}
