using System;
using UnityEngine;

namespace Cafe
{
    /// Cổng bật/tắt luật dọn dẹp mới (bàn bẩn, bồn rửa, chỉ số Sạch sẽ CI): chỉ áp dụng từ cấp quán >= 2.
    /// Tier 1 giữ nguyên luồng cũ (LeftoverCup, thu ly 1 chạm).
    ///
    /// Tích hợp: gán TierProvider một lần khi dựng game, vd. trong GameBootstrap.Build():
    ///     CleaningRules.TierProvider = () => Tier != null ? Tier.Current : 1;
    /// Cờ -forceclean (DebugAutoPilot) đặt ForceEnabled = true để thử luật dọn trong cảnh T1.
    public static class CleaningRules
    {
        /// Số tier hiện tại (mặc định luôn 1 -> tắt). Integrator gán.
        public static Func<int> TierProvider = DefaultTier;

        /// Bật cứng (test -forceclean), bất kể TierProvider.
        public static bool ForceEnabled;

        public static bool Enabled
        {
            get
            {
                if (ForceEnabled) return true;
                Func<int> p = TierProvider;
                return p != null && p() >= 2;
            }
        }

        private static int DefaultTier()
        {
            return 1;
        }

        /// CI hiện tại (0..100); 100 nếu hệ thống chưa dựng hoặc luật chưa bật.
        public static float CurrentCI
        {
            get
            {
                CleaningSystem s = CleaningSystem.Instance;
                return (s != null && s.Model != null && Enabled) ? s.Model.CI : 100f;
            }
        }

        /// Hệ số tip theo CI (×0,5 khi CI < 60); 1 khi luật dọn tắt.
        public static float TipMultiplier
        {
            get
            {
                CleaningSystem s = CleaningSystem.Instance;
                return (s != null && s.Model != null && Enabled) ? s.Model.TipMultiplier : 1f;
            }
        }

        /// Xác suất khách bỏ đi khi tới ghế vì quán bẩn (0 khi luật dọn tắt).
        public static float WalkAwayChance
        {
            get
            {
                CleaningSystem s = CleaningSystem.Instance;
                return (s != null && s.Model != null && Enabled) ? s.Model.WalkAwayChance : 0f;
            }
        }

        /// Icon cây chổi; chưa có sprite thì dùng icon ly.
        public static Sprite BroomIcon(UiTheme t)
        {
            if (t == null) return null;
            return t.iconBroom != null ? t.iconBroom : t.iconCup;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TierProvider = DefaultTier;
            ForceEnabled = false;
        }
    }
}
