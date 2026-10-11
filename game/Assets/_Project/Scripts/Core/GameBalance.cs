using UnityEngine;

namespace Cafe
{
    /// Hằng số cân bằng Tier 1. Nguồn: docs/economy_monetization.md (§2.3, §3.2, §3.3, §4).
    public static class GameBalance
    {
        public const int StartXu = 500;
        public const int StartCups = 6;
        public const int SidewalkFee = 60;
        public const int ScrapXu = 1; // ve chai khi thu ly

        // Đồng hồ: 210 giây thực = 06:00 -> 22:00
        public const float DayRealSeconds = 210f;
        public const float OpenHour = 6f;
        public const float CloseHour = 22f;
        public const float InGameSecondsPerRealSecond = (CloseHour - OpenHour) * 3600f / DayRealSeconds;

        // Khách
        public const float PatienceSeconds = 60f;
        public const float NoSeatSeconds = 10f;
        public const float DrinkMinSeconds = 8f;
        public const float DrinkMaxSeconds = 15f;
        public const float WalkAwayRp = -2f;
        public const float SpawnStartFraction = 0.02f;
        public const float SpawnEndFraction = 0.80f;
        public const float SeatTurnsPerDay = 4f;

        // Ly để lại (đơn vị: giây TRONG GAME, tức theo đồng hồ DayCycle)
        // Ghế bị chặn khi ly để quá 30 giây THỰC (economy §3.3), quy ra đồng hồ in-game
        public const float LeftoverBlockInGameSeconds = 30f * InGameSecondsPerRealSecond;
        public const float LeftoverFullDecayInGameSeconds = 7200f;

        // Dọn dẹp (T2+, Scripts/Cleaning; economy §5)
        public const float CleanSecondsOwner = 1.2f;          // chủ quán lau một bàn
        public const float WashSecondsPerCup = 0.8f;          // rửa một ly ở bồn
        public const float MopSecondsOwner = 2.0f;            // lau sàn một lượt
        public const float MopFloorDirtReduce = 8f;
        public const float FloorDirtPerCustomer = 0.6f;
        public const float FloorDirtMopChip = 15f;            // nút "Lau sàn" hiện khi FloorDirt vượt ngưỡng
        public const float FloorDirtDailyDecay = 10f;         // sang ngày mới FloorDirt = max(0, FloorDirt - 10)
        public const float CiDirtyTableWeight = 40f;
        public const float CiFloorDirtWeight = 0.4f;
        public const float OldTableRealSeconds = 26f;         // bàn bẩn quá lâu (~2 giờ in-game)
        public const float OldTableCiPenalty = 5f;
        public const float CiWalkAwayBelow = 60f;
        public const float CiWalkAwayPer10 = 0.15f;           // +15% bỏ đi mỗi 10 CI dưới 60
        public const float CiTipMultiplier = 0.5f;
        public const float CiRpPenaltyBelow = 40f;
        public const float CiRpPenaltyPerInGameHour = 1f;
        public const float SeatTableLinkMeters = 1.2f;        // ghế thuộc bàn gần nhất trong bán kính này
        public const string SinkFurnitureId = "Sink_Counter";

        // Lưới
        public const float CellSize = 0.5f;
        public const float FRefTier1 = 20f;
        public const float FRefTier2 = 60f;
        public const float FRefTier3 = 150f;

        // ---- Cấp 2: Thuê mặt bằng (economy §3.2, §3.4) ----
        public const int Tier2StarRP = 300;                   // ★2
        public const int Tier2Deposit = 18000;                // tiền cọc (hoàn khi lên cấp 3)
        public const int Tier2RentPerWeek = 7000;
        public const int RentIntervalDays = 7;                // trừ thuê khi bộ đếm ngày chạm 7
        public const int Tier2DailyFixedCost = 150;           // điện nước + vận hành mỗi ngày (thay phí vỉa hè 60)
        public const float DebtRevenueShare = 0.30f;          // thiếu tiền thuê: trừ 30% doanh thu mỗi ngày vào nợ
        public const float Tier2DemandMult = 1.5f;
        public const float Tier3DemandMult = 2.6f;
        public const float SeatTurnsTier2 = 4.5f;
        public const float SeatTurnsTier3 = 5f;
        public const float StaffCapacityBonusTier2 = 0.25f;   // +25% sức chứa mỗi nhân viên
        public const float StaffCapacityBonusTier3 = 0.15f;

        // Nhân viên Cấp 2 (economy_monetization §5–6)
        public const int StaffCleanerHireFee = 1500;
        public const int StaffServerHireFee = 1500;
        public const int StaffCleanerDailyWage = 300;
        public const int StaffServerDailyWage = 350;
        public const int StaffMaxLevel = 5;

        public static int StaffHireFee(StaffRole role)
        {
            return role == StaffRole.Cleaner ? StaffCleanerHireFee : StaffServerHireFee;
        }

        public static int StaffDailyWage(StaffRole role)
        {
            return role == StaffRole.Cleaner ? StaffCleanerDailyWage : StaffServerDailyWage;
        }

        /// Cost(Lv) = 1.000 × 2,5^(Lv−1); currentLevel là cấp trước khi nâng.
        public static int StaffUpgradeCost(int currentLevel)
        {
            int level = Mathf.Clamp(currentLevel, 1, StaffMaxLevel - 1);
            return Mathf.RoundToInt(1000f * Mathf.Pow(2.5f, level - 1));
        }

        // Hạng sao theo RP: ★1..★5
        public static readonly int[] StarThresholds = { 0, 300, 4000, 40000, 120000 };

        /// Demand_T1 = 18 + 9(★-1) + 0.5·day
        public static int DemandT1(int stars, int day)
        {
            return Mathf.RoundToInt(18f + 9f * (stars - 1) + 0.5f * day);
        }

        /// Tổng % khách tăng thêm tối đa từ đồ hút khách (chậu cây, bảng menu...).
        public const int MaxAttractPercent = 40;

        /// Demand_T1 × (1 + clamp(Σattract, 0, MaxAttractPercent)/100)
        public static int DemandWithAttract(int stars, int day, int attractSum)
        {
            float mult = 1f + Mathf.Clamp(attractSum, 0, MaxAttractPercent) / 100f;
            return Mathf.RoundToInt(DemandT1(stars, day) * mult);
        }

        public static int Capacity(int seats)
        {
            return Mathf.RoundToInt(seats * SeatTurnsPerDay);
        }

        /// Nhu cầu theo cấp (economy §3.2): T1 = công thức gốc, T2 = 1,5x, T3 = 2,6x.
        public static int DemandFor(int tier, int stars, int day)
        {
            float mult = tier >= 3 ? Tier3DemandMult : (tier == 2 ? Tier2DemandMult : 1f);
            return Mathf.RoundToInt(DemandT1(stars, day) * mult);
        }

        /// DemandFor × (1 + clamp(Σattract)/100). tier = 1 cho kết quả y hệt DemandWithAttract(stars, day, attractSum).
        public static int DemandWithAttract(int tier, int stars, int day, int attractSum)
        {
            float mult = 1f + Mathf.Clamp(attractSum, 0, MaxAttractPercent) / 100f;
            return Mathf.RoundToInt(DemandFor(tier, stars, day) * mult);
        }

        /// Sức chứa/ngày: T1 = ghế x 4; T2 = chỗ x 4,5 x (1 + 0,25·NV); T3 = chỗ x 5 x (1 + 0,15·NV).
        public static int CapacityFor(int tier, int seats, int staffCount)
        {
            if (tier <= 1) return Capacity(seats);
            float turns = tier >= 3 ? SeatTurnsTier3 : SeatTurnsTier2;
            float staffBonus = tier >= 3 ? StaffCapacityBonusTier3 : StaffCapacityBonusTier2;
            return Mathf.RoundToInt(seats * turns * (1f + staffBonus * Mathf.Max(0, staffCount)));
        }

        /// F_ref theo cấp (economy §4.1): T1 20, T2 60, T3 150.
        public static float FRefFor(int tier)
        {
            return tier >= 3 ? FRefTier3 : (tier == 2 ? FRefTier2 : FRefTier1);
        }

        public static float QualityMult(int q)
        {
            if (q <= 1) return 0.85f;
            if (q == 2) return 1f;
            return 1.2f;
        }

        public static float TipChance(int q)
        {
            if (q <= 1) return 0.05f;
            if (q == 2) return 0.25f;
            return 0.5f;
        }

        public static float TipSize(int q)
        {
            if (q <= 1) return 0.05f;
            if (q == 2) return 0.10f;
            return 0.15f;
        }

        /// RP_khach = clamp(1.5 * (S - 0.4) / 0.3, -3, 3)
        public static float RpPerCustomer(float s)
        {
            return Mathf.Clamp(1.5f * (s - 0.4f) / 0.3f, -3f, 3f);
        }
    }
}
