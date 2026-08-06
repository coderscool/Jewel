using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Tìm màu gần nhất trong bảng màu.
    ///
    /// Dùng xấp xỉ "redmean" thay vì khoảng cách Euclid thẳng trên RGB. Thêm khoảng
    /// mười dòng nhưng bám cảm nhận mắt người tốt hơn đáng kể — khoảng cách RGB thẳng
    /// coi mọi kênh nặng như nhau, khiến tông da người hay bị đẩy sang xanh lá.
    public static class PaletteMatcher
    {
        public static int FindNearest(Color32 color, IReadOnlyList<Color32> palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Count == 0) throw new ArgumentException("Bảng màu rỗng", nameof(palette));

            var bestIndex = 0;
            var bestDistance = double.MaxValue;

            for (var i = 0; i < palette.Count; i++)
            {
                var distance = SquaredDistance(color, palette[i]);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        /// Khoảng cách cảm nhận giữa hai màu, thang 0 (trùng khớp) đến khoảng 765
        /// (đen với trắng). Công khai để ColorQuantizer dùng chung một định nghĩa
        /// "gần giống nhau" với việc dò màu.
        public static double Distance(Color32 a, Color32 b) => Math.Sqrt(SquaredDistance(a, b));

        private static double SquaredDistance(Color32 a, Color32 b)
        {
            var meanRed = (a.r + b.r) / 2.0;
            var deltaRed = (double)a.r - b.r;
            var deltaGreen = (double)a.g - b.g;
            var deltaBlue = (double)a.b - b.b;

            return (2.0 + meanRed / 256.0) * deltaRed * deltaRed
                   + 4.0 * deltaGreen * deltaGreen
                   + (2.0 + (255.0 - meanRed) / 256.0) * deltaBlue * deltaBlue;
        }
    }
}
