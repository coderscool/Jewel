using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Rút bảng màu từ một tập màu bằng thuật toán median cut.
    ///
    /// Ý tưởng: bỏ hết màu vào một hộp, cắt đôi hộp theo trục màu trải rộng nhất tại
    /// điểm giữa, lặp lại trên hộp còn rộng nhất cho tới khi đủ số hộp. Mỗi hộp lấy
    /// màu trung bình. Cắt theo phân bố thật nên vùng nào nhiều màu thì được chia mịn hơn.
    ///
    /// Thuần C# — test EditMode được, không cần scene.
    public static class ColorQuantizer
    {
        /// Ít màu hơn yêu cầu thì trả đúng số màu đang có, không nhồi thêm màu rác.
        ///
        /// mergeDistance > 0 thì sau khi cắt xong, những hộp có màu trung bình gần nhau
        /// hơn ngưỡng đó sẽ gộp lại. Thang khoảng cách 0..765 theo PaletteMatcher —
        /// 20 là gộp các sắc độ rất sát, 60 là gộp mạnh tay.
        public static Color32[] Quantize(IReadOnlyList<Color32> colors, int maxColors, float mergeDistance = 0f)
        {
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (maxColors < 1) maxColors = 1;
            if (colors.Count == 0) return Array.Empty<Color32>();

            // Ảnh ít màu hơn yêu cầu: trả thẳng, khỏi cắt. Nếu để median cut chạy thì
            // một màu có thể bị tách thành hai hộp giống hệt nhau, ra bảng màu trùng lặp.
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

        /// Gộp trên danh sách màu rời, dùng cho nhánh ảnh vốn đã ít màu.
        private static Color32[] MergeColors(List<Color32> colors, float mergeDistance)
        {
            var boxes = new List<List<Color32>>(colors.Count);
            foreach (var color in colors) boxes.Add(new List<Color32> { color });

            MergeSimilar(boxes, mergeDistance);

            var palette = new Color32[boxes.Count];
            for (var i = 0; i < boxes.Count; i++) palette[i] = Average(boxes[i]);

            return palette;
        }

        /// Gộp lặp lại cặp hộp gần nhau nhất cho tới khi không còn cặp nào hợp lệ.
        ///
        /// Gộp trên HỘP chứ không trên màu trung bình: hai hộp nhập vào nhau rồi mới
        /// tính trung bình, nên hộp nhiều pixel kéo màu chung về phía nó — đúng hơn là
        /// lấy trung bình của hai màu đại diện.
        ///
        /// HAI phép kiểm, không phải một, và phép thứ hai mới là phần quan trọng:
        ///
        ///   1. hai tâm hộp phải cách nhau dưới mergeDistance — đây là phép cũ;
        ///   2. hộp SAU KHI GỘP vẫn phải nằm gọn trong bán kính mergeDistance.
        ///
        /// Thiếu phép thứ hai thì đây là gộp cụm theo liên kết đơn, và nó DÂY CHUYỀN: A
        /// nuốt B (cách 20), tâm trôi; hộp mới nuốt C (lại cách 20), tâm trôi tiếp. Sau
        /// tám bước hộp trải rộng 160 trong không gian màu, trong khi MỌI bước đều lọt qua
        /// ngưỡng 20.
        ///
        /// Đó là cách nét viền đen bị nuốt vào mảng xanh dù hai màu cách nhau 171 — xa hơn
        /// cả giá trị lớn nhất của thanh trượt. Kết quả là bảng màu xỉn và mất hẳn màu đen,
        /// còn người dùng thì không hiểu vì sao đặt ngưỡng 24 lại gộp hai màu cách nhau 171.
        private static void MergeSimilar(List<List<Color32>> boxes, float mergeDistance)
        {
            var averages = new List<Color32>(boxes.Count);
            for (var i = 0; i < boxes.Count; i++) averages.Add(Average(boxes[i]));

            // Cặp đã bị từ chối vì gộp vào sẽ phình quá rộng. Nhớ lại để vòng sau không
            // chọn đúng nó rồi từ chối mãi. Xoá sạch sau MỖI lần gộp thành công: nội dung
            // hộp đã đổi nên mọi phán quyết cũ hết giá trị.
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

                // Chỉ đo bán kính cho cặp ĐANG THẮNG, không đo cho mọi cặp: phép này duyệt
                // hết pixel của cả hai hộp, mà số cặp dưới ngưỡng thì đông.
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

        /// Bán kính của hộp SAU KHI gộp: khoảng cách từ màu trung bình mới tới thành viên
        /// xa nhất. Đây là thứ nói được "hộp này còn là MỘT màu hay đã thành một vệt".
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

        /// Hộp đáng cắt nhất. -1 khi không hộp nào còn cắt được (mọi hộp chỉ còn một màu).
        ///
        /// Chấm điểm bằng ĐỘ TRẢI NHÂN SỐ PIXEL, không chỉ độ trải.
        ///
        /// Chỉ xét độ trải thì một hộp chứa dăm pixel màu pha vương vãi ở mép hình — vốn
        /// trải rất rộng vì nó nối hai vùng màu khác nhau — sẽ liên tục giành nhát cắt khỏi
        /// những hộp thật sự chiếm diện tích tranh. Ảnh càng nhiễu, nó càng ăn nhiều nhát,
        /// và bảng màu càng tiêu tốn ô cho những màu không ai nhìn thấy.
        ///
        /// Nhân thêm số pixel là cách median cut chuẩn cân hai thứ đó.
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

        /// Dời điểm cắt tới RANH GIỚI GIÁ TRỊ gần giữa nhất.
        ///
        /// Cắt thẳng ở chỉ số giữa thì những màu TRÙNG NHAU nằm vắt qua điểm cắt bị chia
        /// sang hai hộp, và hai hộp đó cho ra cùng một màu trung bình — tức hai ô bảng màu
        /// trùng nhau. Người dùng xin 32 màu nhưng chỉ nhận về chừng 28 màu dùng được, mà
        /// không có gì báo.
        ///
        /// Ranh giới luôn tồn tại vì bên gọi đã bỏ qua hộp có độ trải bằng 0 — nghĩa là
        /// trên trục này chắc chắn có ít nhất hai giá trị khác nhau.
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
