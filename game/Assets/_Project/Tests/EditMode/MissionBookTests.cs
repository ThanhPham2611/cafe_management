using System;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class MissionBookTests
    {
        private int xuPaid;
        private int gemsPaid;
        private float rpPaid;
        private MissionBook book;

        private static readonly MissionContext AllKnown = new MissionContext
        {
            RecipeKnown = id => true,
            HasHiddenKnown = true
        };

        [SetUp]
        public void SetUp()
        {
            xuPaid = 0;
            gemsPaid = 0;
            rpPaid = 0f;
            book = new MissionBook(x => xuPaid += x, g => gemsPaid += g, r => rpPaid += r);
        }

        private int IndexOfKind(MissionKind kind)
        {
            for (int i = 0; i < book.Items.Count; i++)
                if (MissionCatalog.Find(book.Items[i].id).kind == kind) return i;
            return -1;
        }

        /// Sinh ngày mới tới khi có nhiệm vụ loại kind (thử nhiều key; tất định theo key).
        private int GenerateUntil(MissionKind kind, int tier, MissionContext ctx)
        {
            for (int key = 1000; key < 1400; key++)
            {
                book = new MissionBook(x => xuPaid += x, g => gemsPaid += g, r => rpPaid += r);
                book.EnsureToday(key, tier, ctx);
                if (IndexOfKind(kind) >= 0) return IndexOfKind(kind);
            }
            Assert.Fail("không sinh được nhiệm vụ " + kind);
            return -1;
        }

        [Test]
        public void EnsureToday_GeneratesThreeDistinctMissions()
        {
            Assert.That(book.EnsureToday(500, 1, AllKnown), Is.True);

            Assert.That(book.Items.Count, Is.EqualTo(MissionBook.DailyCount));
            Assert.That(book.Items[0].id, Is.Not.EqualTo(book.Items[1].id));
            Assert.That(book.Items[1].id, Is.Not.EqualTo(book.Items[2].id));
            Assert.That(book.Items[0].id, Is.Not.EqualTo(book.Items[2].id));
        }

        [Test]
        public void EnsureToday_Tier1_NeverOffersTierTwoMissions()
        {
            for (int key = 0; key < 200; key++)
            {
                var b = new MissionBook(x => { }, g => { }, r => { });
                b.EnsureToday(key, 1, AllKnown);
                for (int i = 0; i < b.Items.Count; i++)
                {
                    MissionKind k = MissionCatalog.Find(b.Items[i].id).kind;
                    Assert.That(k, Is.Not.EqualTo(MissionKind.ThreeStar));
                    Assert.That(k, Is.Not.EqualTo(MissionKind.SellHidden));
                    Assert.That(k, Is.Not.EqualTo(MissionKind.CleanDay));
                }
            }
        }

        [Test]
        public void EnsureToday_SkipsRecipeMissionsForRecipesNotKnown_AndHiddenWhenNoneKnown()
        {
            var ctx = new MissionContext { RecipeKnown = id => id == "ca_phe_sua", HasHiddenKnown = false };
            for (int key = 0; key < 200; key++)
            {
                var b = new MissionBook(x => { }, g => { }, r => { });
                b.EnsureToday(key, 2, ctx);
                for (int i = 0; i < b.Items.Count; i++)
                {
                    MissionTemplate t = MissionCatalog.Find(b.Items[i].id);
                    Assert.That(t.kind, Is.Not.EqualTo(MissionKind.SellHidden));
                    if (t.kind == MissionKind.SellRecipe) Assert.That(t.recipeId, Is.EqualTo("ca_phe_sua"));
                }
            }
        }

        [Test]
        public void EnsureToday_SameDay_KeepsMissions_NextDay_Regenerates_ClockBack_Ignored()
        {
            book.EnsureToday(500, 1, AllKnown);
            book.RecordServed("ca_phe_sua", 2, false, 0);
            string firstId = book.Items[0].id;

            Assert.That(book.EnsureToday(500, 1, AllKnown), Is.False);
            Assert.That(book.Items[0].id, Is.EqualTo(firstId));

            Assert.That(book.EnsureToday(499, 1, AllKnown), Is.False, "chỉnh giờ lùi không sinh lại");
            Assert.That(book.DayKey, Is.EqualTo(500));

            Assert.That(book.EnsureToday(501, 1, AllKnown), Is.True);
            Assert.That(book.DayKey, Is.EqualTo(501));
            Assert.That(book.ChestClaimed, Is.False);
            for (int i = 0; i < book.Items.Count; i++) Assert.That(book.Items[i].progress, Is.Zero);
        }

        [Test]
        public void RewardsScaleWithTier()
        {
            book.EnsureToday(500, 1, AllKnown);
            int t1 = book.Items[0].rewardXu;
            var b2 = new MissionBook(x => { }, g => { }, r => { });
            b2.EnsureToday(500, 2, AllKnown);
            // cùng key nhưng cấp khác có thể ra bộ nhiệm vụ khác: so theo hệ số của cấp
            Assert.That(MissionCatalog.TierScale(1), Is.EqualTo(1));
            Assert.That(MissionCatalog.TierScale(2), Is.EqualTo(4));
            Assert.That(MissionCatalog.TierScale(3), Is.EqualTo(12));
            Assert.That(MissionCatalog.TierScale(4), Is.EqualTo(30));
            Assert.That(t1, Is.GreaterThan(0));
            Assert.That(b2.Items[0].rewardXu % 4, Is.Zero);
        }

        [Test]
        public void RecordServed_AdvancesServeAndSellRecipe_AndCapsAtTarget()
        {
            int idx = GenerateUntil(MissionKind.Serve, 1, AllKnown);
            int target = book.Items[idx].target;

            for (int i = 0; i < target + 7; i++) book.RecordServed("tra_da", 2, false, 0);

            Assert.That(book.Items[idx].progress, Is.EqualTo(target));
            Assert.That(book.Items[idx].IsDone, Is.True);
        }

        [Test]
        public void RecordServed_SellRecipe_OnlyCountsThatRecipe()
        {
            int idx = GenerateUntil(MissionKind.SellRecipe, 1, AllKnown);
            string wanted = MissionCatalog.Find(book.Items[idx].id).recipeId;
            string other = wanted == "tra_da" ? "ca_phe_den" : "tra_da";

            book.RecordServed(other, 2, false, 0);
            Assert.That(book.Items[idx].progress, Is.Zero);
            book.RecordServed(wanted, 2, false, 0);
            Assert.That(book.Items[idx].progress, Is.EqualTo(1));
        }

        [Test]
        public void RecordServed_ThreeStarAndHiddenAndTips()
        {
            int star = GenerateUntil(MissionKind.ThreeStar, 2, AllKnown);
            book.RecordServed("ca_phe_sua", 2, false, 0);
            Assert.That(book.Items[star].progress, Is.Zero);
            book.RecordServed("ca_phe_sua", 3, false, 0);
            Assert.That(book.Items[star].progress, Is.EqualTo(1));

            int hidden = GenerateUntil(MissionKind.SellHidden, 2, AllKnown);
            book.RecordServed("ca_phe_muoi", 2, false, 0);
            Assert.That(book.Items[hidden].progress, Is.Zero);
            book.RecordServed("ca_phe_muoi", 2, true, 0);
            Assert.That(book.Items[hidden].progress, Is.EqualTo(1));

            int tips = GenerateUntil(MissionKind.EarnTips, 1, AllKnown);
            book.RecordServed("tra_da", 2, false, 40);
            book.RecordServed("tra_da", 2, false, 25);
            Assert.That(book.Items[tips].progress, Is.EqualTo(65));
        }

        [Test]
        public void RecordDay_NoWalkaway_NeedsNoLostAndEnoughServed()
        {
            int idx = GenerateUntil(MissionKind.NoWalkaway, 1, AllKnown);

            book.RecordDay(served: 20, lost: 1, cleanliness: -1f);
            Assert.That(book.Items[idx].progress, Is.Zero);
            book.RecordDay(served: 3, lost: 0, cleanliness: -1f);
            Assert.That(book.Items[idx].progress, Is.Zero, "ngày quá vắng không tính");
            book.RecordDay(served: 12, lost: 0, cleanliness: -1f);
            Assert.That(book.Items[idx].IsDone, Is.True);
        }

        [Test]
        public void EnsureToday_PicksOneMissionPerSlot()
        {
            for (int tier = 1; tier <= 3; tier++)
                for (int key = 0; key < 200; key++)
                {
                    var b = new MissionBook(x => { }, g => { }, r => { });
                    b.EnsureToday(key, tier, AllKnown);
                    var slots = new System.Collections.Generic.HashSet<int>();
                    for (int i = 0; i < b.Items.Count; i++) slots.Add(MissionCatalog.Find(b.Items[i].id).slot);
                    Assert.That(slots.Count, Is.EqualTo(MissionBook.DailyCount), "tier " + tier + " key " + key);
                }
        }

        [Test]
        public void EnsureToday_Tier1_NeverOffersExperiment()
        {
            for (int key = 0; key < 200; key++)
            {
                var b = new MissionBook(x => { }, g => { }, r => { });
                b.EnsureToday(key, 1, AllKnown);
                for (int i = 0; i < b.Items.Count; i++)
                    Assert.That(MissionCatalog.Find(b.Items[i].id).kind, Is.Not.EqualTo(MissionKind.Experiment));
            }
        }

        [Test]
        public void RecordPurchase_AdvancesBuyItems_AndIgnoresOtherMissions()
        {
            int idx = GenerateUntil(MissionKind.BuyItems, 1, AllKnown);
            int target = book.Items[idx].target;

            book.RecordServed("tra_da", 2, false, 0);
            Assert.That(book.Items[idx].progress, Is.Zero);
            for (int i = 0; i < target + 3; i++) book.RecordPurchase();

            Assert.That(book.Items[idx].progress, Is.EqualTo(target));
            Assert.That(book.Items[idx].IsDone, Is.True);
        }

        [Test]
        public void RecordServed_RegularFavorite_OnlyCountsFavoriteServes()
        {
            int idx = GenerateUntil(MissionKind.RegularFavorite, 1, AllKnown);

            book.RecordServed("ca_phe_sua", 2, false, 0);
            Assert.That(book.Items[idx].progress, Is.Zero, "khách thường / sai món không tính");
            book.RecordServed("ca_phe_sua", 2, false, 0, favorite: true);
            Assert.That(book.Items[idx].progress, Is.EqualTo(1));
        }

        [Test]
        public void RecordExperiment_AdvancesTryRecipe_AndClaimPaysRpAndGems()
        {
            int idx = GenerateUntil(MissionKind.Experiment, 2, AllKnown);
            MissionTemplate t = MissionCatalog.Find(book.Items[idx].id);

            for (int i = 0; i < book.Items[idx].target; i++) book.RecordExperiment();
            Assert.That(book.Items[idx].IsDone, Is.True);

            Assert.That(book.Claim(idx), Is.True);
            Assert.That(rpPaid, Is.EqualTo(t.rewardRp).Within(0.001f));
            Assert.That(gemsPaid, Is.EqualTo(t.rewardGems));
            Assert.That(book.Claim(idx), Is.False);
            Assert.That(rpPaid, Is.EqualTo(t.rewardRp).Within(0.001f), "nhận một lần");
        }

        [Test]
        public void RecordDay_NoWalkaway_MinServedGrowsWithTier()
        {
            Assert.That(MissionBook.MinServedFor(1), Is.EqualTo(MissionBook.MinServedForDayMission));
            Assert.That(MissionBook.MinServedFor(3), Is.GreaterThan(MissionBook.MinServedFor(2)));

            int idx = GenerateUntil(MissionKind.NoWalkaway, 2, AllKnown);
            int need = MissionBook.MinServedFor(2);

            book.RecordDay(served: need - 1, lost: 0, cleanliness: -1f, tier: 2);
            Assert.That(book.Items[idx].progress, Is.Zero, "cấp 2 cần đủ khách mới tính");
            book.RecordDay(served: need, lost: 0, cleanliness: -1f, tier: 2);
            Assert.That(book.Items[idx].IsDone, Is.True);
        }

        [Test]
        public void RecordDay_CleanDay_NeedsCleanlinessAtLeast80()
        {
            int idx = GenerateUntil(MissionKind.CleanDay, 2, AllKnown);

            book.RecordDay(served: 20, lost: 0, cleanliness: 79.9f);
            Assert.That(book.Items[idx].progress, Is.Zero);
            book.RecordDay(served: 20, lost: 0, cleanliness: 85f);
            Assert.That(book.Items[idx].IsDone, Is.True);
        }

        [Test]
        public void Completed_EventFiresOncePerMission()
        {
            int idx = GenerateUntil(MissionKind.Serve, 1, AllKnown);
            int fired = 0;
            book.Completed += i => { if (i == idx) fired++; };
            int target = book.Items[idx].target;

            for (int i = 0; i < target + 5; i++) book.RecordServed("tra_da", 2, false, 0);

            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void Claim_PaysOnce_AndOnlyWhenDone()
        {
            int idx = GenerateUntil(MissionKind.Serve, 1, AllKnown);
            Assert.That(book.Claim(idx), Is.False);
            Assert.That(xuPaid, Is.Zero);

            int target = book.Items[idx].target;
            int reward = book.Items[idx].rewardXu;
            for (int i = 0; i < target; i++) book.RecordServed("tra_da", 2, false, 0);

            Assert.That(book.Claim(idx), Is.True);
            Assert.That(xuPaid, Is.EqualTo(reward));
            Assert.That(book.Items[idx].claimed, Is.True);
            Assert.That(book.Claim(idx), Is.False);
            Assert.That(xuPaid, Is.EqualTo(reward));
            Assert.That(book.Claim(-1), Is.False);
            Assert.That(book.Claim(99), Is.False);
        }

        [Test]
        public void Chest_NeedsAllThreeClaimed_ThenPaysGemsAndRpOnce()
        {
            book.EnsureToday(500, 1, AllKnown);
            Assert.That(book.ChestReady, Is.False);
            Assert.That(book.ClaimChest(), Is.False);

            // Ép hoàn thành cả 3 bằng cách phát sự kiện thật nhiều lần
            for (int round = 0; round < 80; round++)
            {
                book.RecordServed("tra_da", 3, false, 50);
                book.RecordServed("ca_phe_sua", 3, false, 50);
                book.RecordServed("ca_phe_den", 3, false, 50);
                book.RecordServed("bac_xiu", 3, false, 50, favorite: true);
                book.RecordPurchase();
                book.RecordExperiment();
            }
            book.RecordDay(20, 0, 90f);
            for (int i = 0; i < book.Items.Count; i++)
            {
                Assert.That(book.Items[i].IsDone, Is.True, "mission " + book.Items[i].id);
                if (i < 2) Assert.That(book.Claim(i), Is.True);
            }
            Assert.That(book.ChestReady, Is.False, "mới nhận 2/3");

            Assert.That(book.Claim(2), Is.True);
            Assert.That(book.ChestReady, Is.True);

            // Nhiệm vụ có thể tự thưởng RP / Hạt khi nhận (buy_items, regular_fav, try_recipe): chỉ so phần tăng thêm của rương
            int gemsBefore = gemsPaid;
            float rpBefore = rpPaid;
            Assert.That(book.ClaimChest(), Is.True);
            Assert.That(gemsPaid - gemsBefore, Is.EqualTo(MissionBook.ChestGems));
            Assert.That(rpPaid - rpBefore, Is.EqualTo(MissionBook.ChestRp).Within(0.001f));
            Assert.That(book.ClaimChest(), Is.False);
            Assert.That(gemsPaid - gemsBefore, Is.EqualTo(MissionBook.ChestGems));
        }

        [Test]
        public void SaveLoad_RoundTripsProgressClaimsAndChest()
        {
            int idx = GenerateUntil(MissionKind.Serve, 1, AllKnown);
            for (int i = 0; i < book.Items[idx].target; i++) book.RecordServed("tra_da", 2, false, 0);
            book.Claim(idx);
            var save = new SaveData();
            book.WriteToSave(save);

            var copy = new MissionBook(x => { }, g => { }, r => { });
            copy.Load(save);

            Assert.That(copy.DayKey, Is.EqualTo(book.DayKey));
            Assert.That(copy.Items.Count, Is.EqualTo(book.Items.Count));
            Assert.That(copy.Items[idx].id, Is.EqualTo(book.Items[idx].id));
            Assert.That(copy.Items[idx].claimed, Is.True);
            Assert.That(copy.Items[idx].progress, Is.EqualTo(book.Items[idx].target));
            Assert.That(copy.EnsureToday(book.DayKey, 1, AllKnown), Is.False, "cùng ngày: giữ nguyên bản đã lưu");
        }

        [Test]
        public void Load_DropsUnknownIdsAndClampsProgress()
        {
            var save = new SaveData
            {
                missionDayKey = 700,
                missions = new[]
                {
                    new MissionSave { id = "khong_ton_tai", target = 5, progress = 2, rewardXu = 50 },
                    new MissionSave { id = "serve_30", target = 30, progress = 9999, rewardXu = 50, claimed = false },
                    new MissionSave { id = "tips_300", target = 0, progress = -4, rewardXu = -5 }
                }
            };

            book.Load(save);

            Assert.That(book.Items.Count, Is.EqualTo(2));
            Assert.That(book.Items[0].progress, Is.EqualTo(30));
            Assert.That(book.Items[1].target, Is.GreaterThan(0));
            Assert.That(book.Items[1].progress, Is.Zero);
            Assert.That(book.Items[1].rewardXu, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Load_NoMissions_FirstEnsureGenerates()
        {
            book.Load(null);
            Assert.That(book.Items.Count, Is.Zero);
            Assert.That(book.EnsureToday(500, 1, AllKnown), Is.True);
            Assert.That(book.Items.Count, Is.EqualTo(MissionBook.DailyCount));
        }

        [Test]
        public void DayKey_RollsOverAt04hVietnamTime()
        {
            // 04:00 giờ VN (UTC+7) = 21:00 UTC hôm trước
            int before = MissionClock.DayKey(new DateTime(2026, 10, 11, 20, 59, 0, DateTimeKind.Utc));
            int after = MissionClock.DayKey(new DateTime(2026, 10, 11, 21, 0, 0, DateTimeKind.Utc));
            int sameDay = MissionClock.DayKey(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc));

            Assert.That(after, Is.EqualTo(before + 1));
            Assert.That(sameDay, Is.EqualTo(after));
        }

        [Test]
        public void SecondsUntilReset_IsPositiveAndWithinOneDay()
        {
            var now = new DateTime(2026, 10, 11, 20, 30, 0, DateTimeKind.Utc);
            double s = MissionClock.SecondsUntilReset(now);
            Assert.That(s, Is.EqualTo(30 * 60).Within(1));
            double later = MissionClock.SecondsUntilReset(new DateTime(2026, 10, 11, 21, 0, 0, DateTimeKind.Utc));
            Assert.That(later, Is.EqualTo(24 * 3600).Within(1));
        }
    }
}
