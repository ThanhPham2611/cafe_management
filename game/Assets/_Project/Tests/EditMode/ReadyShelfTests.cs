using NUnit.Framework;

namespace Cafe.Tests
{
    public class ReadyShelfTests
    {
        private int discarded;
        private ReadyShelf shelf;
        private readonly object brewer = new object();
        private readonly object server = new object();

        [SetUp]
        public void SetUp()
        {
            discarded = 0;
            shelf = new ReadyShelf(() => discarded++);
        }

        private static Order NewOrder(int id)
        {
            return new Order { id = id };
        }

        [Test]
        public void TryAdd_MovesTheClaimFromBrewerToShelf()
        {
            Order o = NewOrder(1);
            Assert.That(o.TryClaim(brewer), Is.True);

            Assert.That(shelf.TryAdd(o, 2, brewer), Is.True);

            Assert.That(shelf.Count, Is.EqualTo(1));
            Assert.That(o.IsClaimed, Is.True);
            Assert.That(o.TryClaim(brewer), Is.False, "Pha chế không còn giữ đơn này");
        }

        [Test]
        public void TryAdd_RejectsWhenFull_OrBadInput()
        {
            for (int i = 0; i < ReadyShelf.DefaultCapacity; i++)
            {
                Order o = NewOrder(i);
                o.TryClaim(brewer);
                Assert.That(shelf.TryAdd(o, 2, brewer), Is.True);
            }
            Assert.That(shelf.HasRoom, Is.False);

            Order extra = NewOrder(99);
            extra.TryClaim(brewer);
            Assert.That(shelf.TryAdd(extra, 2, brewer), Is.False, "quầy đầy");
            Assert.That(shelf.TryAdd(null, 2, brewer), Is.False);
            Assert.That(shelf.TryAdd(NewOrder(100), 0, brewer), Is.False, "ly hỏng không đặt lên quầy");
            Assert.That(shelf.Count, Is.EqualTo(ReadyShelf.DefaultCapacity));
        }

        [Test]
        public void TryTake_GivesOldestServableDrink_AndTransfersClaim()
        {
            Order first = NewOrder(1), second = NewOrder(2);
            first.TryClaim(brewer); shelf.TryAdd(first, 2, brewer);
            second.TryClaim(brewer); shelf.TryAdd(second, 3, brewer);

            ReadyShelf.Drink d;
            Assert.That(shelf.TryTake(server, o => ReferenceEquals(o, second), out d), Is.True, "bỏ qua ly chưa phục vụ được");
            Assert.That(ReferenceEquals(d.order, second), Is.True);
            Assert.That(d.quality, Is.EqualTo(3));
            Assert.That(second.TryClaim(server), Is.True, "đơn thuộc về Phục vụ");
            Assert.That(shelf.Count, Is.EqualTo(1));

            Assert.That(shelf.TryTake(server, o => true, out d), Is.True);
            Assert.That(ReferenceEquals(d.order, first), Is.True);
            Assert.That(shelf.TryTake(server, o => true, out d), Is.False, "quầy trống");
        }

        [Test]
        public void TryTake_NothingServable_LeavesShelfUntouched()
        {
            Order o = NewOrder(1);
            o.TryClaim(brewer);
            shelf.TryAdd(o, 2, brewer);

            ReadyShelf.Drink d;
            Assert.That(shelf.TryTake(server, x => false, out d), Is.False);
            Assert.That(shelf.Count, Is.EqualTo(1));
            Assert.That(o.IsClaimed, Is.True, "đơn vẫn do quầy giao giữ");
        }

        [Test]
        public void Prune_DropsDrinksWhoseCustomerLeft_AndReportsEachDiscard()
        {
            Order stay = NewOrder(1), gone = NewOrder(2), gone2 = NewOrder(3);
            foreach (Order o in new[] { stay, gone, gone2 })
            {
                o.TryClaim(brewer);
                shelf.TryAdd(o, 2, brewer);
            }

            int removed = shelf.Prune(o => ReferenceEquals(o, stay));

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(discarded, Is.EqualTo(2), "mỗi ly bỏ trả một ly sạch về kho");
            Assert.That(shelf.Count, Is.EqualTo(1));
            Assert.That(gone.IsClaimed, Is.False, "đơn của khách đã đi được nhả");
        }

        [Test]
        public void Clear_RemovesEverything_AndDiscardsAll()
        {
            for (int i = 0; i < 3; i++)
            {
                Order o = NewOrder(i);
                o.TryClaim(brewer);
                shelf.TryAdd(o, 2, brewer);
            }
            Assert.That(shelf.Clear(), Is.EqualTo(3));
            Assert.That(shelf.Count, Is.Zero);
            Assert.That(discarded, Is.EqualTo(3));
        }
    }
}
