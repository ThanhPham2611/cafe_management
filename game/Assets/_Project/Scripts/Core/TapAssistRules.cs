using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Dung sai chạm theo màn hình: chọn điểm gần điểm chạm nhất trong bán kính (pixel). Hàm thuần để test.
    public static class TapAssistRules
    {
        public const float MinRadiusPx = 56f;
        public const float RadiusScreenFraction = 0.06f;

        public static float RadiusPx(float screenHeight)
        {
            return Mathf.Max(MinRadiusPx, screenHeight * RadiusScreenFraction);
        }

        /// Như Nearest nhưng khi nhiều điểm cùng nằm sát nhau trên màn hình (nhìn ngang: ly ở nhiều độ sâu chồng lên nhau) thì chọn điểm
        /// gần camera hơn: trong dải TieBandPx quanh khoảng cách nhỏ nhất, lấy depth nhỏ nhất.
        public static int Nearest(Vector2 tap, IList<Vector2> points, float radius, IList<float> depths)
        {
            int best = Nearest(tap, points, radius);
            if (best < 0 || depths == null) return best;
            float bestDist = (points[best] - tap).magnitude;
            int pick = best;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - tap).magnitude;
                if (d > radius || d > bestDist + TieBandPx) continue;
                if (depths[i] < depths[pick] - 0.01f) pick = i;
            }
            return pick;
        }

        public const float TieBandPx = 16f;

        /// Chỉ số điểm gần `tap` nhất có khoảng cách <= radius; -1 nếu không có. Hòa thì lấy chỉ số nhỏ.
        public static int Nearest(Vector2 tap, IList<Vector2> points, float radius)
        {
            int best = -1;
            float bestSqr = radius * radius;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - tap).sqrMagnitude;
                if (d <= bestSqr && (best < 0 || d < bestSqr))
                {
                    best = i;
                    bestSqr = d;
                }
            }
            return best;
        }
    }
}
