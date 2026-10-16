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
