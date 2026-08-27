using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Sinh ảnh tham số của viên ngọc từ một JewelFacetProfile.
    ///
    /// Không dùng bộ vẽ đa giác nào cả: mỗi điểm lấy mẫu tự hỏi "tôi nằm ở mặt nào"
    /// bằng vài phép toán vector. Bát giác trong và bát giác ngoài đồng dạng qua tâm,
    /// nên đường chia giữa hai mặt kề nhau chính là tia từ tâm qua đỉnh — hỏi góc là
    /// biết mặt, không cần dựng hình.
    ///
    /// Chống răng cưa bằng cách lấy nhiều mẫu trong một pixel rồi lấy trung bình. Trung
    /// bình được vì ba kênh là SỐ: nửa pixel nằm ở mặt bàn nửa ở mặt bên thì bộ số
    /// trung bình cho ra đúng màu trung bình. Nếu ảnh mang CHỈ SỐ mặt thay vì bộ số
    /// thì không có phép trung bình nào đúng, và đó là lý do file này tồn tại thay vì
    /// đẩy chín bộ số thành property của material.
    public static class JewelFacetMap
    {
        /// Thang mã hoá độ rực trong kênh R.
        /// PHẢI KHỚP SAT_MIN/SAT_MAX trong JewelFacets.shader.
        public const float SaturationMin = -1f;
        public const float SaturationMax = 3f;

        private const float Cut = 0.29f;          // cạnh bị cắt bao nhiêu phần
        private const float Half = 0.455f;        // nửa bề ngang so với khung ảnh
        private const float TableScale = 0.53f;   // mặt bàn = bát giác ngoài thu nhỏ
        private const float Round = 0.055f;       // bo góc

        // Bề rộng NỬA của các nét, tính theo khung ảnh 1.0.
        private const float SeamHalf = 1f / 256f;
        private const float RimHalf = 1.5f / 256f;

        private static readonly Vector2[] Corners = BuildCorners();

        /// Trả về mảng pixel theo thứ tự của Texture2D: dòng 0 ở DƯỚI cùng.
        public static Color32[] Build(JewelFacetProfile profile, int resolution, int supersample)
        {
            var pixels = new Color32[resolution * resolution];
            var samples = Mathf.Max(1, supersample);
            var step = 1f / (resolution * samples);
            var perPixel = samples * samples;

            var outer = Offset(Corners, Half, Round);
            var inner = Offset(Corners, Half * TableScale, Round * TableScale);
            var outlineHalf = profile.OutlineWidth * 0.5f / 256f;

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    float sumS = 0f, sumK = 0f, sumB = 0f, coverage = 0f;

                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var point = new Vector2(
                                (x * samples + sx + 0.5f) * step - 0.5f,
                                (y * samples + sy + 0.5f) * step - 0.5f);

                            var facet = Sample(profile, outer, inner, outlineHalf, point, out var inside);

                            sumS += facet.Saturation;
                            sumK += facet.Contrast;
                            sumB += facet.Brightness;

                            if (inside) coverage += 1f;
                        }
                    }

                    pixels[y * resolution + x] = Encode(
                        sumS / perPixel, sumK / perPixel, sumB / perPixel, coverage / perPixel);
                }
            }

            return pixels;
        }

        /// Bộ số tại một điểm. `inside` cho biết điểm có nằm trong hình bóng không —
        /// bộ số vẫn trả về cả khi ở ngoài, để pixel ở rìa lấy trung bình không hút
        /// phải số 0 và sinh ra một vành sáng quanh viên ngọc.
        private static ColorAdjustment Sample(
            JewelFacetProfile profile, Vector2[] outer, Vector2[] inner, float outlineHalf,
            Vector2 point, out bool inside)
        {
            var toOuter = RoundedDistance(outer, point, Round);
            inside = toOuter <= 0f;

            // Viền ngoài: nằm trong dải quanh biên, và cả phần tràn ra ngoài.
            if (toOuter > -outlineHalf) return profile.Outline;

            // Ánh hắt ở mép dưới của mặt bàn. Dùng lại bộ số của mặt đỉnh: đây là cùng
            // một nguồn sáng hắt lên, hạ độ loé của đỉnh mà vệt này không hạ theo thì
            // nó thành vệt trắng lơ lửng.
            if (DistanceToSegment(point, inner[4], inner[5]) < RimHalf) return profile.GetFacet(0);

            var toInner = RoundedDistance(inner, point, Round * TableScale);

            // Khe quanh mặt bàn.
            if (Mathf.Abs(toInner) < SeamHalf) return profile.Seam;

            if (toInner < 0f) return profile.Table;

            // Khe trên đường chia giữa hai mặt ngoài.
            if (DistanceToNearestDivider(point) < SeamHalf) return profile.Seam;

            return profile.GetFacet(SectorOf(point));
        }

        /// Khoảng cách có dấu tới bát giác đã bo góc. Âm là ở trong.
        ///
        /// Bo góc = co đa giác vào `radius` rồi nở ngược ra `radius`. `polygon` truyền
        /// vào đã là bản co sẵn, nên chỉ còn trừ đi bán kính.
        private static float RoundedDistance(Vector2[] polygon, Vector2 point, float radius)
        {
            return ConvexDistance(polygon, point) - radius;
        }

        private static float ConvexDistance(Vector2[] polygon, Vector2 point)
        {
            var nearest = float.MaxValue;
            var inside = true;

            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];

                nearest = Mathf.Min(nearest, DistanceToSegment(point, a, b));

                // Đa giác xếp theo chiều kim đồng hồ trong hệ trục y hướng lên.
                if (Cross(b - a, point - a) > 0f) inside = false;
            }

            return inside ? -nearest : nearest;
        }

        /// Mặt nào chứa điểm này.
        ///
        /// Bát giác trong là bát giác ngoài thu nhỏ QUA TÂM, nên hai đỉnh tương ứng
        /// nằm cùng một tia từ tâm. Đường chia giữa hai mặt kề nhau chính là tia đó,
        /// và việc "điểm nằm ở mặt nào" rút gọn thành "điểm nằm giữa hai tia nào".
        private static int SectorOf(Vector2 point)
        {
            for (var i = 0; i < Corners.Length; i++)
            {
                var a = Corners[i];
                var b = Corners[(i + 1) % Corners.Length];

                if (Cross(a, point) <= 0f && Cross(b, point) >= 0f) return i;
            }

            return 0;
        }

        private static float DistanceToNearestDivider(Vector2 point)
        {
            var nearest = float.MaxValue;

            foreach (var corner in Corners)
            {
                var direction = corner.normalized;

                // Chỉ tính nửa tia chứa điểm, không tính nửa đối diện bên kia tâm.
                if (Vector2.Dot(direction, point) <= 0f) continue;

                nearest = Mathf.Min(nearest, Mathf.Abs(Cross(direction, point)));
            }

            return nearest;
        }

        private static Color32 Encode(float saturation, float contrast, float brightness, float alpha)
        {
            return new Color32(
                ToByte(Mathf.InverseLerp(SaturationMin, SaturationMax, saturation)),
                ToByte(contrast * 0.5f + 0.5f),
                ToByte(brightness + 0.5f),
                ToByte(alpha));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        /// Tám đỉnh của bát giác gốc trong hệ -1..1, xếp theo chiều kim đồng hồ từ
        /// đỉnh trên-trái. y hướng LÊN, nên cạnh 0 là cạnh trên cùng.
        private static Vector2[] BuildCorners()
        {
            var k = Cut * 2f;

            return new[]
            {
                new Vector2(-1f + k, 1f),
                new Vector2(1f - k, 1f),
                new Vector2(1f, 1f - k),
                new Vector2(1f, -1f + k),
                new Vector2(1f - k, -1f),
                new Vector2(-1f + k, -1f),
                new Vector2(-1f, -1f + k),
                new Vector2(-1f, 1f - k),
            };
        }

        /// Bát giác đã nhân tỉ lệ và co vào `radius` để chừa chỗ bo góc.
        private static Vector2[] Offset(Vector2[] corners, float scale, float radius)
        {
            var count = corners.Length;
            var normals = new Vector2[count];
            var offsets = new float[count];

            for (var i = 0; i < count; i++)
            {
                var a = corners[i] * scale;
                var b = corners[(i + 1) % count] * scale;
                var edge = (b - a).normalized;

                // Pháp tuyến hướng vào trong với đa giác xếp theo chiều kim đồng hồ.
                normals[i] = new Vector2(edge.y, -edge.x);
                offsets[i] = Vector2.Dot(a, normals[i]) + radius;
            }

            var result = new Vector2[count];

            for (var i = 0; i < count; i++)
            {
                var previous = (i + count - 1) % count;
                result[i] = LineIntersection(normals[previous], offsets[previous], normals[i], offsets[i]);
            }

            return result;
        }

        private static Vector2 LineIntersection(Vector2 n0, float d0, Vector2 n1, float d1)
        {
            var determinant = n0.x * n1.y - n0.y * n1.x;

            // Hai cạnh song song thì không có giao điểm; bát giác không rơi vào đây,
            // nhưng trả về gốc còn hơn trả về vô cực rồi hỏng cả ảnh.
            if (Mathf.Abs(determinant) < 1e-6f) return Vector2.zero;

            return new Vector2(
                (d0 * n1.y - d1 * n0.y) / determinant,
                (n0.x * d1 - n1.x * d0) / determinant);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var lengthSquared = edge.sqrMagnitude;

            if (lengthSquared < 1e-12f) return Vector2.Distance(point, a);

            var t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / lengthSquared);

            return Vector2.Distance(point, a + edge * t);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
