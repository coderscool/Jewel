using JewelPainter.Gameplay.Domain;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Sinh ảnh tham số của viên ngọc từ một JewelFacetProfile.
    public static class JewelFacetMap
    {
        public const float SaturationMin = -1f;
        public const float SaturationMax = 3f;

        private const float Cut = 0.29f;
        private const float Half = 0.455f;
        private const float TableScale = 0.53f;
        private const float Round = 0.055f;

        private const float SeamHalf = 1f / 256f;
        private const float RimHalf = 1.5f / 256f;

        private static readonly Vector2[] Corners = BuildCorners();

        /// Sinh mảng pixel của ảnh tham số.
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

        /// Bộ số tại một điểm.
        private static ColorAdjustment Sample(
            JewelFacetProfile profile, Vector2[] outer, Vector2[] inner, float outlineHalf,
            Vector2 point, out bool inside)
        {
            var toOuter = RoundedDistance(outer, point, Round);
            inside = toOuter <= 0f;

            if (toOuter > -outlineHalf) return profile.Outline;

            if (DistanceToSegment(point, inner[4], inner[5]) < RimHalf) return profile.GetFacet(0);

            var toInner = RoundedDistance(inner, point, Round * TableScale);

            if (Mathf.Abs(toInner) < SeamHalf) return profile.Seam;

            if (toInner < 0f) return profile.Table;

            if (DistanceToNearestDivider(point) < SeamHalf) return profile.Seam;

            return profile.GetFacet(SectorOf(point));
        }

        /// Khoảng cách có dấu tới bát giác đã bo góc.
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

                if (Cross(b - a, point - a) > 0f) inside = false;
            }

            return inside ? -nearest : nearest;
        }

        /// Mặt nào chứa điểm này.
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

        /// Tám đỉnh của bát giác gốc trong hệ -1..1, xếp theo chiều kim đồng hồ từ đỉnh trên-trái.
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
