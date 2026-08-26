using System.Collections.Generic;
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
    public class JewelColorTunerWindow : EditorWindow
    {
        private const int PreviewCell = 84;
        private const int PreviewColumns = 6;
        private const string ConfigFolder = "Assets/Scriptables";

        /// Bộ màu mẫu khi chưa chọn Grid Data — đủ trải từ sẫm tới nhạt để thấy ngay
        /// một núm ảnh hưởng khác nhau thế nào ở hai đầu thang sáng.
        private static readonly Color32[] SampleColors =
        {
            new Color32(0xEA, 0x89, 0x1E, 255),
            new Color32(0x59, 0xAE, 0xEE, 255),
            new Color32(0x1C, 0xA8, 0xA3, 255),
            new Color32(0x5C, 0x9E, 0x21, 255),
            new Color32(0xDB, 0x33, 0x29, 255),
            new Color32(0xF2, 0xEB, 0xD8, 255),
            new Color32(0x2B, 0x2B, 0x33, 255),
            new Color32(0x7A, 0x3E, 0x8C, 255),
        };

        private Sprite _jewelSprite;
        private LevelGridData _gridData;
        private JewelTintConfig _tintConfig;

        /// Config đã đọc số vào cửa sổ. Giữ lại để biết lúc nào người ta vừa kéo một
        /// asset KHÁC vào ô — chỉ khi đó mới nạp lại, chứ nạp mỗi lần OnGUI thì núm
        /// không bao giờ kéo được.
        private JewelTintConfig _loadedConfig;

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

            DrawSliderSection();
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

            _jewelSprite = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Sprite viên ngọc", "Ảnh xám của viên ngọc — cùng sprite " +
                    "gắn trên prefab mà JewelLayer dùng. Để trống thì chỉ xem màu đất."),
                _jewelSprite, typeof(Sprite), false);

            _gridData = (LevelGridData)EditorGUILayout.ObjectField(
                new GUIContent("Grid Data", "Lấy bảng màu thật của một màn. Để trống thì dùng bộ màu mẫu."),
                _gridData, typeof(LevelGridData), false);

            _tintConfig = (JewelTintConfig)EditorGUILayout.ObjectField(
                new GUIContent("Jewel Tint Config", "Asset chứa bộ số của viên ngọc, dùng chung cả game."),
                _tintConfig, typeof(JewelTintConfig), false);

            SyncFromConfig();

            if (_gridData == null)
            {
                EditorGUILayout.HelpBox(
                    "Đang dùng bộ màu mẫu. Kéo một LevelGridData vào để chỉnh trên đúng " +
                    "bảng màu của màn đó.", MessageType.None);
            }

            if (_tintConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có Jewel Tint Config — núm của viên ngọc chỉ xem trước, không " +
                    "ghi đi đâu được.", MessageType.Info);

                if (GUILayout.Button("Tạo Jewel Tint Config mới")) CreateConfig();
            }
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

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Xem trước", EditorStyles.boldLabel);

            var colors = SourceColors();
            if (colors.Count == 0)
            {
                EditorGUILayout.HelpBox("Grid Data chưa có màu nào.", MessageType.Warning);
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
                DrawJewel(cell, _jewel.Apply(ground));
            }
        }

        /// Vẽ sprite bằng toạ độ UV của nó trong texture, không vẽ cả texture.
        /// Sprite nằm trong atlas thì textureRect chỉ là một mảnh của tấm lớn.
        ///
        /// GUI.color nhân với texture, đúng như SpriteRenderer.color nhân lúc chạy game —
        /// nên ô này cho thấy đúng thứ sẽ hiện trên bảng.
        private void DrawJewel(Rect rect, Color tint)
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
            return _gridData != null ? _gridData.Colors : SampleColors;
        }
    }
}
