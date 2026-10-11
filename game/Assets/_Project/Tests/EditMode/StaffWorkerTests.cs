using NUnit.Framework;
using System.Runtime.Serialization;

namespace Cafe.Tests
{
    public class StaffWorkerTests
    {
        private static WorkJob NewJob()
        {
            return (WorkJob)FormatterServices.GetUninitializedObject(typeof(WorkJob));
        }

        [SetUp]
        public void SetUp()
        {
            WorkQueue.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            WorkQueue.Clear();
        }

        [TestCase(1, 6f)]
        [TestCase(2, 5f)]
        [TestCase(3, 4f)]
        [TestCase(4, 3f)]
        [TestCase(5, 2.2f)]
        public void CleanerSeconds_MatchesEconomyTable(int level, float expected)
        {
            Assert.That(StaffWorkerRules.CleanSeconds(level), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Lease_DetectsOwnerSteal_WithoutReleasingOwnersClaim()
        {
            object staff = new object();
            object owner = new object();
            WorkJob job = NewJob();
            var lease = new StaffJobLease(staff);

            Assert.That(lease.TryClaim(job), Is.True);
            Assert.That(WorkQueue.Claim(job, owner, true), Is.True);
            Assert.That(lease.StillOwned, Is.False);

            lease.Release();
            Assert.That(job.claimedBy, Is.SameAs(owner));
        }

        [Test]
        public void Lease_ReleasesOwnJob_ForAnotherWorker()
        {
            object staff = new object();
            WorkJob job = NewJob();
            var lease = new StaffJobLease(staff);

            Assert.That(lease.TryClaim(job), Is.True);
            lease.Release();

            Assert.That(job.claimedBy, Is.Null);
            Assert.That(job.finished, Is.False);
        }

        [Test]
        public void Lease_Complete_RemovesJobExactlyOnce()
        {
            WorkJob job = NewJob();
            var lease = new StaffJobLease(new object());
            Assert.That(lease.TryClaim(job), Is.True);

            lease.Complete();
            lease.Complete();

            Assert.That(job.finished, Is.True);
            Assert.That(job.claimedBy, Is.Null);
        }
    }
}
