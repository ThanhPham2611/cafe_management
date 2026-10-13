using NUnit.Framework;

namespace Cafe.Tests
{
    public class StaffRosterTests
    {
        private Wallet wallet;
        private RentLedger ledger;
        private StaffRoster roster;

        [SetUp]
        public void SetUp()
        {
            wallet = new Wallet();
            wallet.Init(10000);
            ledger = new RentLedger();
            ledger.Reset();
            roster = new StaffRoster(wallet, ledger);
            roster.Load(null);
        }

        [Test]
        public void Hire_ChargesExactFee_AndStopsAtRoleCap()
        {
            var tier2 = new StaffRoster(wallet, ledger, () => 2);
            tier2.Load(null);
            Assert.That(tier2.Hire(StaffRole.Cleaner, 0, "Lan"), Is.True);
            Assert.That(wallet.Xu, Is.EqualTo(8500));
            Assert.That(tier2.Hire(StaffRole.Cleaner, 1, "Nam"), Is.False, "cấp 2 chỉ 1 Dọn dẹp (đo: người thứ hai không thêm khách)");
            Assert.That(wallet.Xu, Is.EqualTo(8500));

            var tier3 = new StaffRoster(wallet, ledger, () => 3);
            tier3.Load(null);
            Assert.That(tier3.Hire(StaffRole.Cleaner, 0, "A"), Is.True);
            Assert.That(tier3.Hire(StaffRole.Cleaner, 1, "B"), Is.True);
            Assert.That(tier3.Hire(StaffRole.Cleaner, 1, "C"), Is.True);
            Assert.That(tier3.Hire(StaffRole.Cleaner, 1, "D"), Is.False, "cấp 3 tối đa 3 Dọn dẹp");
            Assert.That(tier3.CountOf(StaffRole.Cleaner), Is.EqualTo(3));
        }

        [Test]
        public void Hire_BrewerOnlyFromTier3()
        {
            var tier2 = new StaffRoster(wallet, ledger, () => 2);
            tier2.Load(null);
            Assert.That(tier2.CanHire(StaffRole.Brewer), Is.False);
            Assert.That(tier2.Hire(StaffRole.Brewer, 1, "Bảo"), Is.False);
            Assert.That(wallet.Xu, Is.EqualTo(10000));

            var tier3 = new StaffRoster(wallet, ledger, () => 3);
            tier3.Load(null);
            Assert.That(tier3.Hire(StaffRole.Brewer, 1, null), Is.True);
            Assert.That(tier3.Hire(StaffRole.Brewer, 2, null), Is.True);
            Assert.That(tier3.Hire(StaffRole.Brewer, 3, null), Is.True);
            Assert.That(tier3.Hire(StaffRole.Brewer, 3, null), Is.False, "tối đa 3 Pha chế ở cấp 3");
            Assert.That(tier3.Members[0].name, Is.Not.EqualTo(tier3.Members[1].name), "mỗi người một tên mặc định");
        }

        [Test]
        public void Hire_RejectedBelowTier2()
        {
            var tier1 = new StaffRoster(wallet, ledger, () => 1);
            tier1.Load(null);
            Assert.That(tier1.Hire(StaffRole.Cleaner, 0, "Lan"), Is.False);
        }

        [Test]
        public void IndexOf_FindsKthMemberOfRole()
        {
            var tier3 = new StaffRoster(wallet, ledger, () => 3);
            tier3.Load(null);
            tier3.Hire(StaffRole.Cleaner, 0, "A");
            tier3.Hire(StaffRole.Server, 0, "B");
            tier3.Hire(StaffRole.Cleaner, 0, "C");
            Assert.That(tier3.IndexOf(StaffRole.Cleaner, 0), Is.EqualTo(0));
            Assert.That(tier3.IndexOf(StaffRole.Cleaner, 1), Is.EqualTo(2));
            Assert.That(tier3.IndexOf(StaffRole.Cleaner, 2), Is.EqualTo(-1));
            Assert.That(tier3.IndexOf(StaffRole.Server, 0), Is.EqualTo(1));
        }

        [Test]
        public void Upgrade_ChargesLevelCost_AndStopsAtFive()
        {
            Assert.That(roster.Hire(StaffRole.Server, 0, "Minh"), Is.True);
            Assert.That(roster.Upgrade(0), Is.True);
            Assert.That(wallet.Xu, Is.EqualTo(7500));
            Assert.That(roster.Members[0].level, Is.EqualTo(2));

            var data = new SaveData
            {
                staff = new[] { new StaffSave { role = "serve", preset = 0, level = 5, name = "Minh" } }
            };
            roster.Load(data);
            Assert.That(roster.Upgrade(0), Is.False);
        }

        [Test]
        public void Load_SkipsUnknownRole_AndClampsUnsafeValues()
        {
            var data = new SaveData
            {
                staff = new[]
                {
                    new StaffSave { role = "unknown", preset = 99, level = -4, name = "Bad" },
                    new StaffSave { role = "clean", preset = 99, level = 12, name = "" }
                }
            };

            roster.Load(data);

            Assert.That(roster.Count, Is.EqualTo(1));
            Assert.That(roster.Members[0].role, Is.EqualTo("clean"));
            Assert.That(roster.Members[0].preset, Is.EqualTo(3));
            Assert.That(roster.Members[0].level, Is.EqualTo(5));
            Assert.That(roster.Members[0].name, Is.Not.Empty);
        }

        [Test]
        public void PayDailyWages_SpendsAvailableXu_AndDebtsOnlyShortfall()
        {
            wallet.Init(2000);
            Assert.That(roster.Hire(StaffRole.Cleaner, 0, "Lan"), Is.True);
            wallet.Init(100);
            var summary = new DaySummary();

            int paid = roster.PayDailyWages(summary);

            Assert.That(paid, Is.EqualTo(100));
            Assert.That(summary.staffWages, Is.EqualTo(100));
            Assert.That(ledger.Debt, Is.EqualTo(200));
            Assert.That(wallet.Xu, Is.Zero);
        }

        [Test]
        public void WriteToSave_RoundTripsMembers()
        {
            Assert.That(roster.Hire(StaffRole.Server, 2, "Hoa"), Is.True);
            var saved = new SaveData();
            roster.WriteToSave(saved);

            var copy = new StaffRoster(new Wallet(), new RentLedger());
            copy.Load(saved);

            Assert.That(copy.Count, Is.EqualTo(1));
            Assert.That(copy.Members[0].role, Is.EqualTo("serve"));
            Assert.That(copy.Members[0].preset, Is.EqualTo(2));
            Assert.That(copy.Members[0].name, Is.EqualTo("Hoa"));
        }
    }
}
