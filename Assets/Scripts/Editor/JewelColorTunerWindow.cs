using System.Collections.Generic;
using JewelPainter.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ chỉnh màu: xem trước ô ĐẤT và VIÊN NGỌC nằm trên nó, kèm núm chỉnh
    /// độ rực, độ tương phản, độ sáng và độ loé của viên ngọc.
    ///
    /// Vì sao cần một cửa sổ riêng thay vì chỉnh thẳng trong Play Mode: hai thứ quyết định
    /// màu cuối cùng nằm ở hai nơi khác nhau — bảng màu của LevelGridData và phép nhân
    /// tint của SpriteRenderer. Chỉnh mù ở một bên rồi chạy game xem bên kia ra sao là
    /// vòng lặp rất chậm. Ở đây hai thứ đó nằm cạnh nhau và đổi tức thì.
    ///
    /// Phép toán chỉnh màu để NGAY TRONG cửa sổ này, không đẩy sang runtime. Chừng nào
    /// bạn còn đang dò số thì nó là việc của Editor; khi chốt được bộ số rồi mới bàn tới
    /// chuyện nướng nó vào lúc sinh lưới.
    public class JewelColorTunerWindow : EditorWindow
    {
        private const int PreviewCell = 84;
        private const int PreviewColumns = 6;

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

        private float _saturation = 1f;
        private float _contrast = 1f;
        private float _brightness;
        private float _jewelLighten = 0.08f;

        private bool _showGround = true;
        private Vector2 _scroll;

        [MenuItem("JewelPainter/Chỉnh màu viên ngọc")]
        public static void Open()
        {
            var window = GetWindow<JewelColorTunerWindow>();
            window.titleContent = new GUIContent("Chỉnh màu ngọc");
            window.minSize = new Vector2(560f, 480f);
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
                new GUIContent("Sprite viên ngọc", "Ảnh xám của viên ngọc. Để trống thì chỉ xem màu đất."),
                _jewelSprite, typeof(Sprite), false);

            _gridData = (LevelGridData)EditorGUILayout.ObjectField(
                new GUIContent("Grid Data", "Lấy bảng màu thật của một màn. Để trống thì dùng bộ màu mẫu."),
                _gridData, typeof(LevelGridData), false);

            if (_gridData == null)
            {
                EditorGUILayout.HelpBox(
                    "Đang dùng bộ màu mẫu. Kéo một LevelGridData vào để chỉnh trên đúng " +
                    "bảng màu của màn đó.", MessageType.None);
            }
        }

        private void DrawSliderSection()
        {
            EditorGUILayout.LabelField("Màu đất — áp cho cả bảng màu", EditorStyles.boldLabel);

            _saturation = EditorGUILayout.Slider(
                new GUIContent("Độ rực", "1 là giữ nguyên. 0 là xám hết. Trên 1 thì đẩy ra xa mức xám."),
                _saturation, 0f, 2f);

            _contrast = EditorGUILayout.Slider(
                new GUIContent("Độ tương phản", "Xoay quanh mốc xám giữa: màu sáng sáng thêm, màu tối tối thêm."),
                _contrast, 0f, 2f);

            _brightness = EditorGUILayout.Slider(
                new GUIContent("Độ sáng", "Cộng thẳng vào cả ba kênh."),
                _brightness, -0.3f, 0.3f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Viên ngọc — chỉ áp cho sprite nằm trên", EditorStyles.boldLabel);

            _jewelLighten = EditorGUILayout.Slider(
                new GUIContent("Độ loé",
                    "CỘNG thêm trắng vào màu tô lên sprite, không đụng màu đất.\n\n" +
                    "Tint của SpriteRenderer là phép NHÂN nên không bao giờ cho ra màu sáng " +
                    "hơn màu gốc. Đây là chỗ duy nhất tạo được vùng sáng vượt lên."),
                _jewelLighten, 0f, 0.4f);

            _showGround = EditorGUILayout.Toggle(
                new GUIContent("Hiện hàng màu đất", "Hàng trên là đất trơn, hàng dưới có viên ngọc đè lên."),
                _showGround);

            using (new EditorGUI.DisabledScope(
                Mathf.Approximately(_saturation, 1f) &&
                Mathf.Approximately(_contrast, 1f) &&
                Mathf.Approximately(_brightness, 0f)))
            {
                if (GUILayout.Button("Trả về mặc định")) ResetSliders();
            }
        }

        private void ResetSliders()
        {
            _saturation = 1f;
            _contrast = 1f;
            _brightness = 0f;
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

                var ground = Adjust(colors[i]);
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
                DrawJewel(cell, Lighten(ground, _jewelLighten));
            }
        }

        /// Vẽ sprite bằng toạ độ UV của nó trong texture, không vẽ cả texture.
        /// Sprite nằm trong atlas thì textureRect chỉ là một mảnh của tấm lớn.
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
                var ground = Adjust(color);
                var jewel = Lighten(ground, _jewelLighten);

                builder.AppendLine($"#{Hex(color)}  →  #{Hex(ground)}  →  #{Hex(jewel)}");
            }

            return builder.ToString();
        }

        private static string Hex(Color32 color) => $"{color.r:x2}{color.g:x2}{color.b:x2}";

        private void DrawApplySection()
        {
            EditorGUILayout.LabelField("Ghi lại", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_gridData == null))
            {
                if (GUILayout.Button("Ghi bảng màu đã chỉnh vào Grid Data")) ApplyToGridData();
            }

            EditorGUILayout.HelpBox(
                "Chỉ ghi BẢNG MÀU, không đụng tới lưới ô. Thao tác này KHÔNG hoàn tác được " +
                "bằng Ctrl+Z và sinh lại lưới bằng tool ảnh sẽ ghi đè lên nó — nên chốt " +
                "bộ số xong hẵng bấm.", MessageType.Warning);
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
            for (var i = 0; i < adjusted.Length; i++) adjusted[i] = Adjust(_gridData.Colors[i]);

            // Dựng lại mảng ô từ PixelGrid: SetData ghi đè cả cụm nên phải đưa lại đủ.
            var cells = new int[grid.Width * grid.Height];
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++) cells[y * grid.Width + x] = grid.GetCell(x, y);
            }

            _gridData.SetData(grid.Width, grid.Height, adjusted, cells);

            EditorUtility.SetDirty(_gridData);
            AssetDatabase.SaveAssets();

            // Bộ số đã nằm trong asset rồi, giữ nguyên trên thanh trượt sẽ chỉnh chồng lần nữa.
            ResetSliders();
        }

        private IReadOnlyList<Color32> SourceColors()
        {
            return _gridData != null ? _gridData.Colors : SampleColors;
        }

        /// Rực → tương phản → sáng, đúng thứ tự Photoshop.
        ///
        /// Thứ tự có ý nghĩa: tương phản xoay quanh mốc 0.5 nên nó khuếch đại luôn cả phần
        /// lệch mà bước tăng độ rực vừa tạo ra. Đảo hai bước cho ra kết quả khác hẳn.
        private Color32 Adjust(Color32 color)
        {
            var gray = (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) / 255f;

            return new Color32(
                ToByte(AdjustChannel(color.r / 255f, gray)),
                ToByte(AdjustChannel(color.g / 255f, gray)),
                ToByte(AdjustChannel(color.b / 255f, gray)),
                color.a);
        }

        private float AdjustChannel(float channel, float gray)
        {
            // KHÔNG dùng Mathf.Lerp: nó kẹp t về 0..1 nên độ rực trên 1 sẽ dừng lại đúng
            // ở màu gốc và thanh trượt có kéo tiếp cũng không đổi gì.
            var value = gray + (channel - gray) * _saturation;

            value = (value - 0.5f) * _contrast + 0.5f;

            return value + _brightness;
        }

        /// Cộng đều một lượng trắng vào ba kênh — phép mà một nguồn sáng làm với bề mặt.
        ///
        /// Nhân theo tỉ lệ thì màu lệch sắc: nhân 1.1 lên #ea891e đẩy kênh đỏ thêm 23 mà
        /// kênh lam chỉ thêm 3, ra cam gắt. Cộng 21 vào cả ba cho ra #fe9d34 — vẫn là màu
        /// đó, chỉ sáng hơn.
        private static Color32 Lighten(Color32 color, float amount)
        {
            if (amount <= 0f) return color;

            var add = Mathf.RoundToInt(Mathf.Clamp01(amount) * 255f);

            return new Color32(
                (byte)Mathf.Min(255, color.r + add),
                (byte)Mathf.Min(255, color.g + add),
                (byte)Mathf.Min(255, color.b + add),
                color.a);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
