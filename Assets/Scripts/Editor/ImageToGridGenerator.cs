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

            if (!EnsureReadable(texture))
            {
                throw new InvalidOperationException(
                    $"Không bật được Read/Write cho '{texture.name}'. Ảnh sinh bằng code không cần bật; " +
                    "ảnh trong project thì mở Import Settings và tick Read/Write Enabled.");
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

        /// GetPixels32 ném lỗi nếu ảnh chưa bật Read/Write. Bật giúp người dùng
        /// thay vì bắt họ đi mở Import Settings.
        /// Ảnh tạo bằng code (không nằm trong AssetDatabase) vốn đã readable.
        public static bool EnsureReadable(Texture2D texture)
        {
            if (texture.isReadable) return true;

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return false;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return false;

            importer.isReadable = true;
            importer.SaveAndReimport();

            return texture.isReadable;
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
