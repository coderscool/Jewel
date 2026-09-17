using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Tìm màu gần nhất trong bảng màu.
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

        /// Khoảng cách cảm nhận giữa hai màu, thang 0 (trùng khớp) đến khoảng 765 (đen với trắng).
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
