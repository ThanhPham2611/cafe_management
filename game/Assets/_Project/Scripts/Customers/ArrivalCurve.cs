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

    /// Nhịp khách theo sức phục vụ (docs/customer_pacing_plan_2026-10-19.md §4): khách đến hạn chỉ vào quán khi số khách
    /// chưa được phục vụ dưới mức tải L, tính từ tốc độ phục vụ đo được (muHat, khách/giây). Người chơi pha chậm thì khách
    /// vào thưa hơn; quán gần trống thì kéo khách kế tới sớm để không phải ngồi chờ.
    public static class ArrivalPacer
    {
        /// Tốc độ phục vụ giả định lúc đầu ngày / đổi cấp (khách/giây) cho cấp 1..3: ~5,5 / 15 / 20 khách/phút (economy §17-18, §24.4).
        private static readonly float[] MuPrior = { 0.09f, 0.25f, 0.33f };
        public const float WaitTarget = 25f;          // hàng chờ dự kiến ≲ 25 s (42% của kiên nhẫn 60 s)
        public const int WalkBuffer = 2;              // khách đang đi bộ chưa vào hàng
        public const int MinLoad = 3;                 // không để quán trống
        public const float HoldMax = 30f;             // đợi ngoài cửa quá lâu thì đi chỗ khác (không tính mất khách, không trừ RP)
        public const int PendingMax = 3;              // tối đa khách xếp ngoài cửa
        public const float PullForwardSeconds = 8f;   // quán gần trống: khách kế còn ≤ 8 s thì cho vào luôn
        public const float EwmaTau = 45f;

        public static float Prior(int tier)
        {
            return MuPrior[Mathf.Clamp(tier, 1, MuPrior.Length) - 1];
        }

        /// Số khách chưa được phục vụ tối đa trong quán.
        public static int LoadLimit(float mu, int seatCount)
        {
            int cap = Mathf.Max(MinLoad, Mathf.CeilToInt(0.6f * seatCount));
            return Mathf.Clamp(Mathf.CeilToInt(mu * WaitTarget) + WalkBuffer, MinLoad, cap);
        }

        /// Khoảng cách tối thiểu giữa hai khách vào quán (giây, chưa nhân nhiễu).
        public static float MinGap(float mu)
        {
            return Mathf.Clamp(0.6f / Mathf.Max(mu, 0.01f), 1.5f, 8f);
        }

        /// EWMA tốc độ phục vụ: served = số khách phục vụ xong trong dt. Chỉ gọi khi quán đang bận (tránh tụt vì nhàn rỗi);
        /// kẹp trong [0,5; 3] × prior để một đoạn xui không kéo nhịp xuống mãi.
        public static float UpdateMu(float mu, float prior, int served, float dt)
        {
            mu += (served - mu * dt) / EwmaTau;
            return Mathf.Clamp(mu, 0.5f * prior, 3f * prior);
        }
    }
}
