using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Data;
using UnityEngine;

namespace JewelPainter.UI.Components
{
    /// Dựng ảnh thu nhỏ của một màn chơi: ô đã tô hiện màu thật, ô chưa tô hiện xám.
    ///
    /// Cùng cách vẽ với BoardView nhưng KHÔNG dùng lại nó: BoardView vẽ màn ĐANG chơi
    /// và gắn liền với trạng thái sống của nó, còn ở đây cần vẽ một màn bất kỳ từ dữ
    /// liệu đọc trên đĩa, kể cả màn chưa bao giờ được nạp.
    ///
    /// Một pixel là một ô, đúng như BoardView. Bảng 27x36 ra ảnh 27x36 — nhỏ xíu, và
    /// Image trong UI phóng nó lên bao nhiêu tuỳ layout.
    public static class LevelThumbnailBuilder
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        /// paintedBits để null nghĩa là chưa tô ô nào. Truyền paintAll = true thì bỏ
        /// qua paintedBits và coi như tô kín — dùng cho màn đã hoàn thành, vì bản lưu
        /// của màn đó đã bị xoá ngay lúc nó xong.
        ///
        /// Người gọi chịu trách nhiệm huỷ: Destroy(sprite.texture) rồi Destroy(sprite).
        /// Trả về null nếu asset chưa được tool sinh dữ liệu.
        public static Sprite Build(LevelGridData data, byte[] paintedBits, bool paintAll, int levelId = 0)
        {
            // Trả null lặng lẽ là kiểu hỏng khó chịu nhất ở đây: ô trong danh sách chỉ
            // còn mỗi số màn, không có gì gợi ý là thiếu asset hay thiếu dữ liệu.
            if (data == null)
            {
                Debug.LogWarning($"Màn {levelId}: LevelConfig chưa gán Grid Data — ô trên " +
                                 "Home sẽ trống.");
                return null;
            }

            var grid = data.ToGrid();
            if (grid == null)
            {
                Debug.LogWarning($"Màn {levelId}: '{data.name}' chưa được tool sinh dữ liệu " +
                                 "lưới — ô trên Home sẽ trống.");
                return null;
            }

            var colors = data.Colors;
            if (colors.Count == 0)
            {
                Debug.LogWarning($"Màn {levelId}: '{data.name}' không có màu nào — " +
                                 "ô trên Home sẽ trống.");
                return null;
            }

            var pixels = new Color32[grid.Width * grid.Height];

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    var color = Transparent;

                    if (index >= 0 && index < colors.Count)
                    {
                        var painted = paintAll || IsBitSet(paintedBits, y * grid.Width + x);
                        color = painted ? colors[index] : BoardColors.ToGrayscale(colors[index]);
                    }

                    // PixelGrid có y = 0 ở TRÊN, Texture2D có y = 0 ở DƯỚI.
                    pixels[(grid.Height - 1 - y) * grid.Width + x] = color;
                }
            }

            var texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels32(pixels);
            texture.Apply(false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, grid.Width, grid.Height),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        /// Cùng cách gói bit với PaintState.ToPaintedBits: chỉ số ô chạy theo hàng,
        /// mỗi byte tám ô, bit thấp trước.
        private static bool IsBitSet(byte[] bits, int index)
        {
            if (bits == null) return false;

            var byteIndex = index >> 3;
            if (byteIndex < 0 || byteIndex >= bits.Length) return false;

            return (bits[byteIndex] & (1 << (index & 7))) != 0;
        }
    }
}
