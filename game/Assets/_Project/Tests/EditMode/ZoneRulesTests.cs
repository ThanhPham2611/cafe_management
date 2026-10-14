using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    /// Các khu của nhà hàng cấp 3: bản đồ ô, luật đặt đồ theo khu, giá mở khu, nhu cầu, độ dài ngày.
    public class ZoneRulesTests
    {
        private static ZoneMap Map()
        {
            return RoomLayout.For(3).CreateZoneMap();
        }

        [Test]
        public void Tier2HasNoZones_Tier3HasZones()
        {
            Assert.That(RoomLayout.For(2).CreateZoneMap(), Is.Null);
            Assert.That(RoomLayout.For(2).HasZones, Is.False);
            Assert.That(RoomLayout.For(3).HasZones, Is.True);
            Assert.That(RoomLayout.For(3).CreateZoneMap().Rects.Length, Is.EqualTo(4), "sảnh + 3 khu phụ");
        }

        [Test]
        public void ZoneRects_PartitionTheWholeGridExactlyOnce()
        {
            RoomSpec r = RoomLayout.For(3);
            ZoneMap m = Map();
            Vector2Int size = r.GridSize;
            int area = 0;
            for (int i = 0; i < m.Rects.Length; i++) area += m.Rects[i].width * m.Rects[i].height;
            Assert.That(area, Is.EqualTo(size.x * size.y), "tổng diện tích các khu = diện tích lưới");

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int hits = 0;
                    for (int i = 0; i < m.Rects.Length; i++)
                    {
                        RectInt rc = m.Rects[i];
                        if (x >= rc.xMin && x < rc.xMax && y >= rc.yMin && y < rc.yMax) hits++;
                    }
                    Assert.That(hits, Is.EqualTo(1), "ô (" + x + "," + y + ") phải thuộc đúng một khu");
                }
            }
        }

        [Test]
        public void ZoneRects_MatchTheDesignLayout()
        {
            ZoneMap m = Map();
            Assert.That(m.Rects[(int)ZoneId.Hall], Is.EqualTo(new RectInt(12, 0, 20, 24)), "sảnh 10 x 12 m ở giữa");
            Assert.That(m.Rects[(int)ZoneId.Garden], Is.EqualTo(new RectInt(0, 0, 12, 24)), "sân vườn 6 x 12 m bên trái");
            Assert.That(m.Rects[(int)ZoneId.Terrace], Is.EqualTo(new RectInt(32, 0, 12, 12)), "lầu 6 x 6 m bên phải, phía trước");
            Assert.That(m.Rects[(int)ZoneId.Vip], Is.EqualTo(new RectInt(32, 12, 12, 12)), "VIP 6 x 6 m bên phải, phía sau");
        }

        [Test]
        public void ZoneWorldRects_LineUpWithTheHallWalls()
        {
            RoomSpec r = RoomLayout.For(3);
            Rect hall = r.ZoneWorldRect(ZoneId.Hall);
            Assert.That(hall.xMin, Is.EqualTo(-5f).Within(0.001f));
            Assert.That(hall.xMax, Is.EqualTo(5f).Within(0.001f));
            Assert.That(hall.yMin, Is.EqualTo(-6f).Within(0.001f));
            Assert.That(hall.yMax, Is.EqualTo(6f).Within(0.001f));
            Rect garden = r.ZoneWorldRect(ZoneId.Garden);
            Assert.That(garden.xMin, Is.EqualTo(-11f).Within(0.001f));
            Assert.That(garden.xMax, Is.EqualTo(-5f).Within(0.001f), "sân vườn chạm tường trái của sảnh");
            Assert.That(r.ZoneWorldRect(ZoneId.Terrace).xMin, Is.EqualTo(5f).Within(0.001f));
            Assert.That(r.ZoneWorldRect(ZoneId.Terrace).yMax, Is.EqualTo(0f).Within(0.001f));
            Assert.That(r.ZoneWorldRect(ZoneId.Vip).yMin, Is.EqualTo(0f).Within(0.001f));
            Assert.That(r.TotalWidth, Is.EqualTo(22f).Within(0.001f));
        }

        [Test]
        public void ZoneAt_ReturnsTheOwningZone()
        {
            ZoneMap m = Map();
            Assert.That(m.ZoneAt(new Vector2Int(20, 10)), Is.EqualTo(ZoneId.Hall));
            Assert.That(m.ZoneAt(new Vector2Int(0, 0)), Is.EqualTo(ZoneId.Garden));
            Assert.That(m.ZoneAt(new Vector2Int(11, 23)), Is.EqualTo(ZoneId.Garden));
            Assert.That(m.ZoneAt(new Vector2Int(12, 0)), Is.EqualTo(ZoneId.Hall));
            Assert.That(m.ZoneAt(new Vector2Int(40, 3)), Is.EqualTo(ZoneId.Terrace));
            Assert.That(m.ZoneAt(new Vector2Int(40, 15)), Is.EqualTo(ZoneId.Vip));
            Assert.That(m.ZoneAt(new Vector2Int(32, 11)), Is.EqualTo(ZoneId.Terrace));
            Assert.That(m.ZoneAt(new Vector2Int(32, 12)), Is.EqualTo(ZoneId.Vip));
        }

        [Test]
        public void Fits_OnlyInsideOneOpenZone()
        {
            ZoneMap m = Map();
            var table = new Vector2Int(2, 2);
            Assert.That(m.Fits(new Vector2Int(20, 10), table), Is.True, "sảnh luôn mở");
            Assert.That(m.Fits(new Vector2Int(4, 4), table), Is.False, "sân vườn chưa mở");
            Assert.That(m.Fits(new Vector2Int(36, 4), table), Is.False, "lầu chưa mở");

            m.SetOpen(ZoneInfo.Bit(ZoneId.Garden));
            Assert.That(m.IsOpen(ZoneId.Garden), Is.True);
            Assert.That(m.Fits(new Vector2Int(4, 4), table), Is.True);
            Assert.That(m.Fits(new Vector2Int(36, 4), table), Is.False, "lầu vẫn chưa mở");
            Assert.That(m.Fits(new Vector2Int(11, 4), table), Is.False, "bàn vắt qua ranh giới sân vườn / sảnh bị từ chối");
            Assert.That(m.Fits(new Vector2Int(10, 4), table), Is.True, "vừa sát ranh giới vẫn được");
            Assert.That(m.Fits(new Vector2Int(10, 23), table), Is.False, "chạy ra ngoài mép lưới");
        }

        [Test]
        public void Fits_TerraceAndVipShareAnEdge()
        {
            ZoneMap m = Map();
            m.SetOpen(ZoneInfo.Bit(ZoneId.Terrace) | ZoneInfo.Bit(ZoneId.Vip));
            var table = new Vector2Int(2, 2);
            Assert.That(m.Fits(new Vector2Int(36, 10), table), Is.True, "nằm hẳn trong lầu (y 10-11)");
            Assert.That(m.Fits(new Vector2Int(36, 11), table), Is.False, "vắt qua tường ngăn lầu / VIP");
            Assert.That(m.Fits(new Vector2Int(36, 12), table), Is.True, "nằm hẳn trong VIP");
        }

        [Test]
        public void SetOpen_HallIsAlwaysOpen()
        {
            ZoneMap m = Map();
            m.SetOpen(0);
            Assert.That(m.IsOpen(ZoneId.Hall), Is.True);
            m.SetOpen(ZoneInfo.Bit(ZoneId.Vip));
            Assert.That(m.IsOpen(ZoneId.Vip), Is.True);
            Assert.That(m.IsOpen(ZoneId.Garden), Is.False);
            Assert.That(m.IsOpen(ZoneId.Hall), Is.True);
        }

        [Test]
        public void GridSystem_ZoneQueriesFollowTheMap()
        {
            var grid = new GridSystem(Vector3.zero, new Vector2Int(44, 24), null);
            Assert.That(grid.ZoneAt(new Vector2Int(2, 2)), Is.EqualTo(ZoneId.Hall), "chưa có bản đồ khu: tất cả là sảnh");
            Assert.That(grid.IsCellOpen(new Vector2Int(2, 2)), Is.True);
            Assert.That(grid.IsCellOpen(new Vector2Int(44, 2)), Is.False, "ngoài lưới");

            ZoneMap m = Map();
            grid.SetZones(m);
            Assert.That(grid.ZoneAt(new Vector2Int(2, 2)), Is.EqualTo(ZoneId.Garden));
            Assert.That(grid.IsCellOpen(new Vector2Int(2, 2)), Is.False, "sân vườn chưa mở");
            Assert.That(grid.IsCellOpen(new Vector2Int(20, 2)), Is.True);
            m.SetOpen(ZoneInfo.Bit(ZoneId.Garden));
            Assert.That(grid.IsCellOpen(new Vector2Int(2, 2)), Is.True);
        }

        [Test]
        public void ZoneCosts_FollowEconomyDoc()
        {
            Assert.That(ZoneInfo.Cost(ZoneId.Garden), Is.EqualTo(40000));
            Assert.That(ZoneInfo.Cost(ZoneId.Terrace), Is.EqualTo(90000));
            Assert.That(ZoneInfo.Cost(ZoneId.Vip), Is.EqualTo(180000));
            Assert.That(ZoneInfo.Cost(ZoneId.Hall), Is.Zero);
        }

        [Test]
        public void OpenCount_IgnoresHallAndUnknownBits()
        {
            Assert.That(ZoneInfo.OpenCount(0), Is.Zero);
            Assert.That(ZoneInfo.OpenCount(ZoneInfo.Bit(ZoneId.Hall)), Is.Zero);
            Assert.That(ZoneInfo.OpenCount(ZoneInfo.Bit(ZoneId.Garden) | ZoneInfo.Bit(ZoneId.Vip)), Is.EqualTo(2));
            int all = ZoneInfo.Bit(ZoneId.Garden) | ZoneInfo.Bit(ZoneId.Terrace) | ZoneInfo.Bit(ZoneId.Vip);
            Assert.That(ZoneInfo.OpenCount(all | 0x100), Is.EqualTo(3));
        }

        [Test]
        public void PriceMult_OnlyVipPaysMore()
        {
            Assert.That(ZoneInfo.PriceMult(ZoneId.Hall), Is.EqualTo(1f));
            Assert.That(ZoneInfo.PriceMult(ZoneId.Garden), Is.EqualTo(1f));
            Assert.That(ZoneInfo.PriceMult(ZoneId.Terrace), Is.EqualTo(1f));
            Assert.That(ZoneInfo.PriceMult(ZoneId.Vip), Is.EqualTo(1.5f));
        }

        [Test]
        public void FRef_HallUsesTierValue_ZonesUseOwnValue()
        {
            Assert.That(ZoneInfo.FRef(ZoneId.Hall, 400f), Is.EqualTo(400f));
            Assert.That(ZoneInfo.FRef(ZoneId.Garden, 400f), Is.EqualTo(GameBalance.FRefZone));
            Assert.That(GameBalance.FRefZone, Is.EqualTo(150f));
        }

        [Test]
        public void ZoneDemandMult_AddsTwentyPercentPerZone()
        {
            Assert.That(GameBalance.ZoneDemandMult(0), Is.EqualTo(1f));
            Assert.That(GameBalance.ZoneDemandMult(1), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(GameBalance.ZoneDemandMult(3), Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(GameBalance.ZoneDemandMult(-2), Is.EqualTo(1f), "không bao giờ giảm nhu cầu");
        }

        [Test]
        public void Demand_ZoneMultScalesTheTier3Demand()
        {
            int baseDemand = GameBalance.DemandWithAttract(3, 3, 1, 0);
            Assert.That(GameBalance.DemandWithAttract(3, 3, 1, 0, 1f), Is.EqualTo(baseDemand));
            int withZones = GameBalance.DemandWithAttract(3, 3, 1, 0, GameBalance.ZoneDemandMult(3));
            Assert.That(withZones, Is.EqualTo(Mathf.RoundToInt(baseDemand * 1.6f)).Within(1));
            Assert.That(GameBalance.DemandWithAttract(1, 1, 1, 0), Is.EqualTo(GameBalance.DemandWithAttract(1, 1, 1, 0, 1f)), "tham số mặc định không đổi kết quả cũ");
        }

        [Test]
        public void DayLength_Tier3IsLonger_AndKeepsOneInGameDayPerRealDay()
        {
            Assert.That(GameBalance.DayRealSecondsFor(1), Is.EqualTo(GameBalance.DayRealSeconds));
            Assert.That(GameBalance.DayRealSecondsFor(2), Is.EqualTo(GameBalance.DayRealSeconds));
            Assert.That(GameBalance.DayRealSecondsFor(3), Is.EqualTo(300f));
            float inGameDay = (GameBalance.CloseHour - GameBalance.OpenHour) * 3600f;
            Assert.That(GameBalance.InGameSecondsPerRealSecondFor(3) * GameBalance.DayRealSecondsFor(3), Is.EqualTo(inGameDay).Within(0.1f));
            Assert.That(GameBalance.InGameSecondsPerRealSecondFor(2), Is.EqualTo(GameBalance.InGameSecondsPerRealSecond).Within(0.0001f));
            Assert.That(GameBalance.InGameSecondsPerRealSecondFor(3), Is.LessThan(GameBalance.InGameSecondsPerRealSecondFor(2)));
            Assert.That(GameBalance.InGameSecondsPerRealSecondForLength(300f), Is.EqualTo(GameBalance.InGameSecondsPerRealSecondFor(3)), "DayCycle.InGameRate dùng cùng công thức");
            Assert.That(GameBalance.InGameSecondsPerRealSecondForLength(0f), Is.GreaterThan(0f).And.Not.EqualTo(float.PositiveInfinity), "độ dài 0 không gây chia cho 0");
        }

        [Test]
        public void LayoutShift_OnlyForOldTier3Saves()
        {
            Assert.That(TierManager.LayoutShiftCells(null), Is.Zero);
            Assert.That(TierManager.LayoutShiftCells(new SaveData { tier = 2, gridVersion = 0 }), Is.Zero);
            Assert.That(TierManager.LayoutShiftCells(new SaveData { tier = 3, gridVersion = 0 }), Is.EqualTo(12), "save nhà hàng cũ dời sảnh sang ô x = 12");
            Assert.That(TierManager.LayoutShiftCells(new SaveData { tier = 3, gridVersion = 1 }), Is.Zero);
        }

        [Test]
        public void CleanlinessByCi_WalkAwayAndTipThresholds()
        {
            Assert.That(CleanlinessModel.WalkAwayChanceFor(100f), Is.Zero);
            Assert.That(CleanlinessModel.WalkAwayChanceFor(60f), Is.Zero);
            Assert.That(CleanlinessModel.WalkAwayChanceFor(50f), Is.EqualTo(0.15f).Within(0.0001f), "+15% mỗi 10 CI dưới 60");
            Assert.That(CleanlinessModel.WalkAwayChanceFor(0f), Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(CleanlinessModel.TipMultiplierFor(80f), Is.EqualTo(1f));
            Assert.That(CleanlinessModel.TipMultiplierFor(59f), Is.EqualTo(GameBalance.CiTipMultiplier));
        }

        [Test]
        public void OpeningClear_NeedsAOneMeterWideFreeLane()
        {
            ZoneMap m = Map();
            RectInt hall = m.Hall;
            // Sân vườn: hai cột ô sát tường trái của sảnh (x = 12, 13), dải y = 9..14
            System.Func<Vector2Int, bool> allFree = c => true;
            Assert.That(m.OpeningClear(ZoneId.Garden, allFree), Is.True);
            Assert.That(m.OpeningClear(ZoneId.Hall, c => false), Is.True, "sảnh chính luôn thông");

            // Chặn hết cột sát tường trong dải lỗ mở
            System.Func<Vector2Int, bool> blockedWall = c => !(c.x == hall.xMin && c.y >= 9 && c.y <= 14);
            Assert.That(m.OpeningClear(ZoneId.Garden, blockedWall), Is.False, "đồ chắn kín lỗ mở => không thông");

            // Chỉ chừa một hàng ô rộng 1 ô: chưa đủ 1 m
            System.Func<Vector2Int, bool> oneCell = c => !(c.x == hall.xMin && c.y != 11) && !(c.x == hall.xMin + 1 && c.y != 11);
            Assert.That(m.OpeningClear(ZoneId.Garden, oneCell), Is.False, "lối chỉ rộng 1 ô (0,5 m) chưa đủ");

            // Chừa lối rộng 2 ô (y = 11,12)
            System.Func<Vector2Int, bool> twoCells = c => !(c.x <= hall.xMin + 1 && c.y >= 9 && c.y <= 14 && c.y != 11 && c.y != 12);
            Assert.That(m.OpeningClear(ZoneId.Garden, twoCells), Is.True);

            // Lầu / VIP nằm bên phải: cột sát tường phải của sảnh (x = 31, 30)
            int rx = hall.xMax - 1;
            System.Func<Vector2Int, bool> blockedRightFront = c => !(c.x == rx && c.y >= 3 && c.y <= 8);
            Assert.That(m.OpeningClear(ZoneId.Terrace, blockedRightFront), Is.False);
            Assert.That(m.OpeningClear(ZoneId.Vip, blockedRightFront), Is.True, "lối sang VIP (y 15..20) không bị ảnh hưởng");
        }

        [Test]
        public void ScheduleRescale_OnlyMovesCustomersNotYetSpawned()
        {
            float[] schedule = { 10f, 50f, 100f, 150f };
            CustomerSpawner.RescaleFrom(schedule, 2, 300f / 210f);
            Assert.That(schedule[0], Is.EqualTo(10f));
            Assert.That(schedule[1], Is.EqualTo(50f), "khách đã sinh giữ nguyên");
            Assert.That(schedule[2], Is.EqualTo(100f * 300f / 210f).Within(0.01f));
            Assert.That(schedule[3], Is.EqualTo(150f * 300f / 210f).Within(0.01f));
            CustomerSpawner.RescaleFrom(null, 0, 2f); // không ném lỗi
            CustomerSpawner.RescaleFrom(schedule, 0, 0f);
            Assert.That(schedule[0], Is.EqualTo(10f), "tỉ lệ <= 0 bị bỏ qua");
        }
    }
}
