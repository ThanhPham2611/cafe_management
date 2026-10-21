using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class BrewerRulesTests
    {
        private static RecipeDef NewRecipe()
        {
            return (RecipeDef)FormatterServices.GetUninitializedObject(typeof(RecipeDef));
        }

        private static Order NewOrder(int id, RecipeDef recipe)
        {
            return new Order { id = id, recipe = recipe };
        }

        [TestCase(1, 9f)]
        [TestCase(2, 7.5f)]
        [TestCase(3, 6.25f)]
        [TestCase(4, 5.5f)]
        [TestCase(5, 5f)]
        public void BrewSeconds_ScalesWithLevel(int level, float expected)
        {
            Assert.That(BrewerRules.BrewSeconds(5f, level), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void BrewSeconds_ClampsLevelAndNeverBelowOneSecond()
        {
            Assert.That(BrewerRules.BrewSeconds(5f, 0), Is.EqualTo(9f).Within(0.001f));
            Assert.That(BrewerRules.BrewSeconds(5f, 99), Is.EqualTo(5f).Within(0.001f));
            Assert.That(BrewerRules.BrewSeconds(0f, 1), Is.EqualTo(1f).Within(0.001f));
        }

        [TestCase(1, true, 2)]
        [TestCase(2, true, 2)]
        [TestCase(3, true, 3)]
        [TestCase(5, true, 3)]
        [TestCase(3, false, 2)]
        [TestCase(5, false, 2)]
        public void Quality_ThreeStarsNeedsLevelThreeAndGoodTools(int level, bool toolsOk, int expected)
        {
            Assert.That(BrewerRules.Quality(level, toolsOk), Is.EqualTo(expected));
        }

        [Test]
        public void OrderClaim_IsExclusive_AndOnlyHolderCanRelease()
        {
            var order = NewOrder(1, null);
            object barista = new object();
            object other = new object();

            Assert.That(order.TryClaim(barista), Is.True);
            Assert.That(order.IsClaimed, Is.True);
            Assert.That(order.TryClaim(other), Is.False);
            Assert.That(order.TryClaim(barista), Is.True, "claim lại bởi chính người giữ vẫn ổn");

            order.Release(other);
            Assert.That(order.IsClaimed, Is.True);
            order.Release(barista);
            Assert.That(order.IsClaimed, Is.False);
        }

        [Test]
        public void NextForBrewer_TakesOldestWaitingUnclaimedOrder()
        {
            var a = NewOrder(1, NewRecipe());
            var b = NewOrder(2, NewRecipe());
            var c = NewOrder(3, NewRecipe());
            a.TryClaim(new object());
            var list = new List<Order> { a, b, c };

            Order pick = OrderPicker.NextForBrewer(list, null, null, o => true);

            Assert.That(pick, Is.SameAs(b));
        }

        [Test]
        public void NextForBrewer_SkipsOrdersWhoseCustomerIsNotWaiting()
        {
            var a = NewOrder(1, NewRecipe());
            var b = NewOrder(2, NewRecipe());
            var list = new List<Order> { a, b };

            Order pick = OrderPicker.NextForBrewer(list, null, null, o => o == b);

            Assert.That(pick, Is.SameAs(b));
        }

        [Test]
        public void NextForBrewer_LeavesOwnersSelectedOrderAlone()
        {
            var a = NewOrder(1, NewRecipe());
            var b = NewOrder(2, NewRecipe());
            var list = new List<Order> { a, b };

            Order pick = OrderPicker.NextForBrewer(list, a, null, o => true);

            Assert.That(pick, Is.SameAs(b));
        }

        [Test]
        public void NextForBrewer_LeavesOrdersTheOwnersReadyDrinkCanServe()
        {
            RecipeDef latte = NewRecipe();
            RecipeDef tea = NewRecipe();
            var a = NewOrder(1, latte);
            var b = NewOrder(2, tea);
            var list = new List<Order> { a, b };

            Order pick = OrderPicker.NextForBrewer(list, null, latte, o => true);

            Assert.That(pick, Is.SameAs(b));
        }

        [Test]
        public void NextForBrewer_SkipsOrdersThatCannotFinishInTime()
        {
            var doomed = NewOrder(1, NewRecipe());
            var ok = NewOrder(2, NewRecipe());
            var list = new List<Order> { doomed, ok };

            Order pick = OrderPicker.NextForBrewer(list, null, null, o => true, o => o != doomed);

            Assert.That(pick, Is.SameAs(ok), "đơn cũ nhất sắp hết kiên nhẫn thì nhường đơn kế còn kịp");
        }

        [Test]
        public void SameRecipeWaiting_FindsOldestUnclaimedSameDrink()
        {
            RecipeDef latte = NewRecipe();
            var other = NewOrder(1, NewRecipe());
            var claimed = NewOrder(2, latte);
            claimed.TryClaim(new object());
            var special = NewOrder(3, latte);
            special.ownerOnly = true;
            var ok = NewOrder(4, latte);
            var list = new List<Order> { other, claimed, special, ok };

            Assert.That(OrderPicker.SameRecipeWaiting(list, latte, o => true), Is.SameAs(ok));
            Assert.That(OrderPicker.SameRecipeWaiting(list, latte, o => o != ok), Is.Null);
        }

        [Test]
        public void CanFinish_NeedsBrewTimePlusDeliveryMargin()
        {
            Assert.That(BrewerRules.CanFinish(20f, 10f), Is.True);
            Assert.That(BrewerRules.CanFinish(10f + BrewerRules.DeliveryMargin, 10f), Is.False);
            Assert.That(BrewerRules.CanFinish(3f, 10f), Is.False);
        }

        [Test]
        public void NextForBrewer_ReturnsNull_WhenNothingIsAvailable()
        {
            var a = NewOrder(1, NewRecipe());
            var list = new List<Order> { a };

            Assert.That(OrderPicker.NextForBrewer(list, a, null, o => true), Is.Null);
            Assert.That(OrderPicker.NextForBrewer(new List<Order>(), null, null, o => true), Is.Null);
            Assert.That(OrderPicker.NextForBrewer(null, null, null, o => true), Is.Null);
        }

        [Test]
        public void NextForServer_PicksOldestWaitingOrderMatchingTheReadyDrink()
        {
            RecipeDef latte = NewRecipe();
            RecipeDef tea = NewRecipe();
            var a = NewOrder(1, tea);
            var b = NewOrder(2, latte);
            var c = NewOrder(3, latte);
            var list = new List<Order> { a, b, c };

            Order pick = OrderPicker.NextForServer(list, latte, o => true);

            Assert.That(pick, Is.SameAs(b));
        }

        [Test]
        public void NextForServer_SkipsClaimedOrdersAndMissingDrink()
        {
            RecipeDef latte = NewRecipe();
            var a = NewOrder(1, latte);
            var b = NewOrder(2, latte);
            a.TryClaim(new object());
            var list = new List<Order> { a, b };

            Assert.That(OrderPicker.NextForServer(list, latte, o => true), Is.SameAs(b));
            Assert.That(OrderPicker.NextForServer(list, null, o => true), Is.Null);
        }

        [Test]
        public void Brewer_RoleIdRoundTrips_AndHasEconomyDocNumbers()
        {
            StaffRole role;
            Assert.That(StaffRoles.TryParse("brew", out role), Is.True);
            Assert.That(role, Is.EqualTo(StaffRole.Brewer));
            Assert.That(StaffRoles.ToId(StaffRole.Brewer), Is.EqualTo("brew"));
            Assert.That(GameBalance.StaffHireFee(StaffRole.Brewer), Is.EqualTo(2500));
            Assert.That(GameBalance.StaffDailyWage(StaffRole.Brewer), Is.EqualTo(450));
            Assert.That(StaffRoles.DefaultName(StaffRole.Brewer), Is.Not.Empty);
        }

        [Test]
        public void Roster_HiresBrewer_ChargesFeeAndPaysWage()
        {
            var wallet = new Wallet();
            wallet.Init(10000);
            var ledger = new RentLedger();
            ledger.Reset();
            var roster = new StaffRoster(wallet, ledger);
            roster.Load(null);

            Assert.That(roster.Hire(StaffRole.Brewer, 1, "Anh Bảo"), Is.True);
            Assert.That(wallet.Xu, Is.EqualTo(7500));
            var summary = new DaySummary();
            Assert.That(roster.PayDailyWages(summary), Is.EqualTo(450));
            Assert.That(summary.staffWages, Is.EqualTo(450));
        }
    }
}
