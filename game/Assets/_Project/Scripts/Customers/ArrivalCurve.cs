using UnityEngine;

namespace Cafe
{
    /// Mật độ khách đến trong cửa sổ sinh khách [SpawnStart, SpawnEnd] của ngày: vào chậm đầu ngày, hai đỉnh cao điểm.
    /// Hàm thuần: Map(u, xs, xe) đổi vị trí đều u∈[0,1] trong đoạn con [xs,xe] của cửa sổ thành vị trí theo mật độ.
    public static class ArrivalCurve
    {
        private const int Bins = 64;
        private static float[] cdf; // cdf[i] = tích lũy tới biên i/Bins, chuẩn hóa về [0,1]

        public static float Density(float x)
        {
            float[] k = GameBalance.ArrivalDensity; // cặp (x, mật độ)
            x = Mathf.Clamp01(x);
            for (int i = 2; i < k.Length; i += 2)
            {
                if (x <= k[i])
                {
                    float t = (x - k[i - 2]) / Mathf.Max(1e-6f, k[i] - k[i - 2]);
                    return Mathf.Lerp(k[i - 1], k[i + 1], t);
                }
            }
            return k[k.Length - 1];
        }

        private static void Build()
        {
            var c = new float[Bins + 1];
            for (int i = 0; i < Bins; i++) c[i + 1] = c[i] + Density((i + 0.5f) / Bins);
            for (int i = 0; i <= Bins; i++) c[i] /= c[Bins];
            cdf = c;
        }

        public static float Cdf(float x)
        {
            if (cdf == null) Build();
            float p = Mathf.Clamp01(x) * Bins;
            int i = Mathf.Min((int)p, Bins - 1);
            return Mathf.Lerp(cdf[i], cdf[i + 1], p - i);
        }

        public static float Inverse(float c)
        {
            if (cdf == null) Build();
            c = Mathf.Clamp01(c);
            int lo = 0, hi = Bins;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cdf[mid] <= c) lo = mid; else hi = mid;
            }
            float span = cdf[lo + 1] - cdf[lo];
            float t = span > 1e-9f ? (c - cdf[lo]) / span : 0f;
            return (lo + t) / Bins;
        }

        public static float Map(float u, float xs, float xe)
        {
            xs = Mathf.Clamp01(xs);
            xe = Mathf.Clamp01(xe);
            if (xe <= xs) return xs;
            float c0 = Cdf(xs);
            float c1 = Cdf(xe);
            return Mathf.Clamp(Inverse(c0 + Mathf.Clamp01(u) * (c1 - c0)), xs, xe);
        }
    }

    /// Chặn sinh thêm khách khi quán đã nghẽn: nhiều khách đang chờ chỗ hoặc chờ món.
    public static class SpawnThrottle
    {
        public static bool ShouldHold(int waitingSeat, int waitingService, int seatCount)
        {
            int serviceLimit = Mathf.Max(GameBalance.ThrottleSeatWait, seatCount / 2);
            return waitingSeat >= GameBalance.ThrottleSeatWait || waitingService >= serviceLimit;
        }
    }
}
