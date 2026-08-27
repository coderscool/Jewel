using System.Collections.Generic;
using System.IO;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ chỉnh màu: xem trước ô ĐẤT và VIÊN NGỌC nằm trên nó, mỗi bên một bộ núm.
    ///
    /// Vì sao cần một cửa sổ riêng thay vì chỉnh thẳng trong Play Mode: hai thứ quyết
    /// định màu cuối cùng nằm ở hai asset khác nhau — bảng màu của LevelGridData và
    /// phép chỉnh trong JewelTintConfig. Chỉnh mù ở một bên rồi chạy game xem bên kia
    /// ra sao là vòng lặp rất chậm. Ở đây hai thứ đó nằm cạnh nhau và đổi tức thì.
    ///
    /// Phép toán KHÔNG nằm trong file này. Nó ở Gameplay/Domain/ColorAdjustment.cs,
    /// đúng cái mà LevelManager chạy lúc vào màn. Trước đây cửa sổ tự tính lấy, nên
    /// bộ số dò ra chỉ đúng ở đây còn game thì không chạy nó.
    ///
    /// Bộ số của viên ngọc có HAI chỗ ở được: asset JewelTintConfig, hoặc material
    /// của prefab nếu prefab dùng shader JewelPainter/Jewel Facets. Cửa sổ ghi được
    /// vào cả hai, nhưng chỉ nên dùng MỘT — hai phép chỉnh chồng lên nhau thì không
    /// số nào còn nghĩa. Có nút cảnh báo ở mục Ghi lại khi cả hai đang khác 0.
    public class JewelColorTunerWindow : EditorWindow
    {
        private const int PreviewCell = 84;
        private const int PreviewColumns = 6;

        /// Độ phân giải ô xem trước. Ảnh tham số 256x256 thu về đây rồi mới chạy phép
        /// chỉnh: 84 pixel trên màn hình không cần tới 65 nghìn phép tính mỗi ô.
        private const int PreviewResolution = 96;

        /// Thang mã hoá độ rực trong kênh R của ảnh tham số.
        /// PHẢI KHỚP SAT_MIN/SAT_MAX trong JewelFacets.shader và S_MIN/S_MAX của
        /// script sinh ảnh. Lệch một trong ba là màu xem trước khác màu chạy game.
        private const float ParamSaturationMin = -1f;
        private const float ParamSaturationMax = 3f;
        private const string ConfigFolder = "Assets/Scriptables";

        // Tên property của shader JewelPainter/Jewel Facets. Đọc bằng HasProperty
        // trước khi lấy: prefab có thể đang gắn Sprites/Default, lúc đó GetFloat chỉ
        // ném cảnh báo đỏ ra Console chứ không cho biết gì.
        private const string SaturationProperty = "_Saturation";
        private const string ContrastProperty = "_Contrast";
        private const string BrightnessProperty = "_Brightness";
        private const string FacetStrengthProperty = "_FacetStrength";
        private const string ParamTexProperty = "_ParamTex";

        /// Prefab viên ngọc — cùng thứ gán vào JewelLayer và JewelFlyEffect.
        ///
        /// Lấy prefab chứ không lấy rời sprite với material: ba thứ đó phải khớp nhau
        /// mới ra đúng hình, mà nguồn duy nhất biết chúng khớp là prefab. Kéo rời thì
        /// sớm muộn cũng có lúc xem trước một đằng game chạy một nẻo.
        private SpriteRenderer _jewelPrefab;

        private Material _jewelMaterial;
        private Sprite _jewelSprite;
        private Texture _paramTexture;

        /// Ảnh tham số đã giải mã: mỗi ô một ColorAdjustment, cùng alpha hình bóng.
        ///
        /// Đọc thẳng byte của FILE PNG chứ không lấy pixel của texture đã import.
        /// Texture đã import đi qua sRGB, qua nén khối, qua "Alpha Is Transparency" —
        /// ba thứ bẻ cong giá trị mà không báo gì, và ba kênh này là SỐ chứ không
        /// phải màu. Đọc file thì cửa sổ luôn thấy đúng cái script sinh ảnh đã ghi,
        /// bất kể ô import bên kia tick gì.
        private ColorAdjustment[] _facetParams;
        private byte[] _facetAlpha;

        /// Ảnh đã giải mã vào mảng trên. Chỉ đọc lại khi đổi sang ảnh khác.
        private Texture _loadedParamTexture;

        /// Ô xem trước đã dựng, theo màu đất. Dựng lại cả bảng mỗi lần OnGUI thì kéo
        /// núm là giật; xoá sạch khi bộ số đổi là đủ.
        private readonly Dictionary<int, Texture2D> _previewCache = new Dictionary<int, Texture2D>();
        private int _previewStateHash;

        /// Material đã đọc số vào cửa sổ. Cùng lý do với _loadedConfig bên dưới.
        private Material _loadedMaterial;

        private float _facetStrength = 1f;
        private LevelGridData _gridData;
        private JewelTintConfig _tintConfig;

        /// Config đã đọc số vào cửa sổ. Giữ lại để biết lúc nào người ta vừa kéo một
        /// asset KHÁC vào ô — chỉ khi đó mới nạp lại, chứ nạp mỗi lần OnGUI thì núm
        /// không bao giờ kéo được.
        private JewelTintConfig _loadedConfig;

        /// Bảng màu tự điền, dùng khi chưa kéo Grid Data vào.
        ///
        /// Trước đây chỗ này là một mảng màu mẫu cứng trong code. Dò một màu cụ thể của
        /// khách hàng thì mảng đó vô dụng: phải sinh cả một LevelGridData chỉ để xem một
        /// mã hex chạy qua bộ số ra sao. Gõ thẳng hex vào đây là xong.
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

            // Chỉ hiện khi không có Grid Data: hai bảng màu cùng lúc thì không ai biết
            // ô xem trước đang lấy màu từ đâu.
            if (_gridData == null)
            {
                DrawCustomColorSection();
                EditorGUILayout.Space();
            }

            DrawSliderSection();
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

            _tintConfig = (JewelTintConfig)EditorGUILayout.ObjectField(
                new GUIContent("Jewel Tint Config", "Asset chứa bộ số của viên ngọc, dùng chung cả game."),
                _tintConfig, typeof(JewelTintConfig), false);

            SyncFromMaterial();
            SyncFromConfig();

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

            // Chỉ giục tạo Config khi KHÔNG có đường nào khác để ghi. Prefab đã gắn
            // shader Jewel Facets thì material chính là chỗ ở của bộ số, giục thêm một
            // asset nữa chỉ dẫn người ta vào đúng cái bẫy chỉnh hai chỗ.
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
        ///
        /// sharedMaterial chứ không phải material: material sinh ra một bản sao chỉ
        /// sống trong phiên Editor, ghi vào đó là ghi vào hư không rồi tự hỏi vì sao
        /// chạy game không thấy đổi.
        private void ResolveFromPrefab()
        {
            _jewelMaterial = _jewelPrefab != null ? _jewelPrefab.sharedMaterial : null;
            _jewelSprite = _jewelPrefab != null ? _jewelPrefab.sprite : null;

            _paramTexture = _jewelMaterial != null && _jewelMaterial.HasProperty(ParamTexProperty)
                ? _jewelMaterial.GetTexture(ParamTexProperty)
                : null;

            LoadParamMap();
        }

        /// Giải mã ảnh tham số từ file PNG vào mảng ColorAdjustment.
        private void LoadParamMap()
        {
            if (_paramTexture == _loadedParamTexture) return;

            _loadedParamTexture = _paramTexture;
            _facetParams = null;
            _facetAlpha = null;
            ClearPreviewCache();

            if (_paramTexture == null) return;

            var path = AssetDatabase.GetAssetPath(_paramTexture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            // linear: true để LoadImage giữ nguyên byte, không diễn giải là màu sRGB.
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

                    _facetParams[index] = new ColorAdjustment(
                        Mathf.Lerp(ParamSaturationMin, ParamSaturationMax, pixel.r / 255f),
                        pixel.g / 255f * 2f - 1f,
                        pixel.b / 255f - 0.5f);

                    _facetAlpha[index] = pixel.a;
                }
            }

            DestroyImmediate(decoded);
        }

        /// Bộ số đổi thì mọi ô đã dựng đều hết hạn.
        private void RefreshPreviewCache()
        {
            var hash = _jewel.Saturation.GetHashCode();
            hash = hash * 397 ^ _jewel.Contrast.GetHashCode();
            hash = hash * 397 ^ _jewel.Brightness.GetHashCode();
            hash = hash * 397 ^ _facetStrength.GetHashCode();

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
        }

        /// Nạp số từ asset vào núm, chỉ khi ô Config vừa đổi sang một asset khác.
        ///
        /// Gỡ asset ra khỏi ô thì GIỮ NGUYÊN số đang có. Xoá đi là vứt công dò của
        /// người ta chỉ vì họ bấm nhầm ô, mà số trên núm thì không có Ctrl+Z.
        private void SyncFromConfig()
        {
            if (_tintConfig == _loadedConfig) return;

            _loadedConfig = _tintConfig;

            if (_tintConfig != null) _jewel = _tintConfig.Tint;
        }

        /// Asset mới sinh ra mang luôn bộ số đang dò dở, không phải bộ số rỗng — người
        /// bấm nút này gần như luôn là người vừa kéo núm xong và muốn cất nó đi.
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

        /// Ba núm của một ColorAdjustment. Thang 0 là giữ nguyên cho cả ba — xem chú
        /// thích ở ColorAdjustment về việc vì sao không dùng thang nhân quanh mốc 1.
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
        ///
        /// Giữ cả ColorField lẫn ô hex chứ không chỉ một: hex là thứ designer gửi qua
        /// chat, còn ColorField là thứ mở ra picker khi muốn dò quanh một màu.
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

            // Xoá sau vòng lặp: bỏ phần tử giữa chừng thì chỉ số của các ô còn lại lệch
            // đi ngay trong lần OnGUI đó.
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

            // Alpha luôn về 255: bảng màu của ô đất không có khái niệm trong suốt, để
            // lọt một màu alpha thấp vào đây thì ô xem trước ra màu khác game.
            color = new Color32((byte)Mathf.RoundToInt(parsed.r * 255f),
                (byte)Mathf.RoundToInt(parsed.g * 255f),
                (byte)Mathf.RoundToInt(parsed.b * 255f), 255);

            return true;
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

                // Ô có ngọc: đất trước, ngọc đè lên — đúng thứ tự lớp trong game.
                var cell = new Rect(x, y, PreviewCell, PreviewCell);
                EditorGUI.DrawRect(cell, ground);

                // Đưa MÀU ĐẤT thô vào, không phải màu đã chỉnh: mỗi pixel của viên
                // ngọc có bộ số riêng lấy từ ảnh tham số, áp một phép chỉnh chung ở
                // đây trước là chạy hai lần.
                DrawJewel(cell, ground);
            }
        }

        /// Vẽ viên ngọc nằm trên màu đất `ground`.
        ///
        /// Có ảnh tham số thì dựng ô bằng chính ColorAdjustment.Apply — đúng hàm mà
        /// shader chép lại và LevelManager gọi lúc vào màn. Không có thì lùi về cách
        /// cũ: nhuộm sprite bằng màu đã chỉnh sẵn, đủ xem màu nhưng không có mặt cắt.
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

        private Texture2D GetPreview(Color32 ground)
        {
            if (_facetParams == null) return null;

            var key = (ground.r << 16) | (ground.g << 8) | ground.b;
            if (_previewCache.TryGetValue(key, out var cached) && cached != null) return cached;

            // Kéo núm MÀU ĐẤT là sinh một màu mới mỗi frame, mỗi màu một texture.
            // Không có chặn này thì rê chuột vài giây là vài trăm texture nằm lại.
            if (_previewCache.Count > 64) ClearPreviewCache();

            var texture = new Texture2D(PreviewResolution, PreviewResolution, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[_facetParams.Length];

            for (var i = 0; i < pixels.Length; i++)
            {
                var facet = _facetParams[i];

                // Bộ số của mặt cắt nhân với độ đậm, rồi cộng bộ số chung — đúng thứ
                // tự shader làm. _facetStrength = 0 thì chỉ còn bộ số chung.
                var combined = new ColorAdjustment(
                    facet.Saturation * _facetStrength + _jewel.Saturation,
                    facet.Contrast * _facetStrength + _jewel.Contrast,
                    facet.Brightness * _facetStrength + _jewel.Brightness);

                var color = combined.Apply(ground);
                pixels[i] = new Color32(color.r, color.g, color.b, _facetAlpha[i]);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _previewCache[key] = texture;
            return texture;
        }

        /// Vẽ sprite bằng toạ độ UV của nó trong texture, không vẽ cả texture.
        /// Sprite nằm trong atlas thì textureRect chỉ là một mảnh của tấm lớn.
        ///
        /// GUI.color nhân với texture, đúng như SpriteRenderer.color nhân lúc chạy game —
        /// nên ô này cho thấy đúng thứ sẽ hiện trên bảng.
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
        ///
        /// Đọc số THẬT trong material chứ không đọc núm: núm là thứ đang dò dở, còn
        /// cái chạy trong game là số đã ghi xuống asset.
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

            // Dựng lại mảng ô từ PixelGrid: SetData ghi đè cả cụm nên phải đưa lại đủ.
            var cells = new int[grid.Width * grid.Height];
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++) cells[y * grid.Width + x] = grid.GetCell(x, y);
            }

            _gridData.SetData(grid.Width, grid.Height, adjusted, cells);

            EditorUtility.SetDirty(_gridData);
            AssetDatabase.SaveAssets();

            // Bộ số đã nằm trong asset rồi, giữ nguyên trên núm sẽ chỉnh chồng lần nữa.
            // Nhóm ngọc KHÔNG reset: nó chồng lên màu đất mới, vẫn còn nguyên ý nghĩa.
            _ground = ColorAdjustment.None;
        }

        private IReadOnlyList<Color32> SourceColors()
        {
            return _gridData != null ? _gridData.Colors : (IReadOnlyList<Color32>)_customColors;
        }
    }
}
