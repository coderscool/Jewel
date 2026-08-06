using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Chia ảnh thành lưới ô chữ nhật, mỗi ô lấy màu CHIẾM PHẦN LỚN diện tích ô đó.
    ///
    /// Không lấy trung bình: ô nằm vắt qua ranh giới hai vùng màu sẽ cho ra một màu
    /// thứ ba không có thật trong ảnh — đỏ cạnh xanh ra nâu xỉn. Lấy màu áp đảo thì ô
    /// đó theo hẳn về một bên, đường nét trong ảnh giữ được sắc.
    ///
    /// Không quan tâm trên/dưới — chỉ ánh xạ mảng 2D sang mảng 2D thô hơn, giữ nguyên
    /// chiều. Bên gọi chịu trách nhiệm lật ảnh cho đúng hướng trước khi truyền vào.
    public static class GridSampler
    {
        /// Alpha dưới ngưỡng này coi như trong suốt.
        public const byte DefaultAlphaThreshold = 128;

        /// Gom màu về 16 mức mỗi kênh trước khi đếm.
        ///
        /// Đếm màu trùng khít từng bit là vô dụng với ảnh có khử răng cưa: gần như mỗi
        /// pixel một màu lệch nhau chút xíu, "nhiều nhất" sẽ ra một pixel ngẫu nhiên.
        /// Gom nhóm rồi mới đếm thì vài pixel chuyển tiếp ở mép không lấn át được vùng
        /// màu thật sự chiếm diện tích.
        private const int BucketShift = 4;

        public static SampledCell[] Sample(
            IReadOnlyList<Color32> pixels,
            int imageWidth,
            int imageHeight,
            int gridWidth,
            int gridHeight,
            byte alphaThreshold = DefaultAlphaThreshold)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Phải dương");
            if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight), imageHeight, "Phải dương");
            if (gridWidth <= 0) throw new ArgumentOutOfRangeException(nameof(gridWidth), gridWidth, "Phải dương");
            if (gridHeight <= 0) throw new ArgumentOutOfRangeException(nameof(gridHeight), gridHeight, "Phải dương");

            if (pixels.Count != imageWidth * imageHeight)
            {
                throw new ArgumentException(
                    $"Cần {imageWidth * imageHeight} pixel cho ảnh {imageWidth}x{imageHeight}, nhận được {pixels.Count}",
                    nameof(pixels));
            }

            var cells = new SampledCell[gridWidth * gridHeight];

            // Một dictionary dùng lại cho mọi ô, xoá giữa các lần — cấp phát mới cho
            // từng ô là 4096 dictionary cho lưới 64x64.
            var buckets = new Dictionary<int, Bucket>();

            for (var cellY = 0; cellY < gridHeight; cellY++)
            {
                var startY = cellY * imageHeight / gridHeight;
                var endY = (cellY + 1) * imageHeight / gridHeight;
                if (endY <= startY) endY = startY + 1;

                for (var cellX = 0; cellX < gridWidth; cellX++)
                {
                    var startX = cellX * imageWidth / gridWidth;
                    var endX = (cellX + 1) * imageWidth / gridWidth;
                    if (endX <= startX) endX = startX + 1;

                    cells[cellY * gridWidth + cellX] =
                        SampleCell(pixels, imageWidth, startX, endX, startY, endY, alphaThreshold, buckets);
                }
            }

            return cells;
        }

        private static SampledCell SampleCell(
            IReadOnlyList<Color32> pixels,
            int imageWidth,
            int startX,
            int endX,
            int startY,
            int endY,
            byte alphaThreshold,
            Dictionary<int, Bucket> buckets)
        {
            buckets.Clear();

            var opaqueCount = 0;
            var totalCount = 0;

            for (var y = startY; y < endY; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    var pixel = pixels[y * imageWidth + x];
                    totalCount++;

                    if (pixel.a < alphaThreshold) continue;

                    opaqueCount++;

                    var key = BucketKey(pixel);
                    buckets.TryGetValue(key, out var bucket);

                    bucket.SumRed += pixel.r;
                    bucket.SumGreen += pixel.g;
                    bucket.SumBlue += pixel.b;
                    bucket.Count++;

                    buckets[key] = bucket;
                }
            }

            // Rỗng khi QUÁ nửa số pixel trong suốt. Đúng một nửa thì vẫn tô.
            if (opaqueCount == 0 || opaqueCount * 2 < totalCount) return SampledCell.Empty;

            var winner = default(Bucket);
            foreach (var pair in buckets)
            {
                if (pair.Value.Count > winner.Count) winner = pair.Value;
            }

            // Trung bình của RIÊNG nhóm thắng, không phải của cả ô: cho ra màu thật
            // của vùng chiếm diện tích chứ không phải màu pha giữa các vùng.
            var color = new Color32(
                (byte)(winner.SumRed / winner.Count),
                (byte)(winner.SumGreen / winner.Count),
                (byte)(winner.SumBlue / winner.Count),
                byte.MaxValue);

            return new SampledCell(color, false);
        }

        private static int BucketKey(Color32 pixel)
        {
            return ((pixel.r >> BucketShift) << 16)
                   | ((pixel.g >> BucketShift) << 8)
                   | (pixel.b >> BucketShift);
        }

        private struct Bucket
        {
            public long SumRed;
            public long SumGreen;
            public long SumBlue;
            public int Count;
        }
    }
}
