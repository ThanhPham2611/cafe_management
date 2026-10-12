using NUnit.Framework;

namespace Cafe.Tests
{
    public class CupStockTests
    {
        [Test]
        public void EnsureTotal_AddsOnlyTheMissingCups_AndNeverRemoves()
        {
            var stock = new CupStock();
            stock.Init(6);
            Assert.That(stock.Total, Is.EqualTo(6));

            stock.TryTake();
            stock.TryTake();
            stock.EnsureTotal(9);

            Assert.That(stock.Total, Is.EqualTo(9));
            Assert.That(stock.Count, Is.EqualTo(7), "2 ly đang dùng + 3 ly mới => 4 + 3 trong kho");

            stock.EnsureTotal(5);
            Assert.That(stock.Total, Is.EqualTo(9));
            Assert.That(stock.Count, Is.EqualTo(7));
        }

        [Test]
        public void EnsureTotal_RaisesChangedOnlyWhenCupsAdded()
        {
            var stock = new CupStock();
            stock.Init(6);
            int changed = 0;
            stock.Changed += () => changed++;

            stock.EnsureTotal(6);
            Assert.That(changed, Is.Zero);
            stock.EnsureTotal(7);
            Assert.That(changed, Is.EqualTo(1));
        }

        [TestCase(1, 4, 6)]    // vỉa hè giữ nguyên 6 ly khi ít ghế
        [TestCase(1, 6, 6)]
        [TestCase(1, 12, 12)]  // nhiều ghế thì ít nhất 1 ly/ghế
        [TestCase(2, 6, 9)]    // cấp 2: 1,5 ly/ghế (ly nằm trên bàn bẩn chờ dọn)
        [TestCase(2, 14, 21)]
        [TestCase(2, 3, 6)]    // không bao giờ ít hơn số ly khởi đầu
        public void CupsFor_ScalesWithSeatsAndTier(int tier, int seats, int expected)
        {
            Assert.That(GameBalance.CupsFor(tier, seats), Is.EqualTo(expected));
        }
    }
}
