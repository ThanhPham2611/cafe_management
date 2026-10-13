using System;

namespace Cafe
{
    /// Id máy móc cấp 3 (economy §3.5). Máy là nội thất đặt trong quán (FurnitureDef.machineId); có đặt trên lưới thì mới có tác dụng.
    public static class MachineIds
    {
        public const string Grinder = "grinder";       // Máy xay 2 họng  25.000
        public const string ColdBrew = "cold_brew";    // Máy cold brew   60.000
        public const string Espresso = "espresso";     // Máy espresso   150.000

        public static readonly string[] All = { Grinder, ColdBrew, Espresso };
    }

    /// Luật tác dụng của máy móc. Thuần logic (nhận hàm "có máy này không") để kiểm thử được.
    ///  - Máy xay: nhân viên Pha chế pha nhanh hơn 15%.
    ///  - Máy espresso: pha nhanh hơn 25% (cộng dồn với máy xay) và mở món đặc sản.
    ///  - Máy cold brew: mở món cold brew (ủ sẵn nên pha rất nhanh).
    ///  Thời gian "pha hoàn hảo" của chủ quán nới ra theo cùng hệ số: máy tốt thì dễ đạt 3★ hơn.
    public static class MachineRules
    {
        public const float GrinderTimeFactor = 0.85f;
        public const float EspressoTimeFactor = 0.75f;

        /// Hệ số nhân thời gian pha của nhân viên Pha chế (1 = không có máy tăng tốc).
        public static float BrewTimeFactor(Func<string, bool> has)
        {
            float f = 1f;
            if (has == null) return f;
            if (has(MachineIds.Grinder)) f *= GrinderTimeFactor;
            if (has(MachineIds.Espresso)) f *= EspressoTimeFactor;
            return f;
        }

        /// Thời gian "pha hoàn hảo" của chủ quán được nhân với số này (>= 1) khi có máy tăng tốc.
        public static float PerfectTimeSlack(float brewTimeFactor)
        {
            return 1f / Math.Max(0.5f, Math.Min(1f, brewTimeFactor));
        }

        public static string DisplayName(string id)
        {
            switch (id)
            {
                case MachineIds.Grinder: return "Máy xay 2 họng";
                case MachineIds.ColdBrew: return "Máy cold brew";
                case MachineIds.Espresso: return "Máy espresso";
                default: return "";
            }
        }

        /// Dòng mô tả ngắn trên thẻ cửa hàng.
        public static string Effect(string id)
        {
            switch (id)
            {
                case MachineIds.Grinder: return "Pha nhanh −15%";
                case MachineIds.ColdBrew: return "Mở món cold brew";
                case MachineIds.Espresso: return "Pha nhanh −25%";
                default: return "";
            }
        }
    }
}
