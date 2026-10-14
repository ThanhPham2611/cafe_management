using NUnit.Framework;

namespace Cafe.Tests
{
    public class DayAdvisorTests
    {
        private static DayAdvice Advise(DaySummary s, int tier = 2, bool cleaner = true,
                                        int debt = 0, bool missionReady = false, DayAdvice last = DayAdvice.None, bool canOpenZone = false)
        {
            return DayAdvisor.Pick(new DayAdvisorInput
            {
                summary = s,
                tier = tier,
                hasCleaner = cleaner,
                debt = debt,
                missionReady = missionReady,
                canOpenZone = canOpenZone,
                lastAdvice = last
            });
        }

        [Test]
        public void QuietDay_GivesNoAdvice()
        {
            Assert.That(Advise(new DaySummary { served = 30, lost = 1, lostNoSeat = 1, cleanliness = 90f }), Is.EqualTo(DayAdvice.None));
        }

        [Test]
        public void ManyLostForNoSeat_SuggestsBuyingSeats()
        {
            Assert.That(Advise(new DaySummary { served = 25, lost = 10, lostNoSeat = 8, cleanliness = 90f }), Is.EqualTo(DayAdvice.BuySeats));
        }

        [Test]
        public void ManyLostForPatience_SuggestsStaff()
        {
            Assert.That(Advise(new DaySummary { served = 25, lost = 10, lostPatience = 7, cleanliness = 90f }), Is.EqualTo(DayAdvice.MoreStaff));
        }

        [Test]
        public void DirtyShop_SuggestsCleaner_ButOnlyWhenCleanlinessMeasured()
        {
            Assert.That(Advise(new DaySummary { served = 25, lost = 2, cleanliness = 60f }), Is.EqualTo(DayAdvice.CleanUp));
            Assert.That(Advise(new DaySummary { served = 25, lost = 2, lostDirty = 4, cleanliness = 75f }), Is.EqualTo(DayAdvice.CleanUp));
            Assert.That(Advise(new DaySummary { served = 25, lost = 2, cleanliness = -1f }, tier: 1), Is.EqualTo(DayAdvice.None));
        }

        [Test]
        public void TierTwoWithoutCleaner_SuggestsHiring()
        {
            var s = new DaySummary { served = 25, lost = 2, cleanliness = 90f, day = 5 };
            Assert.That(Advise(s, cleaner: false), Is.EqualTo(DayAdvice.HireCleaner));
            Assert.That(Advise(s, cleaner: true), Is.EqualTo(DayAdvice.None));
            Assert.That(Advise(s, tier: 1, cleaner: false), Is.EqualTo(DayAdvice.None), "cấp 1 chưa có nhân viên");
        }

        [Test]
        public void Priority_SeatsBeforeStaffBeforeCleaning()
        {
            var s = new DaySummary { served = 20, lost = 18, lostNoSeat = 8, lostPatience = 8, lostDirty = 2, cleanliness = 50f };
            Assert.That(Advise(s), Is.EqualTo(DayAdvice.BuySeats));
            s.lostNoSeat = 0;
            Assert.That(Advise(s), Is.EqualTo(DayAdvice.MoreStaff));
            s.lostPatience = 0;
            Assert.That(Advise(s), Is.EqualTo(DayAdvice.CleanUp));
        }

        [Test]
        public void DebtAndMissionReward_AreMentioned_AfterOperationalProblems()
        {
            var calm = new DaySummary { served = 30, lost = 0, cleanliness = 95f };
            Assert.That(Advise(calm, debt: 500), Is.EqualTo(DayAdvice.Debt));
            Assert.That(Advise(calm, missionReady: true), Is.EqualTo(DayAdvice.ClaimMission));
            Assert.That(Advise(calm, debt: 500, missionReady: true), Is.EqualTo(DayAdvice.Debt));
        }

        [Test]
        public void SameAdviceIsNotRepeatedTwiceInARow()
        {
            var s = new DaySummary { served = 25, lost = 10, lostNoSeat = 8, cleanliness = 90f };
            Assert.That(Advise(s, last: DayAdvice.BuySeats), Is.EqualTo(DayAdvice.None));
            Assert.That(Advise(s, last: DayAdvice.MoreStaff), Is.EqualTo(DayAdvice.BuySeats));
        }

        [Test]
        public void EveryAdviceHasText()
        {
            foreach (DayAdvice a in System.Enum.GetValues(typeof(DayAdvice)))
                if (a != DayAdvice.None) Assert.That(DayAdvisor.Text(a), Is.Not.Empty, a.ToString());
            Assert.That(DayAdvisor.Text(DayAdvice.None), Is.Empty);
        }

        [Test]
        public void EnoughXuForAZone_SuggestsOpeningOne_ButNotTwiceInARow()
        {
            var quiet = new DaySummary { served = 40, lost = 1, lostNoSeat = 1, cleanliness = 90f };
            Assert.That(Advise(quiet, tier: 3, canOpenZone: true), Is.EqualTo(DayAdvice.OpenZone));
            Assert.That(Advise(quiet, tier: 3, canOpenZone: true, last: DayAdvice.OpenZone), Is.EqualTo(DayAdvice.None));
            Assert.That(Advise(quiet, tier: 3, canOpenZone: false), Is.EqualTo(DayAdvice.None));
            Assert.That(Advise(new DaySummary { served = 25, lost = 10, lostNoSeat = 8, cleanliness = 90f }, tier: 3, canOpenZone: true),
                Is.EqualTo(DayAdvice.BuySeats), "vấn đề vận hành được ưu tiên hơn việc mở khu");
        }
    }
}
