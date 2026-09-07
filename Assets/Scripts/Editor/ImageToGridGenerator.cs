using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Lớp vỏ Editor: đọc Texture2D, gọi xuống Domain, trả về lưới kèm bảng màu.
    /// Mọi tính toán thật nằm ở GridSampler, ColorQuantizer và PaletteMatcher.
    public static class ImageToGridGenerator
    {
        public static GridGenerationResult Generate(
            Texture2D texture, int gridWidth, int gridHeight, int maxColors, float mergeDistance = 0f)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (gridWidth < 1) gridWidth = 1;
            if (gridHeight < 1) gridHeight = 1;
            if (maxColors < 1) maxColors = 1;
            if (mergeDistance < 0f) mergeDistance = 0f;

            if (!EnsureRawPixels(texture))
            {
                throw new InvalidOperationException(
                    $"Không đọc được pixel gốc của '{texture.name}'. Ảnh sinh bằng code vốn đã đọc được; " +
                    "ảnh trong project thì mở Import Settings, tick Read/Write Enabled và đặt " +
                    "Compression = None.");
            }

            var gridSize = new Vector2Int(gridWidth, gridHeight);
            var pixels = FlipVertically(texture.GetPixels32(), texture.width, texture.height);

            var cells = GridSampler.Sample(pixels, texture.width, texture.height, gridSize.x, gridSize.y);

            // Rút bảng màu từ chính các ô đã lấy mẫu, không phải từ toàn bộ pixel ảnh:
            // ít dữ liệu hơn hẳn mà lại đúng thứ cần biểu diễn.
            var opaqueColors = CollectOpaqueColors(cells);
            var palette = ColorQuantizer.Quantize(opaqueColors, maxColors, mergeDistance);

            var grid = BuildGrid(cells, gridSize, palette);

            return new GridGenerationResult(grid, palette);
        }

        /// Gợi ý kích thước lưới giữ đúng tỉ lệ ảnh, dùng cho nút "Theo tỉ lệ ảnh"
        /// trong cửa sổ tool. Người dùng vẫn nhập tay được hai cạnh nếu muốn kéo méo.
        public static Vector2Int CalculateGridSize(int imageWidth, int imageHeight, int longestSideCells)
        {
            if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Phải dương");
            if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight), imageHeight, "Phải dương");
            if (longestSideCells < 1) longestSideCells = 1;

            if (imageWidth >= imageHeight)
            {
                var height = Mathf.Max(1, Mathf.RoundToInt(longestSideCells * (float)imageHeight / imageWidth));
                return new Vector2Int(longestSideCells, height);
            }

            var width = Mathf.Max(1, Mathf.RoundToInt(longestSideCells * (float)imageWidth / imageHeight));
            return new Vector2Int(width, longestSideCells);
        }

        /// Ép ảnh về trạng thái đọc được ĐÚNG pixel gốc, rồi mới lấy mẫu.
        ///
        /// Read/Write chỉ là một nửa. Nửa còn lại — và là nửa âm thầm phá hoại — là NÉN:
        ///
        /// GetPixels32 trả về dữ liệu đã GIẢI NÉN, không phải pixel trong file PNG. Với
        /// Compression = Compressed (mặc định của Unity), ảnh nằm ở dạng DXT/ETC: mỗi khối
        /// 4x4 chỉ giữ HAI màu đầu mút, đã hạ về RGB565, rồi mọi pixel trong khối bị ép về
        /// một trong bốn giá trị — hai đầu mút và hai màu nội suy giữa chúng.
        ///
        /// Hệ quả với tranh pixel: nét viền đen sát cạnh mảng xanh bị kéo thành một dãy
        /// màu pha không hề tồn tại trong file gốc. Ảnh 60x60 tưởng là 16 màu sạch hoá ra
        /// hơn một nghìn màu, và chính đám màu pha đó làm bộ lượng tử hoá sinh ra bảng màu
        /// xỉn — nó có bàn đạp để gộp dần đen sang xanh.
        ///
        /// npotScale cũng bị ép về None: ảnh cạnh không phải luỹ thừa 2 mà để ToNearest thì
        /// Unity co giãn nó bằng lọc song tuyến trước khi ta kịp đọc, và mọi cạnh sắc thành
        /// cạnh nhoè.
        ///
        /// Sửa thẳng Import Settings thay vì chỉ báo lỗi — cùng lối đã dùng cho Read/Write:
        /// đây là tool nội bộ, và bắt người dùng tự đi tick ba ô là ba chỗ để quên.
        public static bool EnsureRawPixels(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);

            // Ảnh tạo bằng code không nằm trong AssetDatabase — nó vốn đã là pixel thật.
            if (string.IsNullOrEmpty(path)) return texture.isReadable;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return texture.isReadable;

            var changed = false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.crunchedCompression)
            {
                importer.crunchedCompression = false;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (changed)
            {
                Debug.Log($"[ImageToGrid] Đã đặt lại Import Settings của '{texture.name}': " +
                          "Read/Write bật, Compression None, npotScale None. Không có ba thứ này " +
                          "thì pixel đọc ra là bản đã qua nén, không phải ảnh gốc.");

                importer.SaveAndReimport();
            }

            return texture.isReadable;
        }

        /// Ảnh có bị Max Size cắt nhỏ lúc import không.
        ///
        /// Bị cắt thì thứ tool đọc được đã là bản THU NHỎ bằng lọc song tuyến — cạnh nhoè,
        /// màu pha, y hệt tác hại của nén. Không tự sửa vì nâng Max Size là quyết định về
        /// bộ nhớ, không phải thứ tool được tự tiện đổi hộ.
        public static bool IsSizeClamped(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return false;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return false;

            return Mathf.Max(texture.width, texture.height) >= importer.maxTextureSize;
        }

        private static List<Color32> CollectOpaqueColors(SampledCell[] cells)
        {
            var colors = new List<Color32>(cells.Length);

            foreach (var cell in cells)
            {
                if (cell.IsEmpty) continue;

                colors.Add(cell.Color);
            }

            return colors;
        }

        private static PixelGrid BuildGrid(SampledCell[] cells, Vector2Int gridSize, IReadOnlyList<Color32> palette)
        {
            var grid = new PixelGrid(gridSize.x, gridSize.y);
            if (palette.Count == 0) return grid;

            for (var y = 0; y < gridSize.y; y++)
            {
                for (var x = 0; x < gridSize.x; x++)
                {
                    var cell = cells[y * gridSize.x + x];
                    if (cell.IsEmpty) continue;   // PixelGrid đã khởi tạo sẵn EmptyCell

                    grid.SetCell(x, y, PaletteMatcher.FindNearest(cell.Color, palette));
                }
            }

            return grid;
        }

        /// Texture2D trả hàng dưới cùng trước; PixelGrid quy ước y = 0 là hàng trên cùng.
        private static Color32[] FlipVertically(Color32[] pixels, int width, int height)
        {
            var flipped = new Color32[pixels.Length];

            for (var y = 0; y < height; y++)
            {
                var sourceRow = (height - 1 - y) * width;
                var targetRow = y * width;
                Array.Copy(pixels, sourceRow, flipped, targetRow, width);
            }

            return flipped;
        }
    }
}
