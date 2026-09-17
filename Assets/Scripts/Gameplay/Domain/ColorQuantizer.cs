using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Rút bảng màu từ một tập màu bằng thuật toán median cut.
    public static class ColorQuantizer
    {
        /// Rút bảng màu theo số màu yêu cầu.
        public static Color32[] Quantize(IReadOnlyList<Color32> colors, int maxColors, float mergeDistance = 0f)
        {
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (maxColors < 1) maxColors = 1;
            if (colors.Count == 0) return Array.Empty<Color32>();

            var distinct = CollectDistinct(colors);
            if (distinct.Count <= maxColors)
            {
                if (mergeDistance > 0f) return MergeColors(distinct, mergeDistance);

                var exact = new Color32[distinct.Count];
                distinct.CopyTo(exact);
                return exact;
            }

            var boxes = new List<List<Color32>>(maxColors);
            var first = new List<Color32>(colors.Count);
            for (var i = 0; i < colors.Count; i++) first.Add(colors[i]);
            boxes.Add(first);

            while (boxes.Count < maxColors)
            {
                var index = FindWidestBox(boxes);
                if (index < 0) break;

                var (left, right) = Split(boxes[index]);
                boxes[index] = left;
                boxes.Add(right);
            }

            if (mergeDistance > 0f) MergeSimilar(boxes, mergeDistance);

            var palette = new Color32[boxes.Count];
            for (var i = 0; i < boxes.Count; i++) palette[i] = Average(boxes[i]);

            return palette;
        }

        /// Gộp các màu gần nhau trong danh sách màu rời.
        private static Color32[] MergeColors(List<Color32> colors, float mergeDistance)
        {
            var boxes = new List<List<Color32>>(colors.Count);
            foreach (var color in colors) boxes.Add(new List<Color32> { color });

            MergeSimilar(boxes, mergeDistance);

            var palette = new Color32[boxes.Count];
            for (var i = 0; i < boxes.Count; i++) palette[i] = Average(boxes[i]);

            return palette;
        }

        /// Gộp dần các cặp hộp màu gần nhau nhất.
        private static void MergeSimilar(List<List<Color32>> boxes, float mergeDistance)
        {
            var averages = new List<Color32>(boxes.Count);
            for (var i = 0; i < boxes.Count; i++) averages.Add(Average(boxes[i]));

            var rejected = new HashSet<(List<Color32>, List<Color32>)>();

            while (boxes.Count > 1)
            {
                var bestA = -1;
                var bestB = -1;
                var bestDistance = double.MaxValue;

                for (var i = 0; i < boxes.Count; i++)
                {
                    for (var j = i + 1; j < boxes.Count; j++)
                    {
                        if (rejected.Contains((boxes[i], boxes[j]))) continue;

                        var distance = PaletteMatcher.Distance(averages[i], averages[j]);
                        if (distance > mergeDistance || distance >= bestDistance) continue;

                        bestDistance = distance;
                        bestA = i;
                        bestB = j;
                    }
                }

                if (bestA < 0) return;

                if (SpreadAfterMerge(boxes[bestA], boxes[bestB]) > mergeDistance)
                {
                    rejected.Add((boxes[bestA], boxes[bestB]));
                    rejected.Add((boxes[bestB], boxes[bestA]));
                    continue;
                }

                boxes[bestA].AddRange(boxes[bestB]);
                averages[bestA] = Average(boxes[bestA]);

                boxes.RemoveAt(bestB);
                averages.RemoveAt(bestB);

                rejected.Clear();
            }
        }

        /// Bán kính của hộp sau khi gộp: khoảng cách từ màu trung bình mới tới thành viên xa nhất.
        private static double SpreadAfterMerge(List<Color32> a, List<Color32> b)
        {
            long sumRed = 0, sumGreen = 0, sumBlue = 0;
            var count = a.Count + b.Count;

            if (count == 0) return 0.0;

            for (var i = 0; i < a.Count; i++)
            {
                sumRed += a[i].r;
                sumGreen += a[i].g;
                sumBlue += a[i].b;
            }

            for (var i = 0; i < b.Count; i++)
            {
                sumRed += b[i].r;
                sumGreen += b[i].g;
                sumBlue += b[i].b;
            }

            var center = new Color32(
                (byte)(sumRed / count), (byte)(sumGreen / count), (byte)(sumBlue / count), byte.MaxValue);

            var farthest = 0.0;

            for (var i = 0; i < a.Count; i++) farthest = Math.Max(farthest, PaletteMatcher.Distance(a[i], center));
            for (var i = 0; i < b.Count; i++) farthest = Math.Max(farthest, PaletteMatcher.Distance(b[i], center));

            return farthest;
        }

        private static List<Color32> CollectDistinct(IReadOnlyList<Color32> colors)
        {
            var seen = new HashSet<int>();
            var distinct = new List<Color32>();

            for (var i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                var key = (color.r << 16) | (color.g << 8) | color.b;

                if (seen.Add(key)) distinct.Add(color);
            }

            return distinct;
        }

        /// Hộp đáng cắt nhất.
        private static int FindWidestBox(List<List<Color32>> boxes)
        {
            var bestIndex = -1;
            var bestScore = 0L;

            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count < 2) continue;

                var range = LongestAxisLength(boxes[i]);
                if (range <= 0) continue;

                var score = (long)range * boxes[i].Count;
                if (score <= bestScore) continue;

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static int LongestAxisLength(List<Color32> box)
        {
            GetRanges(box, out var rangeRed, out var rangeGreen, out var rangeBlue);

            return Mathf.Max(rangeRed, Mathf.Max(rangeGreen, rangeBlue));
        }

        private static void GetRanges(List<Color32> box, out int red, out int green, out int blue)
        {
            int minRed = 255, maxRed = 0;
            int minGreen = 255, maxGreen = 0;
            int minBlue = 255, maxBlue = 0;

            for (var i = 0; i < box.Count; i++)
            {
                var color = box[i];

                if (color.r < minRed) minRed = color.r;
                if (color.r > maxRed) maxRed = color.r;
                if (color.g < minGreen) minGreen = color.g;
                if (color.g > maxGreen) maxGreen = color.g;
                if (color.b < minBlue) minBlue = color.b;
                if (color.b > maxBlue) maxBlue = color.b;
            }

            red = maxRed - minRed;
            green = maxGreen - minGreen;
            blue = maxBlue - minBlue;
        }

        private static (List<Color32> left, List<Color32> right) Split(List<Color32> box)
        {
            GetRanges(box, out var rangeRed, out var rangeGreen, out var rangeBlue);

            int axis;

            if (rangeRed >= rangeGreen && rangeRed >= rangeBlue) { box.Sort(CompareRed); axis = 0; }
            else if (rangeGreen >= rangeBlue) { box.Sort(CompareGreen); axis = 1; }
            else { box.Sort(CompareBlue); axis = 2; }

            var middle = SplitIndex(box, axis, box.Count / 2);

            var left = new List<Color32>(middle);
            var right = new List<Color32>(box.Count - middle);

            for (var i = 0; i < middle; i++) left.Add(box[i]);
            for (var i = middle; i < box.Count; i++) right.Add(box[i]);

            return (left, right);
        }

        /// Tìm điểm cắt hộp màu.
        private static int SplitIndex(List<Color32> box, int axis, int middle)
        {
            for (var offset = 0; offset < box.Count; offset++)
            {
                var forward = middle + offset;
                if (forward > 0 && forward < box.Count &&
                    AxisValue(box[forward - 1], axis) != AxisValue(box[forward], axis))
                {
                    return forward;
                }

                var backward = middle - offset;
                if (backward > 0 && backward < box.Count &&
                    AxisValue(box[backward - 1], axis) != AxisValue(box[backward], axis))
                {
                    return backward;
                }
            }

            return middle;
        }

        private static byte AxisValue(Color32 color, int axis)
        {
            return axis switch
            {
                0 => color.r,
                1 => color.g,
                _ => color.b,
            };
        }

        private static int CompareRed(Color32 a, Color32 b) => a.r.CompareTo(b.r);
        private static int CompareGreen(Color32 a, Color32 b) => a.g.CompareTo(b.g);
        private static int CompareBlue(Color32 a, Color32 b) => a.b.CompareTo(b.b);

        private static Color32 Average(List<Color32> box)
        {
            if (box.Count == 0) return new Color32(0, 0, 0, 255);

            long sumRed = 0, sumGreen = 0, sumBlue = 0;

            for (var i = 0; i < box.Count; i++)
            {
                sumRed += box[i].r;
                sumGreen += box[i].g;
                sumBlue += box[i].b;
            }

            return new Color32(
                (byte)(sumRed / box.Count),
                (byte)(sumGreen / box.Count),
                (byte)(sumBlue / box.Count),
                byte.MaxValue);
        }
    }
}
