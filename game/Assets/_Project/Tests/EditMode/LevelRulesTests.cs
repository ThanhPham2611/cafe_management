using System.Collections.Generic;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class LevelRulesTests
    {
        [Test]
        public void Tier3Band_NoStepLargerThan700Rp()
        {
            for (int lv = 16; lv < 31; lv++)
                Assert.That(LevelRules.RpFor(lv + 1) - LevelRules.RpFor(lv), Is.LessThanOrEqualTo(700), "Lv" + lv + " -> Lv" + (lv + 1));
        }

        [Test]
        public void Thresholds_AscendFromZero_AndHitEveryStarThreshold()
        {
            Assert.That(LevelRules.Thresholds[0], Is.Zero);
            Assert.That(LevelRules.MaxLevel, Is.EqualTo(41));
            for (int i = 1; i < LevelRules.Thresholds.Length; i++)
                Assert.That(LevelRules.Thresholds[i], Is.GreaterThan(LevelRules.Thresholds[i - 1]));
            foreach (int starRp in GameBalance.StarThresholds)
                Assert.That(LevelRules.Thresholds, Does.Contain(starRp), "mốc sao " + starRp + " phải là mốc level");
        }

        [TestCase(0f, 1)]
        [TestCase(34.9f, 1)]
        [TestCase(35f, 2)]
        [TestCase(299f, 5)]
        [TestCase(300f, 6)]      // ★2
        [TestCase(2999f, 15)]
        [TestCase(3000f, 16)]    // ★3
        [TestCase(12000f, 31)]   // ★4
        [TestCase(40000f, 41)]   // ★5 = level tối đa
        [TestCase(1000000f, 41)]
        public void LevelFor_FollowsThresholds(float rp, int expected)
        {
            Assert.That(LevelRules.LevelFor(rp), Is.EqualTo(expected));
        }

        [Test]
        public void StarLevels_AreExactlyTheStarThresholds()
        {
            int star;
            Assert.That(LevelRules.IsStarLevel(1, out star), Is.False);
            Assert.That(LevelRules.IsStarLevel(2, out star), Is.False);
            Assert.That(LevelRules.IsStarLevel(6, out star), Is.True);
            Assert.That(star, Is.EqualTo(2));
            Assert.That(LevelRules.IsStarLevel(16, out star) && star == 3, Is.True);
            Assert.That(LevelRules.IsStarLevel(31, out star) && star == 4, Is.True);
            Assert.That(LevelRules.IsStarLevel(41, out star) && star == 5, Is.True);
        }

        [Test]
        public void Steps_AreConvexInsideEachStar_FirstLevelOfAStarComesEarly()
        {
            // 5 level đầu (★1): 0, 35, 85, 150, 220 rồi ★2 ở 300 RP
            Assert.That(LevelRules.Thresholds[1], Is.EqualTo(35));
            Assert.That(LevelRules.Thresholds[2], Is.EqualTo(85));
            Assert.That(LevelRules.Thresholds[4], Is.EqualTo(220));
            // bước tăng dần trong một hạng sao (lồi): bước cuối lớn hơn bước đầu
            int firstStep = LevelRules.Thresholds[1] - LevelRules.Thresholds[0];
            int lastStep = LevelRules.Thresholds[5] - LevelRules.Thresholds[4];
            Assert.That(lastStep, Is.GreaterThan(firstStep));
            // Lv7 đến sớm sau ★2: chưa tới 5% khoảng ★2→★3
            Assert.That(LevelRules.Thresholds[6] - LevelRules.Thresholds[5], Is.LessThan(0.05f * (3000 - 300)));
        }

        [Test]
        public void Rewards_GrowWithLevel_ScaleWithStarRank_AndStarLevelsPayTripleXuPlusGems()
        {
            Assert.That(LevelRules.StarAt(5), Is.EqualTo(1));
            Assert.That(LevelRules.StarAt(6), Is.EqualTo(2));
            Assert.That(LevelRules.StarAt(15), Is.EqualTo(2));
            Assert.That(LevelRules.StarAt(16), Is.EqualTo(3));
            Assert.That(LevelRules.RewardXu(2), Is.EqualTo(180));
            Assert.That(LevelRules.RewardXu(10), Is.EqualTo(1000), "(100 + 40·10) x 2 sao");
            Assert.That(LevelRules.RewardXu(6), Is.EqualTo(2040), "mốc ★2: (100 + 240) x 2 sao x 3");
            Assert.That(LevelRules.RewardXu(16), Is.EqualTo(6660));
            Assert.That(LevelRules.RewardXu(31), Is.EqualTo(16080));
            Assert.That(LevelRules.RewardXu(41), Is.EqualTo(26100));
            Assert.That(LevelRules.RewardGems(2), Is.Zero);
            Assert.That(LevelRules.RewardGems(6), Is.EqualTo(50));
            Assert.That(LevelRules.RewardGems(16), Is.EqualTo(100));
            Assert.That(LevelRules.RewardGems(31), Is.EqualTo(200));
            Assert.That(LevelRules.RewardGems(41), Is.EqualTo(400));
        }

        [Test]
        public void RpForAndNextRp_AreConsistent()
        {
            Assert.That(LevelRules.RpFor(1), Is.Zero);
            Assert.That(LevelRules.NextRp(1), Is.EqualTo(LevelRules.RpFor(2)));
            Assert.That(LevelRules.NextRp(LevelRules.MaxLevel), Is.EqualTo(-1));
        }

        // ---- Reputation: thưởng đúng một lần, sàn mềm theo level ----

        private static List<int> Collect(System.Action body)
        {
            var got = new List<int>();
            System.Action<int> h = lv => got.Add(lv);
            GameEvents.LevelUp += h;
            try { body(); }
            finally { GameEvents.LevelUp -= h; }
            return got;
        }

        [Test]
        public void Reputation_RaisesLevelUpOncePerLevel_EvenWhenOneGainJumpsSeveral()
        {
            var rep = new Reputation();
            rep.Init(0f);
            Assert.That(Collect(() => rep.Add(30f)), Is.Empty);
            Assert.That(Collect(() => rep.Add(30f)), Is.EqualTo(new[] { 2 }));
            Assert.That(Collect(() => rep.Add(250f)), Is.EqualTo(new[] { 3, 4, 5, 6 }), "310 RP: lên tới ★2 (level 6)");
            Assert.That(Collect(() => rep.Add(1f)), Is.Empty);
            Assert.That(rep.Level, Is.EqualTo(6));
            Assert.That(rep.Stars, Is.EqualTo(2));
        }

        [Test]
        public void Reputation_NeverFallsBelowTheLevelReached()
        {
            var rep = new Reputation();
            rep.Init(130f);                       // level 3 (từ 85 RP)
            rep.Add(-500f);
            Assert.That(rep.RP, Is.EqualTo(85f));
            Assert.That(rep.Level, Is.EqualTo(3));
        }

        [Test]
        public void Reputation_ReloadingMidDayDoesNotPayTheSameLevelTwice()
        {
            var rep = new Reputation();
            rep.Init(0f);
            rep.Add(130f);                        // level 3
            int rewarded = rep.RewardedLevel;
            Assert.That(rewarded, Is.EqualTo(3));

            // Vào lại: RP về mốc đầu ngày (0) nhưng level đã thưởng được lưu
            var reloaded = new Reputation();
            reloaded.Init(0f, rewarded);
            Assert.That(Collect(() => reloaded.Add(130f)), Is.Empty, "đã thưởng level 2 và 3 rồi");
            Assert.That(Collect(() => reloaded.Add(60f)), Is.EqualTo(new[] { 4 }), "level mới thì vẫn thưởng");
        }

        [Test]
        public void RewardBudget_UpToTheTierThreeStarStaysUnderFifteenPercentOfTheHousePrice()
        {
            int xu = 0, gems = 0;
            for (int lv = 2; lv <= 16; lv++) xu += LevelRules.RewardXu(lv);
            for (int lv = 2; lv <= LevelRules.MaxLevel; lv++) gems += LevelRules.RewardGems(lv);
            Assert.That(xu, Is.EqualTo(19380));
            Assert.That(xu, Is.LessThanOrEqualTo(GameBalance.Tier3Price * 0.15f), "thưởng level không được làm lệch nhịp mua nhà (economy §22)");
            Assert.That(gems, Is.EqualTo(750), "50 + 100 + 200 + 400 Hạt");
        }

        [Test]
        public void Reputation_RpNeverGoesNegative_AndAddReportsTheClampedChange()
        {
            var rep = new Reputation();
            rep.Init(0f);
            Assert.That(rep.Add(-50f), Is.Zero);
            Assert.That(rep.RP, Is.Zero);
            rep.Init(130f);                       // sàn 85
            Assert.That(rep.Add(-500f), Is.EqualTo(-45f));
        }

        [Test]
        public void OldSaveJson_WithoutLevelFields_ReadsAsUnknown()
        {
            SaveData d = UnityEngine.JsonUtility.FromJson<SaveData>("{\"xu\":500,\"rp\":2500}");
            Assert.That(d.levelRewarded, Is.Zero);
            Assert.That(d.levelClaimed, Is.EqualTo(-1), "-1 = save cũ: GameBootstrap suy ra từ levelRewarded hoặc trả bù một lần");
        }

        [Test]
        public void ThrowingLevelUpHandler_DoesNotBreakAddOrOtherHandlers()
        {
            var rep = new Reputation();
            rep.Init(0f);
            var got = new List<int>();
            System.Action<int> bad = lv => { throw new System.InvalidOperationException("boom"); };
            System.Action<int> good = lv => got.Add(lv);
            GameEvents.LevelUp += bad;
            GameEvents.LevelUp += good;
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
                Assert.DoesNotThrow(() => rep.Add(70f));
            }
            finally { GameEvents.LevelUp -= bad; GameEvents.LevelUp -= good; }
            Assert.That(got, Is.EqualTo(new[] { 2 }));
            Assert.That(rep.RewardedLevel, Is.EqualTo(2));
        }

        [Test]
        public void SaveData_RoundTripsEveryRetentionField()
        {
            var d = new SaveData
            {
                levelRewarded = 9, levelClaimed = 7, fundTier = 3, fundStage = 2,
                weekIndex = 5, weekProgress = 1234, weekDone = true, weekTier = 2,
            };
            SaveData back = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(d));
            Assert.That((back.levelRewarded, back.levelClaimed, back.fundTier, back.fundStage), Is.EqualTo((9, 7, 3, 2)));
            Assert.That((back.weekIndex, back.weekProgress, back.weekDone, back.weekTier), Is.EqualTo((5, 1234, true, 2)));
            SaveData old = UnityEngine.JsonUtility.FromJson<SaveData>("{\"xu\":500}");
            Assert.That((old.fundTier, old.weekTier, old.weekDone), Is.EqualTo((0, 1, false)), "save cũ: quỹ chưa có, tuần chốt cấp 1");
        }

        [Test]
        public void Reputation_OldSaveWithoutRewardedLevelGetsNoBackPay()
        {
            var rep = new Reputation();
            rep.Init(2500f, 0);                   // save cũ: levelRewarded = 0
            Assert.That(rep.RewardedLevel, Is.EqualTo(rep.Level));
            Assert.That(Collect(() => rep.Add(1f)), Is.Empty);
        }
    }
}
