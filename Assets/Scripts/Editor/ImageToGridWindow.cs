using System;
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

        private Vector2 _scroll;

        /// Bảng màu ngay lúc vừa rút từ ảnh, giữ nguyên không ai đụng vào.
        /// Chỉ để nút hoàn tác có chỗ mà quay về.
        private Color32[] _originalPalette;

        /// Chữ đang gõ trong ô hex của từng màu.
        ///
        /// Phải nhớ riêng chứ không sinh lại từ màu mỗi frame: gõ tới ký tự thứ ba thì
        /// chuỗi chưa hợp lệ, mà dựng lại từ màu sẽ xoá luôn thứ người ta đang gõ dở.
        private string[] _hexBuffers;

        /// Số ô của lưới dùng từng màu. Con số này là thứ quyết định màu nào đáng chỉnh:
        /// sửa một màu chỉ có 3 ô thì không ai nhận ra.
        private int[] _paletteCellCounts;

        private bool _paletteFoldout = true;

        /// Bảng màu và mấy mảng đi kèm đã khớp nhau chưa.
        ///
        /// Cần kiểm vì EditorWindow sống qua lần biên dịch lại, mà PixelGrid trong _result
        /// là class thuần C# nên không serialize được — sau reload các mảng có thể lệch.
        private bool HasPaletteState =>
            _result.IsValid
            && _originalPalette != null && _originalPalette.Length == _result.Palette.Length
            && _hexBuffers != null && _hexBuffers.Length == _result.Palette.Length
            && _paletteCellCounts != null && _paletteCellCounts.Length == _result.Palette.Length;

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
            // Bảng màu tối đa 64 dòng, không cuộn thì phần dưới cửa sổ không với tới được.
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawBody();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBody()
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
            DrawPaletteEditor();

            EditorGUILayout.Space();
            if (GUILayout.Button("Lưu thành asset", GUILayout.Height(28))) Save();
        }

        /// Sửa mã màu của từng màu trong bảng, ngay tại chỗ.
        ///
        /// Sửa màu KHÔNG tính lại lưới. Chỉ số trong lưới giữ nguyên, chỉ có màu mà chỉ số
        /// đó trỏ tới là đổi — nên mọi ô đang mang màu số 3 đổi theo cùng một lúc, và hình
        /// dạng bức tranh không suy chuyển. Đó là điều bạn muốn khi chỉnh tông màu, và cũng
        /// là lý do không được gọi lại Generate ở đây: sinh lại là bảng màu mới đè lên,
        /// mất sạch phần vừa chỉnh.
        private void DrawPaletteEditor()
        {
            if (!HasPaletteState) return;

            EditorGUILayout.Space();

            _paletteFoldout = EditorGUILayout.Foldout(
                _paletteFoldout, $"Sửa mã màu ({_result.Palette.Length} màu)", true);

            if (!_paletteFoldout) return;

            EditorGUILayout.LabelField(
                "Số bên trái là con số hiện trên ô màu trong game.", EditorStyles.miniLabel);

            for (var i = 0; i < _result.Palette.Length; i++) DrawPaletteRow(i);

            EditorGUILayout.Space(2f);

            using (new EditorGUI.DisabledScope(!HasPaletteEdits()))
            {
                if (GUILayout.Button("Hoàn tác về màu rút từ ảnh")) RevertPalette();
            }
        }

        private void DrawPaletteRow(int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // index + 1 vì lưới đánh số từ 0 còn ô màu trong game hiện từ 1.
                EditorGUILayout.LabelField($"{index + 1}", GUILayout.Width(24f));

                EditorGUI.BeginChangeCheck();

                var picked = EditorGUILayout.ColorField(
                    GUIContent.none, _result.Palette[index],
                    showEyedropper: true, showAlpha: false, hdr: false,
                    GUILayout.Width(64f));

                if (EditorGUI.EndChangeCheck()) ApplyColor(index, picked);

                EditorGUILayout.LabelField("#", GUILayout.Width(12f));

                EditorGUI.BeginChangeCheck();

                _hexBuffers[index] = EditorGUILayout.TextField(
                    _hexBuffers[index], GUILayout.Width(70f));

                if (EditorGUI.EndChangeCheck()) ApplyHex(index);

                EditorGUILayout.LabelField($"{_paletteCellCounts[index]} ô", GUILayout.Width(70f));
            }
        }

        private void ApplyColor(int index, Color color)
        {
            _result.Palette[index] = ToOpaque(color);
            _hexBuffers[index] = ColorUtility.ToHtmlStringRGB(color);

            RebuildPreview();
        }

        /// Gõ dở thì KHÔNG làm gì, không báo lỗi, không tự sửa chuỗi.
        ///
        /// Người ta gõ "1A2B3C" từng ký tự một, và bốn ký tự đầu đều là chuỗi không hợp lệ.
        /// Nhảy vào chỉnh hay cảnh báo ở đó là cướp bàn phím của người dùng.
        private void ApplyHex(int index)
        {
            var text = _hexBuffers[index];
            if (string.IsNullOrEmpty(text)) return;

            if (text[0] != '#') text = "#" + text;

            if (!ColorUtility.TryParseHtmlString(text, out var parsed)) return;

            _result.Palette[index] = ToOpaque(parsed);

            RebuildPreview();
        }

        /// Bảng màu luôn đục. Ô trong suốt được ghi bằng PixelGrid.EmptyCell chứ không
        /// bằng một màu alpha 0, nên một màu bảng có alpha < 255 chỉ là lỗi chờ xảy ra.
        private static Color32 ToOpaque(Color color)
        {
            var value = (Color32)color;
            value.a = byte.MaxValue;

            return value;
        }

        private bool HasPaletteEdits()
        {
            for (var i = 0; i < _result.Palette.Length; i++)
            {
                var a = _result.Palette[i];
                var b = _originalPalette[i];

                if (a.r != b.r || a.g != b.g || a.b != b.b) return true;
            }

            return false;
        }

        private void RevertPalette()
        {
            Array.Copy(_originalPalette, _result.Palette, _originalPalette.Length);

            ResetHexBuffers();
            RebuildPreview();

            SetMessage("Đã trả bảng màu về đúng thứ rút từ ảnh.", MessageType.Info);
        }

        /// Chụp lại bảng màu gốc và dựng các mảng đi kèm. Gọi ngay sau mỗi lần sinh lưới.
        private void CapturePaletteState()
        {
            var palette = _result.Palette;

            _originalPalette = new Color32[palette.Length];
            Array.Copy(palette, _originalPalette, palette.Length);

            _hexBuffers = new string[palette.Length];
            ResetHexBuffers();

            _paletteCellCounts = new int[palette.Length];

            var grid = _result.Grid;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index < 0 || index >= _paletteCellCounts.Length) continue;

                    _paletteCellCounts[index]++;
                }
            }
        }

        private void ResetHexBuffers()
        {
            for (var i = 0; i < _result.Palette.Length; i++)
            {
                _hexBuffers[i] = ColorUtility.ToHtmlStringRGB(_result.Palette[i]);
            }
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

            // Read/Write, Compression và npotScale thì tool tự sửa được. Max Size thì không:
            // nâng nó lên là quyết định về bộ nhớ của cả project, không phải thứ một tool
            // sinh lưới được tự tiện đổi hộ.
            if (ImageToGridGenerator.IsSizeClamped(_sourceTexture))
            {
                EditorGUILayout.HelpBox(
                    $"Ảnh đang là {_sourceTexture.width} x {_sourceTexture.height}, đúng bằng " +
                    "Max Size trong Import Settings — nhiều khả năng nó đã bị THU NHỎ lúc import. " +
                    "Bản thu nhỏ có mép nhoè và màu pha, và lưới sinh ra từ đó sẽ xỉn màu. " +
                    "Nâng Max Size lên cho vừa ảnh gốc rồi sinh lại.",
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

                CapturePaletteState();
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

            // Dựng cả mảng rồi đẩy một lần, không SetPixel từng ô: hàm này chạy lại sau
            // MỖI ký tự gõ vào ô hex, mà lưới 256x256 là 65 nghìn lời gọi.
            var pixels = new Color32[grid.Width * grid.Height];

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    var color = index == PixelGrid.EmptyCell
                        ? new Color32(0, 0, 0, 0)
                        : palette[index];

                    // Texture2D có y = 0 ở dưới cùng, PixelGrid có y = 0 ở trên cùng
                    pixels[(grid.Height - 1 - y) * grid.Width + x] = color;
                }
            }

            _previewTexture.SetPixels32(pixels);
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
