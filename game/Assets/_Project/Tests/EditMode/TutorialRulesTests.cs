using NUnit.Framework;

namespace Cafe.Tests
{
    public class TutorialRulesTests
    {
        [Test]
        public void InitialStep_NewGame_StartsCore()
        {
            Assert.That(TutorialRules.InitialStep(false, 0, 1, 1), Is.EqualTo(TutorialStep.NotStarted));
        }

        [Test]
        public void InitialStep_OldSaveBeyondDay1_CountsAsDone()
        {
            Assert.That(TutorialRules.InitialStep(true, 0, 5, 1), Is.EqualTo(TutorialStep.Done));
            Assert.That(TutorialRules.InitialStep(true, 0, 1, 2), Is.EqualTo(TutorialStep.Done), "đã lên cấp");
        }

        [Test]
        public void InitialStep_SaveDuringDay1_RestartsCore()
        {
            Assert.That(TutorialRules.InitialStep(true, 4, 1, 1), Is.EqualTo(TutorialStep.NotStarted));
            Assert.That(TutorialRules.InitialStep(true, 0, 1, 1), Is.EqualTo(TutorialStep.NotStarted));
        }

        [Test]
        public void InitialStep_SavedDone_StaysDone()
        {
            Assert.That(TutorialRules.InitialStep(true, TutorialStep.Done, 1, 1), Is.EqualTo(TutorialStep.Done));
        }

        [Test]
        public void Next_WalksAllStepsThenStaysDone()
        {
            int s = TutorialStep.Welcome;
            int count = 1;
            while (!TutorialRules.IsDone(s)) { s = TutorialRules.Next(s); count++; }
            Assert.That(count, Is.EqualTo(9));
            Assert.That(TutorialRules.Next(TutorialStep.Done), Is.EqualTo(TutorialStep.Done));
        }

        [Test]
        public void ScriptedSteps_PauseWorld_OnlyBetweenTapCustomerAndCollectCup()
        {
            for (int s = TutorialStep.NotStarted; s <= TutorialStep.Done; s++)
            {
                bool scripted = s >= TutorialStep.TapCustomer && s <= TutorialStep.CollectCup;
                Assert.That(TutorialRules.PatiencePaused(s), Is.EqualTo(scripted), "patience step " + s);
                Assert.That(TutorialRules.NoPenalty(s), Is.EqualTo(scripted), "penalty step " + s);
            }
        }

        [Test]
        public void AllowedSpawns_OneCustomerUntilCupCollected()
        {
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.NotStarted), Is.EqualTo(0));
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.Welcome), Is.EqualTo(0));
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.Brew), Is.EqualTo(1));
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.CollectCup), Is.EqualTo(1));
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.PatienceRing), Is.EqualTo(int.MaxValue));
            Assert.That(TutorialRules.AllowedSpawns(TutorialStep.Done), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ClockHold_UntilBurstReleased()
        {
            Assert.That(TutorialRules.ClockHold(TutorialStep.Brew, 1), Is.True);
            Assert.That(TutorialRules.ClockHold(TutorialStep.PatienceRing, 1), Is.True);
            Assert.That(TutorialRules.ClockHold(TutorialStep.PatienceRing, TutorialRules.BurstCustomers), Is.False);
            Assert.That(TutorialRules.ClockHold(TutorialStep.Summary, 0), Is.False);
            Assert.That(TutorialRules.ClockHold(TutorialStep.Done, 0), Is.False);
        }

        [Test]
        public void ForcedRecipes_FirstTwoOrdersOnly()
        {
            Assert.That(TutorialRules.ForcedRecipeFor(0), Is.EqualTo("ca_phe_den"));
            Assert.That(TutorialRules.ForcedRecipeFor(1), Is.EqualTo("ca_phe_sua"));
            Assert.That(TutorialRules.ForcedRecipeFor(2), Is.Null);
            Assert.That(TutorialRules.ForcedRecipeFor(-1), Is.Null);
        }

        [Test]
        public void StepIds_AreUniqueAndLogged()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int s = TutorialStep.Welcome; s <= TutorialStep.Done; s++)
                Assert.That(ids.Add(TutorialRules.StepId(s)), Is.True, "trùng id bước " + s);
        }

        [Test]
        public void PickTip_NothingEligible_ReturnsNull()
        {
            Assert.That(TutorialRules.PickTip(new TipContext { day = 1, tier = 1 }, new TutorialSeen()), Is.Null);
        }

        [Test]
        public void PickTip_FiresOnce_ThenNextByPriority()
        {
            var seen = new TutorialSeen();
            var ctx = new TipContext { day = 3, tier = 1, cupsEmpty = true, missionClaimable = true, bacXiuNew = true };
            Assert.That(TutorialRules.PickTip(ctx, seen), Is.EqualTo(TutorialTips.NoCup));
            seen.Add(TutorialTips.NoCup);
            Assert.That(TutorialRules.PickTip(ctx, seen), Is.EqualTo(TutorialTips.Mission));
            seen.Add(TutorialTips.Mission);
            Assert.That(TutorialRules.PickTip(ctx, seen), Is.EqualTo(TutorialTips.BacXiu));
            seen.Add(TutorialTips.BacXiu);
            Assert.That(TutorialRules.PickTip(ctx, seen), Is.Null);
        }

        [Test]
        public void Stool_NeedsDay2_Tier1_AndAffordable()
        {
            var seen = new TutorialSeen();
            Assert.That(TutorialRules.PickTip(new TipContext { day = 1, tier = 1, canAffordStool = true }, seen), Is.Null);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 2, tier = 1, canAffordStool = false }, seen), Is.Null);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 2, tier = 2, canAffordStool = true }, seen), Is.Null);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 2, tier = 1, canAffordStool = true }, seen), Is.EqualTo(TutorialTips.Stool));
        }

        [Test]
        public void TierTips_AppearOnlyInTheirTier()
        {
            var seen = new TutorialSeen();
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 1, dirtyTable = true, wantsMop = true }, seen), Is.Null);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 2, dirtyTable = true }, seen), Is.EqualTo(TutorialTips.T2Table));
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 2, canUpgrade = true }, seen), Is.EqualTo(TutorialTips.Upgrade2));
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 1, canUpgrade = true }, seen), Is.EqualTo(TutorialTips.Upgrade1));
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 3 }, seen), Is.EqualTo(TutorialTips.T3Machines));
            seen.Add(TutorialTips.T3Machines);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 3 }, seen), Is.EqualTo(TutorialTips.T3Staff));
            seen.Add(TutorialTips.T3Staff);
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 3 }, seen), Is.Null, "khu chỉ hiện khi đủ tiền");
            Assert.That(TutorialRules.PickTip(new TipContext { day = 5, tier = 3, canAffordZone = true }, seen), Is.EqualTo(TutorialTips.T3Zone));
        }

        [Test]
        public void Seen_RoundTripsThroughArray_AndSkipMarksEverything()
        {
            var a = new TutorialSeen();
            a.Add(TutorialTips.Lost);
            a.Add(TutorialTips.Stool);
            a.Add(TutorialTips.Lost);
            Assert.That(a.Count, Is.EqualTo(2));

            var b = new TutorialSeen();
            b.Load(a.ToArray());
            Assert.That(b.Has(TutorialTips.Lost) && b.Has(TutorialTips.Stool), Is.True);
            Assert.That(b.Has(TutorialTips.NoCup), Is.False);

            b.AddAllTips();
            Assert.That(TutorialRules.PickTip(new TipContext { day = 9, tier = 3, cupsEmpty = true, lostCustomer = true }, b), Is.Null);
            b.Clear();
            Assert.That(b.Count, Is.EqualTo(0));
            b.Load(null);
            Assert.That(b.Count, Is.EqualTo(0));
        }

        [Test]
        public void EveryOrderedTip_HasAnEligibleContext()
        {
            // Mọi mã trong Order phải có luật Eligible (không bỏ sót khi thêm tip mới)
            var all = new TipContext
            {
                day = 9, tier = 2, lostCustomer = true, cupsEmpty = true, bacXiuNew = true, missionClaimable = true, canUpgrade = true,
                canAffordStool = true, dirtyTable = true, wantsMop = true, dirtyCupsCarried = true, canHireCleaner = true,
                canExperiment = true, canAffordZone = true
            };
            var t3 = all;
            t3.tier = 3;
            var t1 = all;
            t1.tier = 1;
            for (int i = 0; i < TutorialTips.Order.Length; i++)
            {
                string id = TutorialTips.Order[i];
                bool any = TutorialRules.Eligible(id, all) || TutorialRules.Eligible(id, t3) || TutorialRules.Eligible(id, t1);
                Assert.That(any, Is.True, "tip " + id + " không bao giờ đủ điều kiện");
            }
        }

        [Test]
        public void CardSlot_AvoidsTheTargetHole()
        {
            const float W = 1920f, H = 1080f, cw = 820f, ch = 230f;
            // Không có lỗ: ô trên
            Assert.That(TutorialRules.PickCardSlot(false, 0, 0, 0, 0, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotTop));
            // Mục tiêu bên trái (Xu chip) không cắt khoảng X của thẻ: vẫn ô trên
            Assert.That(TutorialRules.PickCardSlot(true, 70, 950, 380, 1050, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotTop));
            // Mục tiêu giữa-trên (ngay dưới chip): ô trên bị đè -> ô thấp
            Assert.That(TutorialRules.PickCardSlot(true, 800, 780, 1100, 900, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotLower));
            // Mục tiêu ở bàn pha (thấp, giữa): ô trên
            Assert.That(TutorialRules.PickCardSlot(true, 800, 200, 1000, 340, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotTop));
            // Mục tiêu giữa màn hình: ô được chọn không được đè lỗ
            int slot = TutorialRules.PickCardSlot(true, 800, 500, 1100, 640, W, H, cw, ch, 24f);
            float y0, y1;
            TutorialRules.SlotY(slot, H, ch, out y0, out y1);
            Assert.That(640f + 24f > y0 && 500f - 24f < y1, Is.False, "thẻ đè lỗ");
        }

        [Test]
        public void CardSlot_AvoidsSeveralRects()
        {
            const float W = 1920f, H = 1080f, cw = 820f, ch = 230f;
            // Vùng chỉ giữa-trên + thẻ đơn bên trái: ô thấp vẫn trống
            float[] r = { 800, 780, 1100, 900, 0, 700, 400, 800 };
            Assert.That(TutorialRules.PickCardSlot(r, 2, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotLower));
            // Thêm bảng lớn che ô trên + thấp (bảng tổng kết 1000x800 giữa màn): chỉ còn ô giữa cũng bị che -> mặc định ô trên
            float[] panel = { 460, 140, 1460, 940 };
            Assert.That(TutorialRules.PickCardSlot(panel, 1, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotTop));
            // Không có vùng nào: ô trên
            Assert.That(TutorialRules.PickCardSlot(null, 0, W, H, cw, ch, 24f), Is.EqualTo(TutorialRules.SlotTop));
        }

        [Test]
        public void SideCard_FitsBesideSummaryPanelOnWideScreens()
        {
            float cx;
            // 1920 rộng, bảng 460..1460: dải trái 460 đủ thẻ 420 + lề
            Assert.That(TutorialRules.PickSideCenterX(460f, 1460f, 1920f, 420f, 10f, out cx), Is.True);
            Assert.That(cx, Is.EqualTo(230f).Within(0.01f));
            // Bảng lệch trái: dùng dải phải
            Assert.That(TutorialRules.PickSideCenterX(100f, 1100f, 1920f, 420f, 10f, out cx), Is.True);
            Assert.That(cx, Is.EqualTo(1510f).Within(0.01f));
            // Màn hẹp (4:3, 1440): không bên nào đủ chỗ
            Assert.That(TutorialRules.PickSideCenterX(220f, 1220f, 1440f, 420f, 10f, out cx), Is.False);
        }

        [Test]
        public void ExtraSpawn_OnlyWhenScriptedCustomerIsGoneAndQuotaUsed()
        {
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.TapCustomer, 1, 1, false), Is.True);
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.Brew, 1, 1, false), Is.True);
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.Serve, 1, 1, false), Is.True);
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.TapCustomer, 1, 1, true), Is.False, "còn khách");
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.TapCustomer, 0, 1, false), Is.False, "khách đầu chưa tới");
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.Money, 1, 1, false), Is.False, "đã phục vụ xong");
            Assert.That(TutorialRules.NeedExtraSpawn(TutorialStep.CollectCup, 1, 1, false), Is.False);
        }

        [Test]
        public void Fallback_AfterTooManyRespawnsOrNoProgress()
        {
            Assert.That(TutorialRules.ShouldFallback(0, 10f), Is.False);
            Assert.That(TutorialRules.ShouldFallback(TutorialRules.MaxRespawns, 10f), Is.False);
            Assert.That(TutorialRules.ShouldFallback(TutorialRules.MaxRespawns + 1, 10f), Is.True);
            Assert.That(TutorialRules.ShouldFallback(0, TutorialRules.NoProgressSeconds + 1f), Is.True);
        }

        [Test]
        public void ScriptedPart_NeedsSeats()
        {
            Assert.That(TutorialRules.CanRunScripted(0), Is.False);
            Assert.That(TutorialRules.CanRunScripted(2), Is.True);
        }

        [Test]
        public void EffectiveSpawned_ShortScheduleStillReleasesClock()
        {
            // Lịch chỉ có 2 khách: sau khi cả 2 đã sinh, đồng hồ bước 7 phải chạy
            Assert.That(TutorialRules.ClockHold(TutorialStep.PatienceRing, TutorialRules.EffectiveSpawned(1, 2)), Is.True);
            Assert.That(TutorialRules.ClockHold(TutorialStep.PatienceRing, TutorialRules.EffectiveSpawned(2, 2)), Is.False);
            Assert.That(TutorialRules.EffectiveSpawned(1, 5), Is.EqualTo(1));
        }

        [Test]
        public void VeteranMigration_OnlyOldSavesThatSkipTheTutorial()
        {
            int init = TutorialRules.InitialStep(true, 0, 5, 2);
            Assert.That(TutorialRules.IsVeteranMigration(true, 0, init), Is.True);
            Assert.That(TutorialRules.IsVeteranMigration(false, 0, TutorialStep.NotStarted), Is.False, "game mới");
            Assert.That(TutorialRules.IsVeteranMigration(true, TutorialStep.Done, TutorialStep.Done), Is.False, "đã qua hướng dẫn mới");
            Assert.That(TutorialRules.IsVeteranMigration(true, 0, TutorialRules.InitialStep(true, 0, 1, 1)), Is.False, "mới ở ngày 1");
            int afterSummary = TutorialRules.InitialStep(true, TutorialStep.Summary, 2, 1);
            Assert.That(afterSummary, Is.EqualTo(TutorialStep.Done));
            Assert.That(TutorialRules.IsVeteranMigration(true, TutorialStep.Summary, afterSummary), Is.False, "người mới thoát ở tổng kết ngày 1 vẫn cần gợi ý");
        }

        [Test]
        public void KnownForTier_MarksLowerTiersButKeepsNewerSystems()
        {
            var seen = new TutorialSeen();
            seen.AddKnownForTier(2);
            Assert.That(seen.Has(TutorialTips.NoCup), Is.True);
            Assert.That(seen.Has(TutorialTips.Stool), Is.True);
            Assert.That(seen.Has(TutorialTips.Upgrade1), Is.True, "đã lên cấp 2");
            Assert.That(seen.Has(TutorialTips.T2Mop), Is.True);
            Assert.That(seen.Has(TutorialTips.Upgrade2), Is.False, "chưa lên cấp 3");
            Assert.That(seen.Has(TutorialTips.T3Zone), Is.False);

            var t1 = new TutorialSeen();
            t1.AddKnownForTier(1);
            Assert.That(t1.Has(TutorialTips.Mission), Is.True);
            Assert.That(t1.Has(TutorialTips.Upgrade1), Is.False, "cấp 1 vẫn nên được nhắc lên cấp");
            Assert.That(t1.Has(TutorialTips.T2Table), Is.False);
        }
    }
}
