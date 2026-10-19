using UnityEngine;

namespace Cafe
{
    /// Hằng số cân bằng Tier 1. Nguồn: docs/economy_monetization.md (§2.3, §3.2, §3.3, §4).
    public static class GameBalance
    {
        public const int StartXu = 500;
        public const int StartCups = 6;
        /// Ly trên mỗi ghế. Một ly đi hết vòng pha -> mang ra -> uống -> nằm trên bàn bẩn -> dọn -> rửa mất ~45 s,
        /// nên với 6 ly cố định thông lượng chỉ ~27 ly/ngày dù có thêm ghế hay nhân viên (autoplay cấp 2 xác nhận).
        public const float CupsPerSeatTier1 = 1f;
        public const float CupsPerSeatTier2 = 1.5f;

        /// Tổng số ly của quán theo cấp và số ghế (không bao giờ ít hơn StartCups).
        public static int CupsFor(int tier, int seats)
        {
            float perSeat = tier >= 2 ? CupsPerSeatTier2 : CupsPerSeatTier1;
            return Mathf.Max(StartCups, Mathf.CeilToInt(Mathf.Max(0, seats) * perSeat));
        }
        public const int SidewalkFee = 60;
        public const int ScrapXu = 1; // ve chai khi thu ly

        // Đồng hồ: 210 giây thực = 06:00 -> 22:00
        public const float DayRealSeconds = 210f;
        public const float OpenHour = 6f;
        public const float CloseHour = 22f;
        public const float InGameSecondsPerRealSecond = (CloseHour - OpenHour) * 3600f / DayRealSeconds;

        /// Ngày ở nhà hàng (cấp 3) dài 300 giây thực. Đo bằng autoplay: một lượt khách (đi bộ + chờ ly + uống + chờ lau bàn) mất ~55 giây,
        /// nên cửa sổ sinh khách 165 giây chỉ quay vòng ~3,2 khách/ghế (công thức dùng 5); 300 giây đưa con số này về ~4,5.
        public const float Tier3DayRealSeconds = 300f;

        /// Độ dài một ngày (giây thực) theo cấp quán.
        public static float DayRealSecondsFor(int tier)
        {
            return tier >= 3 ? Tier3DayRealSeconds : DayRealSeconds;
        }

        /// Giây in-game trôi qua mỗi giây thực theo cấp quán.
        public static float InGameSecondsPerRealSecondFor(int tier)
        {
            return InGameSecondsPerRealSecondForLength(DayRealSecondsFor(tier));
        }

        /// Giây in-game trôi qua mỗi giây thực của một ngày dài lengthSeconds giây thực (06:00 -> 22:00).
        public static float InGameSecondsPerRealSecondForLength(float lengthSeconds)
        {
            return (CloseHour - OpenHour) * 3600f / Mathf.Max(1f, lengthSeconds);
        }

        // Khách
        public const float PatienceSeconds = 60f;
        public const float NoSeatSeconds = 20f;
        public const float DrinkMinSeconds = 8f;
        public const float DrinkMaxSeconds = 15f;
        public const float WalkAwayRp = -1f;                  // bỏ về vì chờ món quá lâu / quán bẩn: nhẹ hơn 1 khách hài lòng (~+1,5 RP); GDD §3.2 "mất danh tiếng nhẹ"
        public const float WalkAwayRpNoSeat = 0f;             // hết chỗ = quán đắt khách, không phải khách bực: không trừ RP (economy §21). Cấp 3 bot mất ~180 khách/ngày vì hết chỗ nên -1 triệt tiêu hết RP kiếm được
        public const float SpawnStartFraction = 0.02f;
        public const float SpawnEndFraction = 0.80f;
        public const float SeatTurnsPerDay = 4f;

        // Nhịp khách đến (economy §24): vào chậm đầu ngày, hai đỉnh cao điểm; chặn sinh thêm khi quán nghẽn
        // Cặp (vị trí trong cửa sổ sinh khách 0..1, mật độ tương đối)
        public static readonly float[] ArrivalDensity = { 0f, 0.35f, 0.15f, 1f, 0.4f, 1.5f, 0.6f, 0.8f, 0.8f, 1.6f, 1f, 0.5f };
        public const int ThrottleSeatWait = 3;                // >= 3 khách đang tìm chỗ => chưa sinh thêm
        public const float ThrottleMaxDelay = 45f;            // tổng thời gian lịch có thể bị dời trong một ngày
        public const float MinSpawnGapSeconds = 1f;           // hai khách liên tiếp cách nhau ít nhất, tránh bùng nổ khi hết nghẽn

        // Ly để lại (đơn vị: giây TRONG GAME, tức theo đồng hồ DayCycle)
        // Ghế bị chặn khi ly để quá 30 giây THỰC (economy §3.3), quy ra đồng hồ in-game
        public const float LeftoverBlockRealSeconds = 30f;
        public const float LeftoverBlockInGameSeconds = LeftoverBlockRealSeconds * InGameSecondsPerRealSecond;
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
        // F = (Σ comfort + Σ style) / F_ref cộng trên MỌI món đã đặt (cả ghế, bàn). Với 60 thì bố cục mặc định cấp 2 (~61 điểm)
        // đã đạt F = 1 ngay. Đo bằng autoplay: bố cục mặc định ~61, thêm ~8 ghế + 3 bàn ~116, thêm bộ trang trí ~160 điểm,
        // nên 200 giữ F tăng đều suốt cấp 2 (trang trí còn giá trị) mà vẫn đạt 1 khi quán đã đầy đồ.
        public const float FRefTier2 = 200f;
        public const float FRefTier3 = 400f;

        // ---- Cấp 2: Thuê mặt bằng (economy §3.2, §3.4) ----
        public const int Tier2StarRP = 300;                   // ★2
        public const int Tier2Deposit = 18000;                // tiền cọc (hoàn khi lên cấp 3)
        public const int Tier2RentPerWeek = 5000;             // ~10-16% doanh thu cấp 2 đo được (4.400-6.900/ngày)
        public const int RentIntervalDays = 7;                // trừ thuê khi bộ đếm ngày chạm 7
        public const int Tier2DailyFixedCost = 150;           // điện nước + vận hành mỗi ngày (thay phí vỉa hè 60)
        public const float DebtRevenueShare = 0.30f;          // thiếu tiền thuê: trừ 30% doanh thu mỗi ngày vào nợ
        // ---- Cấp 3: Mua nhà hàng (economy §3.1, §3.5) ----
        public const int Tier3StarRP = 3000;                  // ★3 (cấp 2 đo được ~57 RP/ngày: ~45-55 ngày thay vì ~70)
        public const int Tier3Price = 140000;                 // cọc cấp 2 được hoàn: thực trả Tier3Price - cọc. Lãi cấp 2 đo ~2.100/ngày: ~60 ngày tiết kiệm
        public const int Tier3DailyFixedCost = 700;           // vận hành nhà hàng/ngày; hết tiền thuê

        public const int Tier3OpeningGift = 10000;            // quà khai trương: vào cấp 3 gần như hết tiền mà lương 8 nhân viên ~2.950/ngày

        /// RP khi hoàn thành chương truyện khách quen thứ `chapterIndex` (0-based): 60 / 80 / 100.
        public static int StoryChapterRp(int chapterIndex)
        {
            return 60 + 20 * Mathf.Max(0, chapterIndex);
        }

        // ---- Các khu của nhà hàng (economy §3.5, phase3_spec 3.2) ----
        public const int ZoneGardenCost = 40000;              // sân vườn
        public const int ZoneTerraceCost = 90000;             // lầu (view)
        public const int ZoneVipCost = 180000;                // VIP
        public const float ZoneDemandBonus = 0.20f;           // mỗi khu đã mở: +20% khách (Demand_T3 "+ zone bonus")
        public const float VipPriceMult = 1.5f;               // khách ngồi khu VIP trả 1,5x (đơn đặc biệt 1,5–2x, economy §2)
        public const float FRefZone = 150f;                   // F_ref riêng mỗi khu phụ (sảnh chính giữ FRefTier3)
        public const int ZoneStarterSets = 2;                 // bộ bàn ghế tặng khi mở khu

        /// Nhân nhu cầu theo số khu phụ đã mở: 1 + 0,2 mỗi khu.
        public static float ZoneDemandMult(int openZones)
        {
            return 1f + ZoneDemandBonus * Mathf.Max(0, openZones);
        }

        public const float Tier2DemandMult = 1.5f;
        public const float Tier3DemandMult = 2.6f;
        public const float SeatTurnsTier2 = 4.5f;
        public const float SeatTurnsTier3 = 5f;               // sức chứa gấp ~5 lần thực phục vụ, nhưng cố ý để vậy: hạ xuống làm bot phục vụ ít hơn ~12%, lãi ít hơn ~25% (economy §21)
        public const float StaffCapacityBonusTier2 = 0.25f;   // +25% sức chứa mỗi nhân viên
        public const float StaffCapacityBonusTier3 = 0.15f;

        // Nhân viên Cấp 2 (economy_monetization §5–6)
        public const int StaffCleanerHireFee = 1500;
        public const int StaffServerHireFee = 1500;
        public const int StaffBrewerHireFee = 2500;
        public const int StaffCleanerDailyWage = 300;
        public const int StaffServerDailyWage = 350;
        public const int StaffBrewerDailyWage = 450;
        public const int StaffMaxLevel = 5;

        /// Mỗi khu phụ đã mở nới thêm 1 chỗ cho MỖI vai trò (nhà hàng cấp 3): sảnh 3 Dọn dẹp + 2 Phục vụ + 3 Pha chế = 8,
        /// đủ 3 khu 6 + 5 + 6 = 17. Đo bằng bot: với trần cũ (8 người) mở khu làm khách mất vì hết kiên nhẫn tăng 13 -> 39/ngày
        /// vì thông lượng Pha chế / Phục vụ là giới hạn, nên nhu cầu +20% mỗi khu phải đi cùng thêm người làm.
        public const int StaffSlotsPerZone = 1;

        // ---- Quầy pha chế phụ + Quầy line (cấp 3, docs/stations_spec.md) ----
        /// Pha chế có chỗ ở quầy pha phụ: thời gian pha × 0,85 (người không có chỗ giữ nguyên, save cũ không chậm đi).
        public const float BrewSpotFactor = 0.85f;
        /// Mỗi quầy pha phụ nới thêm 1 chỗ thuê Pha chế (đủ 3 khu: 6 -> 8 với 2 quầy).
        public const int StaffSlotsPerBrewStation = 1;
        /// Tối đa quầy pha phụ được tính (khớp maxCount = 2 của FurnitureDef).
        public const int BrewStationMaxCount = 2;
        /// Sức chứa gốc của kệ giao (ReadyShelf); mỗi quầy line cộng thêm shelfSlots (4).
        public const int ReadyShelfBaseCapacity = 6;

        /// Số nhân viên tối đa mỗi vai trò theo cấp quán (tổng cấp 2 = 2, cấp 3 sảnh = 8 và +3 mỗi khu phụ đã mở; economy §3.1).
        /// Đo bằng autoplay ở cấp 2 (14–16 ghế, 9 ngày): người Dọn dẹp thứ hai và Pha chế đều KHÔNG tăng số khách (~40/ngày dù có hay
        /// không) mà tốn thêm lương, nên cấp 2 chỉ 1 Dọn dẹp + 1 Phục vụ; người thứ hai và Pha chế mở từ cấp 3 (sảnh lớn, nhiều khách).
        /// brewStations: số quầy pha phụ đang đặt (docs/stations_spec.md §1): mỗi quầy +1 chỗ Pha chế, chỉ ở cấp 3, tối đa BrewStationMaxCount.
        public static int StaffMaxCount(StaffRole role, int tier, int openZones = 0, int brewStations = 0)
        {
            if (tier < 2) return 0;
            bool restaurant = tier >= 3;
            int extra = restaurant ? StaffSlotsPerZone * Mathf.Max(0, openZones) : 0;
            int stations = restaurant ? StaffSlotsPerBrewStation * Mathf.Clamp(brewStations, 0, BrewStationMaxCount) : 0;
            switch (role)
            {
                case StaffRole.Server: return (restaurant ? 2 : 1) + extra;
                case StaffRole.Brewer: return (restaurant ? 3 : 0) + extra + stations;
                default: return (restaurant ? 3 : 1) + extra;
            }
        }

        public static int StaffHireFee(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return StaffServerHireFee;
                case StaffRole.Brewer: return StaffBrewerHireFee;
                default: return StaffCleanerHireFee;
            }
        }

        public static int StaffDailyWage(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return StaffServerDailyWage;
                case StaffRole.Brewer: return StaffBrewerDailyWage;
                default: return StaffCleanerDailyWage;
            }
        }

        /// Cost(Lv) = 1.000 × 2,5^(Lv−1); currentLevel là cấp trước khi nâng.
        public static int StaffUpgradeCost(int currentLevel)
        {
            int level = Mathf.Clamp(currentLevel, 1, StaffMaxLevel - 1);
            return Mathf.RoundToInt(1000f * Mathf.Pow(2.5f, level - 1));
        }

        /// RP cần để mua cấp `toTier` (2 hoặc 3).
        public static int UpgradeStarRP(int toTier)
        {
            return toTier >= 3 ? Tier3StarRP : Tier2StarRP;
        }

        /// Xu phải trả ngay khi lên cấp `toTier`: cấp 2 = tiền cọc; cấp 3 = giá nhà trừ cọc đã đặt (được hoàn).
        public static int UpgradeCost(int toTier, int deposit)
        {
            return toTier >= 3 ? Mathf.Max(0, Tier3Price - Mathf.Max(0, deposit)) : Tier2Deposit;
        }

        public static int FixedCostFor(int tier)
        {
            return tier >= 3 ? Tier3DailyFixedCost : (tier == 2 ? Tier2DailyFixedCost : SidewalkFee);
        }

        // Hạng sao theo RP: ★1..★5
        public static readonly int[] StarThresholds = { 0, Tier2StarRP, Tier3StarRP, 12000, 40000 };   // ★4 / ★5 hạ từ 40.000 / 120.000 (economy §21)

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

        /// DemandFor × (1 + clamp(Σattract)/100) × zoneMult. tier = 1, zoneMult = 1 cho kết quả y hệt DemandWithAttract(stars, day, attractSum).
        public static int DemandWithAttract(int tier, int stars, int day, int attractSum, float zoneMult = 1f)
        {
            float mult = 1f + Mathf.Clamp(attractSum, 0, MaxAttractPercent) / 100f;
            return Mathf.RoundToInt(DemandFor(tier, stars, day) * mult * Mathf.Max(0f, zoneMult));
        }

        /// Sức chứa/ngày: T1 = ghế x 4; T2 = chỗ x 4,5 x (1 + 0,25·NV); T3 = chỗ x 5 x (1 + 0,15·NV).
        public static int CapacityFor(int tier, int seats, int staffCount)
        {
            if (tier <= 1) return Capacity(seats);
            float turns = tier >= 3 ? SeatTurnsTier3 : SeatTurnsTier2;
            float staffBonus = tier >= 3 ? StaffCapacityBonusTier3 : StaffCapacityBonusTier2;
            return Mathf.RoundToInt(seats * turns * (1f + staffBonus * Mathf.Max(0, staffCount)));
        }

        /// F_ref theo cấp (economy §4.1): T1 20, T2 200, T3 400.
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
