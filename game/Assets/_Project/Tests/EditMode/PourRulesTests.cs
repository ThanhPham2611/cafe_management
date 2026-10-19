using System;
using NUnit.Framework;

namespace Cafe.Tests
{
    /// Thao tác rót mới (docs/hud_brew_redesign_2026-10-18.md B1/B3): chấm điểm theo thời điểm chạm, giới hạn sao, ngân sách 3 sao.
    public class PourRulesTests
    {
        private static PourGrade? Stop(float elapsed, float t)
        {
            PourGrade g;
            return PourRules.TryGradeStop(elapsed, t, out g) ? g : (PourGrade?)null;
        }

        [Test]
        public void PourSeconds_MatchSpec_AndOnlyForPourSteps()
        {
            Assert.That(StepActions.PourSeconds(BrewStep.Phin), Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(StepActions.PourSeconds(BrewStep.CotDua), Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(StepActions.PourSeconds(BrewStep.SuaDac), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(StepActions.PourSeconds(BrewStep.SuaTuoi), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(StepActions.PourSeconds(BrewStep.Tra), Is.EqualTo(0.8f).Within(1e-4f));
            foreach (BrewStep s in Enum.GetValues(typeof(BrewStep)))
            {
                float t = StepActions.PourSeconds(s);
                if (StepActions.For(s) == StepAction.Hold) Assert.That(t, Is.InRange(0.6f, 1.2f), s.ToString());
                else Assert.That(t, Is.EqualTo(0f), s.ToString());
            }
        }

        [Test]
        public void BandConstants_AreOrdered_AndWindowIsAtLeast200ms()
        {
            Assert.That(StepActions.UnderIgnoreFraction, Is.LessThan(StepActions.PerfectBandStart));
            Assert.That(StepActions.PerfectBandStart, Is.LessThan(StepActions.PerfectBandEnd));
            Assert.That(StepActions.PerfectBandEnd, Is.LessThan(1f));
            foreach (BrewStep s in Enum.GetValues(typeof(BrewStep)))
            {
                float t = StepActions.PourSeconds(s);
                if (t <= 0f) continue;
                float window = PourRules.BandEndSeconds(t) - PourRules.BandStartSeconds(t);
                Assert.That(window, Is.GreaterThanOrEqualTo(0.2f - 1e-4f), s.ToString());
            }
            // Phin: vạch vàng 0,60-0,85 s
            Assert.That(PourRules.BandStartSeconds(1f), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(PourRules.BandEndSeconds(1f), Is.EqualTo(0.85f).Within(1e-4f));
        }

        [Test]
        public void Stop_TooEarly_IsIgnored()
        {
            Assert.That(Stop(0f, 1f), Is.Null);
            Assert.That(Stop(0.149f, 1f), Is.Null);
            Assert.That(Stop(0.11f, 0.8f), Is.Null); // 15% của 0,8 = 0,12
        }

        [Test]
        public void Stop_BeforeBand_IsUnder()
        {
            Assert.That(Stop(0.15f, 1f), Is.EqualTo(PourGrade.Under));
            Assert.That(Stop(0.59f, 1f), Is.EqualTo(PourGrade.Under));
            Assert.That(Stop(0.47f, 0.8f), Is.EqualTo(PourGrade.Under));
        }

        [Test]
        public void Stop_InBand_PlusGrace_IsPerfect()
        {
            Assert.That(Stop(0.60f, 1f), Is.EqualTo(PourGrade.Perfect));
            Assert.That(Stop(0.72f, 1f), Is.EqualTo(PourGrade.Perfect));
            Assert.That(Stop(0.85f, 1f), Is.EqualTo(PourGrade.Perfect));
            Assert.That(Stop(0.85f + StepActions.PerfectGraceSeconds - 0.001f, 1f), Is.EqualTo(PourGrade.Perfect));
            Assert.That(Stop(0.50f, 0.8f), Is.EqualTo(PourGrade.Perfect)); // 0,48-0,68 (+0,05)
        }

        [Test]
        public void Stop_AfterBand_IsGood()
        {
            Assert.That(Stop(0.85f + StepActions.PerfectGraceSeconds + 0.01f, 1f), Is.EqualTo(PourGrade.Good));
            Assert.That(Stop(0.99f, 1f), Is.EqualTo(PourGrade.Good));
            Assert.That(Stop(0.75f, 0.8f), Is.EqualTo(PourGrade.Good));
        }

        [Test]
        public void Fill_AndFull_TrackElapsed()
        {
            Assert.That(PourRules.Fill(0f, 1f), Is.EqualTo(0f));
            Assert.That(PourRules.Fill(0.5f, 1f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(PourRules.Fill(3f, 1f), Is.EqualTo(1f));
            Assert.That(PourRules.IsFull(0.99f, 1f), Is.False);
            Assert.That(PourRules.IsFull(1f, 1f), Is.True);
            Assert.That(PourRules.IsFull(0f, 0f), Is.True);
        }

        [Test]
        public void Quality_UnderCapsAtTwo_WrongIsOne_NeverRuinedByPour()
        {
            Assert.That(PourRules.Quality(0, 0, true), Is.EqualTo(3));
            Assert.That(PourRules.Quality(0, 0, false), Is.EqualTo(2));
            Assert.That(PourRules.Quality(0, 1, true), Is.EqualTo(2));
            Assert.That(PourRules.Quality(0, 5, true), Is.EqualTo(2), "rót ít bao nhiêu lần cũng không hỏng ly");
            Assert.That(PourRules.Quality(1, 0, true), Is.EqualTo(1));
            Assert.That(PourRules.Quality(1, 3, false), Is.EqualTo(1));
        }

        [Test]
        public void PerfectPourDrink_NeedsPoursAllPerfect_AndThreeStars()
        {
            Assert.That(PourRules.IsPerfectPourDrink(1, 1, 3), Is.True);
            Assert.That(PourRules.IsPerfectPourDrink(2, 2, 3), Is.True);
            Assert.That(PourRules.IsPerfectPourDrink(2, 1, 3), Is.False);
            Assert.That(PourRules.IsPerfectPourDrink(0, 0, 3), Is.False, "ly không có bước rót");
            Assert.That(PourRules.IsPerfectPourDrink(1, 1, 2), Is.False);
        }

        [Test]
        public void MinPerfectSeconds_Is1_3TimesBudgetPlusReaction()
        {
            Assert.That(PourRules.MinPerfectSeconds(null), Is.EqualTo(0f));
            Assert.That(PourRules.MinPerfectSeconds(new BrewStep[0]), Is.EqualTo(0f));
            // Cà phê đen: Phin 1,0 + Đá 0,6 + 1,0 phản xạ = 2,6 x 1,3 = 3,38
            Assert.That(PourRules.MinPerfectSeconds(new[] { BrewStep.Phin, BrewStep.Da }), Is.EqualTo(3.38f).Within(1e-3f));
            // Trà đá: Trà 0,8 + Đá 0,6 + 1 = 2,4 x 1,3 = 3,12 (brewSeconds 2 cũ quá gắt với rót tự đầy)
            Assert.That(PourRules.MinPerfectSeconds(new[] { BrewStep.Tra, BrewStep.Da }), Is.EqualTo(3.12f).Within(1e-3f));
            // Vuốt / xoay dùng ngân sách riêng
            Assert.That(PourRules.StepBudgetSeconds(BrewStep.KemMuoi), Is.EqualTo(PourRules.SwipeBudgetSeconds));
            Assert.That(PourRules.StepBudgetSeconds(BrewStep.Lac), Is.EqualTo(PourRules.SpinBudgetSeconds));
            Assert.That(PourRules.StepBudgetSeconds(BrewStep.Duong), Is.EqualTo(PourRules.TapBudgetSeconds));
        }
    }
}
