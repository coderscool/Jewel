using System.Collections.Generic;
using System.IO;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ chỉnh màu: xem trước ô đất và viên ngọc nằm trên nó, mỗi bên một bộ núm.
    public class JewelColorTunerWindow : EditorWindow
    {
        private const int PreviewCell = 84;
        private const int PreviewColumns = 6;

        private const int PreviewResolution = 96;

        private const float ParamSaturationMin = JewelFacetMap.SaturationMin;
        private const float ParamSaturationMax = JewelFacetMap.SaturationMax;

        private const int BakeSupersample = 4;
        private const int PreviewSupersample = 2;
        private const int BakeResolution = 256;

        /// Đảm bảo mảng mặt cắt đủ độ dài.
        private const float HighlightFloor = 0.5f;
        private const string ConfigFolder = "Assets/Scriptables";

        private const string SaturationProperty = "_Saturation";
        private const string ContrastProperty = "_Contrast";
        private const string BrightnessProperty = "_Brightness";
        private const string FacetStrengthProperty = "_FacetStrength";
        private const string HighlightWhiteProperty = "_HighlightWhite";
        private const string DepthProperty = "_Depth";
        private const string DarkLiftProperty = "_DarkLift";
        private const string ParamTexProperty = "_ParamTex";

        private SpriteRenderer _jewelPrefab;

        private Material _jewelMaterial;
        private Sprite _jewelSprite;
        private Texture _paramTexture;

        private ColorAdjustment[] _facetParams;
        private byte[] _facetAlpha;

        private Texture _loadedParamTexture;

        private readonly Dictionary<int, Texture2D> _previewCache = new Dictionary<int, Texture2D>();
        private int _previewStateHash;

        private Material _loadedMaterial;

        private float _facetStrength = 1f;
        private float _highlightWhite = 1f;
        private float _depth;
        private float _darkLift = 0.35f;

        private JewelFacetProfile _facetProfile;
        private JewelFacetProfile _loadedProfile;
        private bool _showFacets;
        private LevelGridData _gridData;
        private JewelTintConfig _tintConfig;

        private JewelTintConfig _loadedConfig;

        private readonly List<Color32> _customColors = new List<Color32>
        {
            new Color32(0xEA, 0x89, 0x1E, 255),
        };

        private string _hexInput = "#EA891E";

        private ColorAdjustment _ground = ColorAdjustment.None;
        private ColorAdjustment _jewel = ColorAdjustment.None;

        private bool _showGround = true;
        private Vector2 _scroll;

        [MenuItem("JewelPainter/Chỉnh màu viên ngọc")]
        public static void Open()
        {
            var window = GetWindow<JewelColorTunerWindow>();
            window.titleContent = new GUIContent("Chỉnh màu ngọc");
            window.minSize = new Vector2(560f, 520f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSourceSection();
            EditorGUILayout.Space();

            if (_gridData == null)
            {
                DrawCustomColorSection();
                EditorGUILayout.Space();
            }

            DrawSliderSection();
            EditorGUILayout.Space();

            DrawFacetSection();
            RefreshPreviewCache();
            EditorGUILayout.Space();

            DrawPreviewSection();
            EditorGUILayout.Space();

            DrawHexSection();
            EditorGUILayout.Space();

            DrawApplySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Nguồn", EditorStyles.boldLabel);

            _jewelPrefab = (SpriteRenderer)EditorGUILayout.ObjectField(
                new GUIContent("Prefab viên ngọc", "Prefab gán vào JewelLayer. Sprite, " +
                    "material và ảnh mặt cắt đều lấy từ đây. Để trống thì chỉ xem màu đất."),
                _jewelPrefab, typeof(SpriteRenderer), false);

            ResolveFromPrefab();

            _gridData = (LevelGridData)EditorGUILayout.ObjectField(
                new GUIContent("Grid Data", "Lấy bảng màu thật của một màn. Để trống thì dùng bộ màu mẫu."),
                _gridData, typeof(LevelGridData), false);

            _facetProfile = (JewelFacetProfile)EditorGUILayout.ObjectField(
                new GUIContent("Bộ số mặt cắt", "Asset chứa bộ số của từng mặt. Có nó thì " +
                    "chỉnh được riêng chín mặt và nướng lại ảnh tham số."),
                _facetProfile, typeof(JewelFacetProfile), false);

            _tintConfig = (JewelTintConfig)EditorGUILayout.ObjectField(
                new GUIContent("Jewel Tint Config", "Asset chứa bộ số của viên ngọc, dùng chung cả game."),
                _tintConfig, typeof(JewelTintConfig), false);

            SyncFromMaterial();
            SyncFromConfig();

            LoadParamMap();

            if (_jewelPrefab != null && _jewelMaterial == null)
            {
                EditorGUILayout.HelpBox(
                    "Prefab chưa có material riêng — đang dùng material mặc định nên " +
                    "không ghi bộ số vào đâu được.", MessageType.Warning);
            }
            else if (_jewelMaterial != null && !HasTintProperties(_jewelMaterial))
            {
                EditorGUILayout.HelpBox(
                    $"Material '{_jewelMaterial.name}' không dùng shader " +
                    "JewelPainter/Jewel Facets nên không có ba núm này. Núm bên dưới " +
                    "vẫn xem trước được, nhưng chỉ ghi vào Jewel Tint Config.",
                    MessageType.Info);
            }

            if (_gridData == null)
            {
                EditorGUILayout.HelpBox(
                    "Đang dùng bảng màu tự điền bên dưới. Kéo một LevelGridData vào để " +
                    "chỉnh trên đúng bảng màu của màn đó.", MessageType.None);
            }

            if (_tintConfig == null && !HasTintProperties(_jewelMaterial))
            {
                EditorGUILayout.HelpBox(
                    "Chưa có chỗ nào để ghi bộ số viên ngọc — núm bên dưới chỉ xem trước. " +
                    "Kéo prefab dùng shader Jewel Facets vào ô trên, hoặc tạo một " +
                    "Jewel Tint Config.", MessageType.Info);

                if (GUILayout.Button("Tạo Jewel Tint Config mới")) CreateConfig();
            }
        }

        /// Rút sprite, material và ảnh mặt cắt ra khỏi prefab.
        private void ResolveFromPrefab()
        {
            _jewelMaterial = _jewelPrefab != null ? _jewelPrefab.sharedMaterial : null;
            _jewelSprite = _jewelPrefab != null ? _jewelPrefab.sprite : null;

            _paramTexture = _jewelMaterial != null && _jewelMaterial.HasProperty(ParamTexProperty)
                ? _jewelMaterial.GetTexture(ParamTexProperty)
                : null;

            LoadParamMap();
        }

        /// Sinh mảng tham số thẳng từ asset bộ số, không qua ảnh.
        private void RebuildFromProfile()
        {
            _loadedParamTexture = null;
            _loadedProfile = _facetProfile;
            ClearPreviewCache();

            if (_facetProfile == null)
            {
                _facetParams = null;
                _facetAlpha = null;
                return;
            }

            var pixels = JewelFacetMap.Build(_facetProfile, PreviewResolution, PreviewSupersample);

            _facetParams = new ColorAdjustment[pixels.Length];
            _facetAlpha = new byte[pixels.Length];

            for (var i = 0; i < pixels.Length; i++)
            {
                _facetParams[i] = Decode(pixels[i]);
                _facetAlpha[i] = pixels[i].a;
            }
        }

        private static ColorAdjustment Decode(Color32 pixel)
        {
            return new ColorAdjustment(
                Mathf.Lerp(ParamSaturationMin, ParamSaturationMax, pixel.r / 255f),
                pixel.g / 255f * 2f - 1f,
                pixel.b / 255f - 0.5f);
        }

        /// Giải mã ảnh tham số từ file PNG vào mảng ColorAdjustment.
        private void LoadParamMap()
        {
            if (_facetProfile != null)
            {
                if (_facetProfile != _loadedProfile) RebuildFromProfile();
                return;
            }

            if (_paramTexture == _loadedParamTexture) return;

            _loadedParamTexture = _paramTexture;
            _loadedProfile = null;
            _facetParams = null;
            _facetAlpha = null;
            ClearPreviewCache();

            if (_paramTexture == null) return;

            var path = AssetDatabase.GetAssetPath(_paramTexture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (!decoded.LoadImage(File.ReadAllBytes(path)))
            {
                DestroyImmediate(decoded);
                return;
            }

            var pixels = decoded.GetPixels32();
            var width = decoded.width;
            var height = decoded.height;

            _facetParams = new ColorAdjustment[PreviewResolution * PreviewResolution];
            _facetAlpha = new byte[PreviewResolution * PreviewResolution];

            for (var y = 0; y < PreviewResolution; y++)
            {
                var sourceY = Mathf.Min(height - 1, y * height / PreviewResolution);

                for (var x = 0; x < PreviewResolution; x++)
                {
                    var sourceX = Mathf.Min(width - 1, x * width / PreviewResolution);
                    var pixel = pixels[sourceY * width + sourceX];
                    var index = y * PreviewResolution + x;

                    _facetParams[index] = Decode(pixel);
                    _facetAlpha[index] = pixel.a;
                }
            }

            DestroyImmediate(decoded);
        }

        /// Xoá cache xem trước khi bộ số đổi.
        private void RefreshPreviewCache()
        {
            var hash = _jewel.Saturation.GetHashCode();
            hash = hash * 397 ^ _jewel.Contrast.GetHashCode();
            hash = hash * 397 ^ _jewel.Brightness.GetHashCode();
            hash = hash * 397 ^ _facetStrength.GetHashCode();
            hash = hash * 397 ^ _highlightWhite.GetHashCode();
            hash = hash * 397 ^ _depth.GetHashCode();
            hash = hash * 397 ^ _darkLift.GetHashCode();

            if (hash == _previewStateHash) return;

            _previewStateHash = hash;
            ClearPreviewCache();
        }

        private void ClearPreviewCache()
        {
            foreach (var pair in _previewCache)
            {
                if (pair.Value != null) DestroyImmediate(pair.Value);
            }

            _previewCache.Clear();
        }

        private void OnDisable()
        {
            ClearPreviewCache();
        }

        private static bool HasTintProperties(Material material)
        {
            return material != null &&
                   material.HasProperty(SaturationProperty) &&
                   material.HasProperty(ContrastProperty) &&
                   material.HasProperty(BrightnessProperty);
        }

        /// Nạp số từ material vào núm, chỉ khi vừa đổi sang một material khác.
        private void SyncFromMaterial()
        {
            if (_jewelMaterial == _loadedMaterial) return;

            _loadedMaterial = _jewelMaterial;

            if (!HasTintProperties(_jewelMaterial)) return;

            _jewel = new ColorAdjustment(
                _jewelMaterial.GetFloat(SaturationProperty),
                _jewelMaterial.GetFloat(ContrastProperty),
                _jewelMaterial.GetFloat(BrightnessProperty));

            _facetStrength = _jewelMaterial.HasProperty(FacetStrengthProperty)
                ? _jewelMaterial.GetFloat(FacetStrengthProperty)
                : 1f;

            _highlightWhite = _jewelMaterial.HasProperty(HighlightWhiteProperty)
                ? _jewelMaterial.GetFloat(HighlightWhiteProperty)
                : 1f;

            _depth = _jewelMaterial.HasProperty(DepthProperty)
                ? _jewelMaterial.GetFloat(DepthProperty)
                : 0f;

            _darkLift = _jewelMaterial.HasProperty(DarkLiftProperty)
                ? _jewelMaterial.GetFloat(DarkLiftProperty)
                : 0.35f;
        }

        /// Nạp số từ asset vào núm, chỉ khi ô Config vừa đổi sang một asset khác.
        private void SyncFromConfig()
        {
            if (_tintConfig == _loadedConfig) return;

            _loadedConfig = _tintConfig;

            if (_tintConfig != null) _jewel = _tintConfig.Tint;
        }

        /// Tạo asset config mới từ bộ số đang chỉnh.
        private void CreateConfig()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Tạo Jewel Tint Config", "JewelTintConfig", "asset",
                "Chọn chỗ lưu asset chứa bộ số màu của viên ngọc.", ConfigFolder);

            if (string.IsNullOrEmpty(path)) return;

            var created = CreateInstance<JewelTintConfig>();
            created.SetTint(_jewel);

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            _tintConfig = created;
            _loadedConfig = created;

            EditorGUIUtility.PingObject(created);
        }

        private void DrawSliderSection()
        {
            EditorGUILayout.LabelField("Màu đất — ghi vào bảng màu của Grid Data", EditorStyles.boldLabel);
            _ground = DrawAdjustment(_ground,
                "Cộng thẳng vào cả ba kênh của màu đất.");

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Viên ngọc — chồng lên màu đất ở trên", EditorStyles.boldLabel);
            _jewel = DrawAdjustment(_jewel,
                "Núm quan trọng nhất của viên ngọc.\n\n" +
                "Tint của SpriteRenderer là phép NHÂN với ảnh, nên chỗ sáng nhất của " +
                "viên ngọc bằng đúng màu tint. Kéo dương thì ngọc nổi lên khỏi nền đất " +
                "cùng màu, để 0 thì ngọc chìm vào ô.");

            using (new EditorGUI.DisabledScope(_paramTexture == null))
            {
                _facetStrength = EditorGUILayout.Slider(
                    new GUIContent("Độ đậm mặt cắt", "Mờ dần lớp mặt cắt về phía màu thân " +
                        "ngọc. 1 là đúng ảnh đã vẽ, 0 là viên trơn một màu.\n\n" +
                        "Hạ núm này làm nhạt CẢ VIỀN NGOÀI, nên dưới 0.5 thì viên ngọc " +
                        "mất đường bao và chìm vào ô sáng."),
                    _facetStrength, 0f, 1f);

                _highlightWhite = EditorGUILayout.Slider(
                    new GUIContent("Độ trắng mặt đỉnh", "Hạ riêng độ loé của mặt trên cùng, " +
                        "mặt bàn giữ nguyên. 1 là đúng ảnh đã vẽ, 0 là đỉnh tụt xuống " +
                        "ngang mặt bàn.\n\nHạ Độ đậm mặt cắt cũng bớt loé, nhưng nó kéo " +
                        "phẳng cả viên; núm này chỉ ăn vào mấy mặt sáng nhất."),
                    _highlightWhite, 0f, 1f);

                _darkLift = EditorGUILayout.Slider(
                    new GUIContent("Loé trên màu tối", "Phần pha trắng còn giữ lại khi ô " +
                        "đen kịt.\n\nPha về trắng là một lượng cộng tuyệt đối: ô càng tối " +
                        "thì lượng ấy càng át màu gốc, tới mức ô đen cho ra mặt bàn xám " +
                        "sáng. Núm này nhân phần pha trắng theo độ sáng của ô — màu sáng " +
                        "không suy suyển, màu tối chỉ loé nhẹ. Để 0 thì viên ngọc đen " +
                        "phẳng lì, không còn mặt cắt nào."),
                    _darkLift, 0f, 1f);

                _depth = EditorGUILayout.Slider(
                    new GUIContent("Tách khỏi nền", "Dìm CẢ VIÊN ngọc về phía đen một " +
                        "lượng tỉ lệ.\n\nChỉ dùng khi muốn nguyên viên lùi khỏi màu ô. " +
                        "Nếu chỉ có hai mặt bên tan vào nền thì đừng kéo núm này — kéo " +
                        "riêng hai mặt đó ở mục Từng mặt cắt, kéo ở đây sẽ làm tối cả " +
                        "mặt bàn lẫn mặt đỉnh theo."),
                    _depth, 0f, 0.5f);
            }

            EditorGUILayout.Space();

            _showGround = EditorGUILayout.Toggle(
                new GUIContent("Hiện hàng màu đất", "Hàng trên là đất trơn, hàng dưới có viên ngọc đè lên."),
                _showGround);

            using (new EditorGUI.DisabledScope(_ground.IsNone && _jewel.IsNone))
            {
                if (GUILayout.Button("Trả cả hai nhóm về 0"))
                {
                    _ground = ColorAdjustment.None;
                    _jewel = ColorAdjustment.None;
                }
            }
        }

        /// Ba núm của một ColorAdjustment.
        private static ColorAdjustment DrawAdjustment(ColorAdjustment value, string brightnessTooltip)
        {
            var saturation = EditorGUILayout.Slider(
                new GUIContent("Độ rực", "0 là giữ nguyên. -1 là xám hết. +1 đẩy gấp đôi ra xa mức xám."),
                value.Saturation, -1f, 1f);

            var contrast = EditorGUILayout.Slider(
                new GUIContent("Độ tương phản", "Xoay quanh mốc xám giữa: dương thì màu sáng " +
                    "sáng thêm và màu tối tối thêm, âm thì cả bảng dồn về giữa."),
                value.Contrast, -1f, 1f);

            var brightness = EditorGUILayout.Slider(
                new GUIContent("Độ sáng", brightnessTooltip),
                value.Brightness, -0.5f, 0.5f);

            return new ColorAdjustment(saturation, contrast, brightness);
        }

        /// Ô gõ hex + danh sách màu đang xem trước.
        private void DrawCustomColorSection()
        {
            EditorGUILayout.LabelField("Màu tự điền", EditorStyles.boldLabel);

            var parsed = TryParseHex(_hexInput, out var typed);

            using (new EditorGUILayout.HorizontalScope())
            {
                _hexInput = EditorGUILayout.TextField(
                    new GUIContent("Mã hex", "Dạng #RRGGBB hoặc RRGGBB. Dán thẳng từ Figma được."),
                    _hexInput);

                using (new EditorGUI.DisabledScope(!parsed))
                {
                    if (GUILayout.Button("Thêm", GUILayout.Width(56f))) _customColors.Add(typed);
                }
            }

            if (!string.IsNullOrWhiteSpace(_hexInput) && !parsed)
            {
                EditorGUILayout.HelpBox("Mã hex không đọc được.", MessageType.Warning);
            }

            var removeAt = -1;

            for (var i = 0; i < _customColors.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var edited = EditorGUILayout.ColorField(
                        new GUIContent($"#{Hex(_customColors[i])}"), _customColors[i]);

                    _customColors[i] = (Color32)edited;

                    if (GUILayout.Button("−", GUILayout.Width(24f))) removeAt = i;
                }
            }

            if (removeAt >= 0) _customColors.RemoveAt(removeAt);
        }

        private static bool TryParseHex(string text, out Color32 color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(text)) return false;

            var trimmed = text.Trim();
            if (!trimmed.StartsWith("#")) trimmed = "#" + trimmed;

            if (!ColorUtility.TryParseHtmlString(trimmed, out var parsed)) return false;

            color = new Color32((byte)Mathf.RoundToInt(parsed.r * 255f),
                (byte)Mathf.RoundToInt(parsed.g * 255f),
                (byte)Mathf.RoundToInt(parsed.b * 255f), 255);

            return true;
        }

        /// Chín mặt cắt, cộng viền và khe.
        private void DrawFacetSection()
        {
            _showFacets = EditorGUILayout.Foldout(_showFacets, "Từng mặt cắt", true, EditorStyles.foldoutHeader);
            if (!_showFacets) return;

            if (_facetProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có asset bộ số mặt cắt. Ảnh tham số hiện tại là thứ đã nướng " +
                    "chín, không đọc ngược ra bộ số được — phải tạo asset mới rồi nướng " +
                    "lại thì mới chỉnh riêng từng mặt.", MessageType.Info);

                if (GUILayout.Button("Tạo bộ số mặt cắt mới")) CreateFacetProfile();
                return;
            }

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                for (var i = 0; i < JewelFacetProfile.FacetCount; i++)
                {
                    EditorGUILayout.LabelField(JewelFacetProfile.FacetNames[i], EditorStyles.boldLabel);
                    _facetProfile.SetFacet(i, DrawAdjustment(_facetProfile.GetFacet(i),
                        "Cộng thẳng vào cả ba kênh của mặt này."));
                }

                EditorGUILayout.LabelField("Mặt bàn (giữa)", EditorStyles.boldLabel);
                _facetProfile.Table = DrawAdjustment(_facetProfile.Table,
                    "Cộng thẳng vào cả ba kênh của mặt bàn.");

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Khe giữa các mặt", EditorStyles.boldLabel);
                _facetProfile.Seam = DrawAdjustment(_facetProfile.Seam,
                    "Cộng thẳng vào cả ba kênh của khe.");

                EditorGUILayout.LabelField("Viền ngoài", EditorStyles.boldLabel);
                _facetProfile.Outline = DrawAdjustment(_facetProfile.Outline,
                    "Cộng thẳng vào cả ba kênh của viền.");

                _facetProfile.OutlineWidth = EditorGUILayout.Slider(
                    new GUIContent("Bề rộng viền", "Tính theo ảnh 256 pixel. Đây là HÌNH " +
                        "chứ không phải màu, nên nó nằm trong ảnh tham số — đổi xong phải " +
                        "nướng lại thì trong game mới thấy."),
                    _facetProfile.OutlineWidth, 1f, 24f);

                if (change.changed)
                {
                    EditorUtility.SetDirty(_facetProfile);
                    RebuildFromProfile();
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_paramTexture == null))
            {
                if (GUILayout.Button("Nướng lại ảnh tham số")) BakeParamMap();
            }

            EditorGUILayout.HelpBox(
                "Ô xem trước đang lấy số thẳng từ asset, còn GAME đọc ảnh đã nướng. " +
                "Kéo núm xong mà không bấm nướng thì trong game không đổi gì.",
                MessageType.Warning);
        }

        private void CreateFacetProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Tạo bộ số mặt cắt", "JewelFacetProfile", "asset",
                "Chọn chỗ lưu asset chứa bộ số của từng mặt.", ConfigFolder);

            if (string.IsNullOrEmpty(path)) return;

            var created = CreateInstance<JewelFacetProfile>();

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            _facetProfile = created;
            _loadedProfile = created;

            RebuildFromProfile();
            EditorGUIUtility.PingObject(created);
        }

        /// Ghi ảnh 256x256 đè lên chính file mà material đang trỏ tới.
        private void BakeParamMap()
        {
            var path = AssetDatabase.GetAssetPath(_paramTexture);

            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Không nướng được",
                    "Ảnh tham số của material không phải một file trong project.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Ghi đè ảnh tham số",
                    $"Ghi đè '{path}'?\n\nKhông hoàn tác được bằng Ctrl+Z.", "Ghi", "Huỷ"))
            {
                return;
            }

            var pixels = JewelFacetMap.Build(_facetProfile, BakeResolution, BakeSupersample);

            var texture = new Texture2D(BakeResolution, BakeResolution, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Xem trước", EditorStyles.boldLabel);

            var colors = SourceColors();
            if (colors.Count == 0)
            {
                EditorGUILayout.HelpBox(_gridData != null
                    ? "Grid Data chưa có màu nào."
                    : "Chưa có màu nào — gõ một mã hex ở mục Màu tự điền.", MessageType.Warning);
                return;
            }

            var rows = Mathf.CeilToInt(colors.Count / (float)PreviewColumns);
            var height = rows * PreviewCell * (_showGround ? 2 : 1) + rows * 6;

            var area = GUILayoutUtility.GetRect(PreviewColumns * PreviewCell, height);

            for (var i = 0; i < colors.Count; i++)
            {
                var column = i % PreviewColumns;
                var row = i / PreviewColumns;

                var ground = _ground.Apply(colors[i]);
                var block = row * (PreviewCell * (_showGround ? 2 : 1) + 6);

                var x = area.x + column * PreviewCell;
                var y = area.y + block;

                if (_showGround)
                {
                    EditorGUI.DrawRect(new Rect(x, y, PreviewCell, PreviewCell), ground);
                    y += PreviewCell;
                }

                var cell = new Rect(x, y, PreviewCell, PreviewCell);
                EditorGUI.DrawRect(cell, ground);

                DrawJewel(cell, ground);
            }
        }

        /// Vẽ viên ngọc nằm trên màu đất `ground`.
        private void DrawJewel(Rect rect, Color32 ground)
        {
            var preview = GetPreview(ground);

            if (preview != null)
            {
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true);
                return;
            }

            DrawJewelBody(rect, _jewel.Apply(ground));
        }

        /// Nhân độ đậm rồi hạ độ loé — bản C# của đúng đoạn trong JewelFacets.shader.
        private ColorAdjustment TrimHighlight(ColorAdjustment facet, float value)
        {
            var saturation = facet.Saturation * _facetStrength;
            var contrast = facet.Contrast * _facetStrength;
            var brightness = facet.Brightness * _facetStrength;

            if (brightness <= 0.0001f) return new ColorAdjustment(saturation, contrast, brightness);

            var t = -contrast;

            t -= Mathf.Max(0f, t - HighlightFloor) * (1f - _highlightWhite);
            t *= Mathf.Lerp(_darkLift, 1f, value);

            return new ColorAdjustment(saturation, -t, 0.5f * t);
        }

        /// Dìm cả viên về phía đen — bản C# của đúng đoạn trong JewelFacets.shader.
        private ColorAdjustment ApplyDepth(ColorAdjustment value)
        {
            if (_depth <= 0.0001f) return value;

            return new ColorAdjustment(
                value.Saturation,
                (1f + value.Contrast) * (1f - _depth) - 1f,
                (0.5f + value.Brightness) * (1f - _depth) - 0.5f);
        }

        private Texture2D GetPreview(Color32 ground)
        {
            if (_facetParams == null) return null;

            var key = (ground.r << 16) | (ground.g << 8) | ground.b;
            if (_previewCache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_previewCache.Count > 64) ClearPreviewCache();

            var texture = new Texture2D(PreviewResolution, PreviewResolution, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[_facetParams.Length];

            var value = Mathf.Max(ground.r, Mathf.Max(ground.g, ground.b)) / 255f;

            for (var i = 0; i < pixels.Length; i++)
            {
                var facet = TrimHighlight(_facetParams[i], value);

                var combined = ApplyDepth(new ColorAdjustment(
                    facet.Saturation + _jewel.Saturation,
                    facet.Contrast + _jewel.Contrast,
                    facet.Brightness + _jewel.Brightness));

                var color = combined.Apply(ground);
                pixels[i] = new Color32(color.r, color.g, color.b, _facetAlpha[i]);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _previewCache[key] = texture;
            return texture;
        }

        /// Vẽ sprite bằng toạ độ UV của nó trong texture, không vẽ cả texture.
        private void DrawJewelBody(Rect rect, Color tint)
        {
            if (_jewelSprite == null || _jewelSprite.texture == null) return;

            var texture = _jewelSprite.texture;
            var region = _jewelSprite.textureRect;

            var uv = new Rect(
                region.x / texture.width,
                region.y / texture.height,
                region.width / texture.width,
                region.height / texture.height);

            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previous;
        }

        private void DrawHexSection()
        {
            EditorGUILayout.LabelField("Mã màu", EditorStyles.boldLabel);

            var colors = SourceColors();
            var text = BuildHexReport(colors);

            EditorGUILayout.SelectableLabel(text,
                EditorStyles.textArea, GUILayout.Height(Mathf.Min(160f, 18f * (colors.Count + 1))));

            if (GUILayout.Button("Sao chép vào clipboard")) EditorGUIUtility.systemCopyBuffer = text;
        }

        private string BuildHexReport(IReadOnlyList<Color32> colors)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("gốc      →  đất       →  ngọc");

            foreach (var color in colors)
            {
                var ground = _ground.Apply(color);
                var jewel = _jewel.Apply(ground);

                builder.AppendLine($"#{Hex(color)}  →  #{Hex(ground)}  →  #{Hex(jewel)}");
            }

            return builder.ToString();
        }

        private static string Hex(Color32 color) => $"{color.r:x2}{color.g:x2}{color.b:x2}";

        private void DrawApplySection()
        {
            EditorGUILayout.LabelField("Ghi lại", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!HasTintProperties(_jewelMaterial)))
            {
                if (GUILayout.Button("Ghi bộ số VIÊN NGỌC vào Material của prefab")) ApplyToMaterial();
            }

            EditorGUILayout.HelpBox(
                "Ghi vào material là đường dùng cho prefab gắn shader Jewel Facets. " +
                "Một chỗ chỉnh cho cả bảng màu, không đụng tới data của màn.",
                MessageType.None);

            if (IsTunedTwice())
            {
                EditorGUILayout.HelpBox(
                    "Material VÀ Jewel Tint Config đang cùng khác 0. Hai phép chỉnh sẽ " +
                    "chồng lên nhau lúc chạy game, viên ngọc ra màu không giống ô xem " +
                    "trước nào ở đây. Đưa một trong hai về 0.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_tintConfig == null))
            {
                if (GUILayout.Button("Ghi bộ số VIÊN NGỌC vào Jewel Tint Config")) ApplyToConfig();
            }

            EditorGUILayout.HelpBox(
                "Ghi vào config là ghi một bộ số, hoàn tác được bằng Ctrl+Z. Nhớ gán " +
                "asset này vào ô Jewel Tint của LevelManager trong scene, nếu không game " +
                "vẫn cho ngọc mang đúng màu đất.", MessageType.None);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_gridData == null))
            {
                if (GUILayout.Button("Ghi bảng màu ĐẤT vào Grid Data")) ApplyToGridData();
            }

            EditorGUILayout.HelpBox(
                "Chỉ ghi BẢNG MÀU, không đụng tới lưới ô. Thao tác này KHÔNG hoàn tác được " +
                "bằng Ctrl+Z và sinh lại lưới bằng tool ảnh sẽ ghi đè lên nó — nên chốt " +
                "bộ số xong hẵng bấm.", MessageType.Warning);
        }

        /// True khi bộ số nằm ở cả material lẫn config, cả hai đều khác 0.
        private bool IsTunedTwice()
        {
            if (_tintConfig == null || _tintConfig.Tint.IsNone) return false;
            if (!HasTintProperties(_jewelMaterial)) return false;

            var stored = new ColorAdjustment(
                _jewelMaterial.GetFloat(SaturationProperty),
                _jewelMaterial.GetFloat(ContrastProperty),
                _jewelMaterial.GetFloat(BrightnessProperty));

            return !stored.IsNone;
        }

        private void ApplyToMaterial()
        {
            Undo.RecordObject(_jewelMaterial, "Ghi bộ số màu viên ngọc");

            _jewelMaterial.SetFloat(SaturationProperty, _jewel.Saturation);
            _jewelMaterial.SetFloat(ContrastProperty, _jewel.Contrast);
            _jewelMaterial.SetFloat(BrightnessProperty, _jewel.Brightness);

            if (_jewelMaterial.HasProperty(FacetStrengthProperty))
            {
                _jewelMaterial.SetFloat(FacetStrengthProperty, _facetStrength);
            }

            if (_jewelMaterial.HasProperty(HighlightWhiteProperty))
            {
                _jewelMaterial.SetFloat(HighlightWhiteProperty, _highlightWhite);
            }

            if (_jewelMaterial.HasProperty(DepthProperty))
            {
                _jewelMaterial.SetFloat(DepthProperty, _depth);
            }

            if (_jewelMaterial.HasProperty(DarkLiftProperty))
            {
                _jewelMaterial.SetFloat(DarkLiftProperty, _darkLift);
            }

            EditorUtility.SetDirty(_jewelMaterial);
            AssetDatabase.SaveAssets();
        }

        private void ApplyToConfig()
        {
            Undo.RecordObject(_tintConfig, "Ghi bộ số màu viên ngọc");

            _tintConfig.SetTint(_jewel);

            EditorUtility.SetDirty(_tintConfig);
            AssetDatabase.SaveAssets();
        }

        private void ApplyToGridData()
        {
            var grid = _gridData.ToGrid();
            if (grid == null)
            {
                EditorUtility.DisplayDialog("Không ghi được",
                    "Grid Data này chưa có dữ liệu lưới. Sinh lưới bằng tool ảnh trước đã.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Ghi đè bảng màu",
                    $"Ghi {_gridData.Colors.Count} màu đã chỉnh vào '{_gridData.name}'?\n\n" +
                    "Không hoàn tác được bằng Ctrl+Z.", "Ghi", "Huỷ"))
            {
                return;
            }

            var adjusted = new Color32[_gridData.Colors.Count];
            for (var i = 0; i < adjusted.Length; i++) adjusted[i] = _ground.Apply(_gridData.Colors[i]);

            var cells = new int[grid.Width * grid.Height];
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++) cells[y * grid.Width + x] = grid.GetCell(x, y);
            }

            _gridData.SetData(grid.Width, grid.Height, adjusted, cells);

            EditorUtility.SetDirty(_gridData);
            AssetDatabase.SaveAssets();

            _ground = ColorAdjustment.None;
        }

        private IReadOnlyList<Color32> SourceColors()
        {
            return _gridData != null ? _gridData.Colors : (IReadOnlyList<Color32>)_customColors;
        }
    }
}
