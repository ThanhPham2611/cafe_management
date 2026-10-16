using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class SpecialOrdersTests
    {
        private static RecipeDef NewRecipe() { return (RecipeDef)FormatterServices.GetUninitializedObject(typeof(RecipeDef)); }

        [TestCase(1, 0, 0.0f, false)]   // Vỉa hè: không có đơn đặc biệt
        [TestCase(2, 0, 0.0f, false)]   // Mặt bằng: không có
        [TestCase(3, 0, 0.05f, true)]   // Nhà hàng, trúng 10%
        [TestCase(3, 0, 0.099f, true)]
        [TestCase(3, 0, 0.10f, false)]  // trượt
        [TestCase(3, 0, 0.9f, false)]
        [TestCase(3, 1, 0.01f, true)]
        [TestCase(3, 2, 0.01f, false)]  // đã đủ 2 đơn đặc biệt: chủ quán không quá tải
        public void ShouldBeSpecial_OnlyAtTierThree_RareAndCapped(int tier, int active, float roll, bool expected)
        {
            Assert.That(SpecialOrders.ShouldBeSpecial(tier, active, roll), Is.EqualTo(expected));
        }

        [Test]
        public void Count_CountsOnlyOwnerOnlyOrders()
        {
            var list = new List<Order> { new Order(), new Order { ownerOnly = true }, null, new Order { ownerOnly = true } };
            Assert.That(SpecialOrders.Count(list), Is.EqualTo(2));
            Assert.That(SpecialOrders.Count(null), Is.Zero);
        }

        [Test]
        public void NewOrders_DefaultToNormalReward()
        {
            var o = new Order();
            Assert.That(o.ownerOnly, Is.False);
            Assert.That(o.rewardMult, Is.EqualTo(1f));
            Assert.That(SpecialOrders.RewardMult, Is.EqualTo(2f));
        }

        [Test]
        public void Brewer_NeverTakesAnOwnerOnlyOrder()
        {
            RecipeDef r = NewRecipe();
            var special = new Order { id = 1, recipe = r, ownerOnly = true };
            var normal = new Order { id = 2, recipe = r };
            var queue = new List<Order> { special, normal };
            Assert.That(OrderPicker.NextForBrewer(queue, null, null, null), Is.SameAs(normal), "đơn đặc biệt cũ hơn nhưng nhân viên bỏ qua");
            Assert.That(OrderPicker.NextForBrewer(new List<Order> { special }, null, null, null), Is.Null);
        }

        [Test]
        public void Server_PrefersTheSpecialOrderOfThatRecipe_ThenOldest()
        {
            RecipeDef a = NewRecipe();
            RecipeDef b = NewRecipe();
            var old = new Order { id = 1, recipe = a };
            var otherDrink = new Order { id = 2, recipe = b, ownerOnly = true };
            var special = new Order { id = 3, recipe = a, ownerOnly = true };
            var queue = new List<Order> { old, otherDrink, special };
            Assert.That(OrderPicker.NextForServer(queue, a, null), Is.SameAs(special), "ly pha cho đơn đặc biệt không bị mang cho đơn thường cũ hơn");
            Assert.That(OrderPicker.NextForServer(queue, b, null), Is.SameAs(otherDrink));
            Assert.That(OrderPicker.NextForServer(new List<Order> { old }, a, null), Is.SameAs(old), "không có đơn đặc biệt: đơn cũ nhất như trước");
        }

        [Test]
        public void Server_SkipsSpecialOrdersWhoseCustomerLeft()
        {
            RecipeDef a = NewRecipe();
            var gone = new Order { id = 1, recipe = a, ownerOnly = true };
            var waiting = new Order { id = 2, recipe = a };
            var queue = new List<Order> { gone, waiting };
            Assert.That(OrderPicker.NextForServer(queue, a, o => o != gone), Is.SameAs(waiting));
        }
    }
}
