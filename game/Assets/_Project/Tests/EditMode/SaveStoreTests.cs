using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class SaveStoreTests
    {
        private string dir;
        private SaveStore store;

        private string Main { get { return Path.Combine(dir, SaveStore.MainName); } }
        private string Bak { get { return Main + ".bak"; } }
        private string Tmp { get { return Main + ".tmp"; } }

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "cafe_save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            store = new SaveStore(dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        private static SaveData Data(int xu)
        {
            return JsonUtility.FromJson<SaveData>(SaveFixtures.V3.Replace("\"xu\":5400", "\"xu\":" + xu));
        }

        private string[] Corrupts() { return Directory.GetFiles(dir, "*.corrupt-*"); }

        [Test]
        public void Load_NoFiles_Missing()
        {
            Assert.That(store.Load().status, Is.EqualTo(SaveStatus.Missing));
        }

        [Test]
        public void SaveThenLoad_Ok_FieldsEqual()
        {
            Assert.That(store.Save(Data(777)), Is.True);
            SaveLoadResult r = new SaveStore(dir).Load();
            Assert.That(r.status, Is.EqualTo(SaveStatus.Ok));
            Assert.That(r.data.xu, Is.EqualTo(777));
            Assert.That(r.data.shopName, Is.EqualTo("Quán Cà Phê Việt"));
        }

        [Test]
        public void Legacy_BareV1V2V3_LoadAndMigrateToCurrent()
        {
            foreach (string json in new[] { SaveFixtures.V1, SaveFixtures.V2, SaveFixtures.V3 })
            {
                File.WriteAllText(Main, json);
                SaveLoadResult r = new SaveStore(dir).Load();
                Assert.That(r.status, Is.EqualTo(SaveStatus.Ok));
                Assert.That(r.data.version, Is.EqualTo(SaveData.CurrentVersion));
            }
            File.WriteAllText(Main, SaveFixtures.V1);
            SaveData v1 = new SaveStore(dir).Load().data;
            Assert.That(v1.creatorDone, Is.False);
            Assert.That(v1.tier, Is.EqualTo(1));
            Assert.That(v1.knownRecipes.Length, Is.EqualTo(4));
        }

        [Test]
        public void FirstSave_NoBak_SecondSave_BakHoldsFirst_ThirdRotates()
        {
            store.Save(Data(1));
            Assert.That(File.Exists(Bak), Is.False);
            store.Save(Data(2));
            Assert.That(SaveEnvelope.Parse(File.ReadAllBytes(Bak), out SaveData b1), Is.EqualTo(SaveStatus.Ok));
            Assert.That(b1.xu, Is.EqualTo(1));
            store.Save(Data(3));
            SaveEnvelope.Parse(File.ReadAllBytes(Bak), out SaveData b2);
            Assert.That(b2.xu, Is.EqualTo(2));
        }

        [Test]
        public void SaveSameDataTwice_DoesNotRotateBak()
        {
            store.Save(Data(5));
            store.Save(Data(5));
            Assert.That(File.Exists(Bak), Is.False);
        }

        [Test]
        public void Save_LeavesNoTmp()
        {
            store.Save(Data(1));
            store.Save(Data(2));
            Assert.That(File.Exists(Tmp), Is.False);
        }

        [Test]
        public void Load_StaleGarbageTmp_IgnoredAndDeleted()
        {
            store.Save(Data(9));
            File.WriteAllText(Tmp, "rác");
            SaveLoadResult r = new SaveStore(dir).Load();
            Assert.That(r.status, Is.EqualTo(SaveStatus.Ok));
            Assert.That(r.data.xu, Is.EqualTo(9));
            Assert.That(File.Exists(Tmp), Is.False);
        }

        [Test]
        public void Load_OnlyJunkTmp_IsMissingNotCorrupt()
        {
            File.WriteAllText(Tmp, "rác");
            Assert.That(store.Load().status, Is.EqualTo(SaveStatus.Missing));
            Assert.That(Corrupts(), Is.Empty);
        }

        [Test]
        public void TruncatedMain_GoodBak_RestoredFromBackup_MainQuarantined()
        {
            store.Save(Data(1));
            store.Save(Data(2));
            byte[] main = File.ReadAllBytes(Main);
            File.WriteAllBytes(Main, main.Take(main.Length / 2).ToArray());
            SaveLoadResult r = new SaveStore(dir).Load();
            Assert.That(r.status, Is.EqualTo(SaveStatus.RestoredFromBackup));
            Assert.That(r.data.xu, Is.EqualTo(1));
            Assert.That(Corrupts().Length, Is.EqualTo(1));
            Assert.That(File.Exists(Main), Is.False);
        }

        [Test]
        public void MissingMain_GoodBak_Restored_NoQuarantine()
        {
            store.Save(Data(1));
            store.Save(Data(2));
            File.Delete(Main);
            SaveLoadResult r = new SaveStore(dir).Load();
            Assert.That(r.status, Is.EqualTo(SaveStatus.RestoredFromBackup));
            Assert.That(Corrupts(), Is.Empty);
        }

        [Test]
        public void CorruptBak_GoodMain_Ok()
        {
            store.Save(Data(1));
            store.Save(Data(2));
            File.WriteAllText(Bak, "hỏng");
            SaveLoadResult r = new SaveStore(dir).Load();
            Assert.That(r.status, Is.EqualTo(SaveStatus.Ok));
            Assert.That(r.data.xu, Is.EqualTo(2));
        }

        [Test]
        public void BothCorrupt_Corrupt_FilesQuarantinedByteIdentical_NextSaveWorks()
        {
            byte[] junkMain = Encoding.UTF8.GetBytes("main hỏng");
            byte[] junkBak = Encoding.UTF8.GetBytes("bak hỏng");
            File.WriteAllBytes(Main, junkMain);
            File.WriteAllBytes(Bak, junkBak);
            var s = new SaveStore(dir);
            Assert.That(s.Load().status, Is.EqualTo(SaveStatus.Corrupt));
            string[] q = Corrupts();
            Assert.That(q.Length, Is.EqualTo(2));
            Assert.That(q.Select(File.ReadAllBytes).Any(b => b.SequenceEqual(junkMain)), Is.True);
            Assert.That(q.Select(File.ReadAllBytes).Any(b => b.SequenceEqual(junkBak)), Is.True);
            Assert.That(s.Save(Data(4)), Is.True);
            Assert.That(new SaveStore(dir).Load().data.xu, Is.EqualTo(4));
        }

        [Test]
        public void FutureVersionMain_LocksAndNeverOverwrites()
        {
            byte[] future = Encoding.UTF8.GetBytes(SaveFixtures.Future());
            File.WriteAllBytes(Main, future);
            var s = new SaveStore(dir);
            Assert.That(s.Load().status, Is.EqualTo(SaveStatus.FutureVersion));
            Assert.That(s.Locked, Is.True);
            Assert.That(s.Save(Data(1)), Is.False);
            Assert.That(File.ReadAllBytes(Main).SequenceEqual(future), Is.True);
            Assert.That(File.Exists(Bak), Is.False);
            Assert.That(Corrupts(), Is.Empty);
        }

        [Test]
        public void FutureVersionMain_WithOlderGoodBak_StillFutureVersion()
        {
            store.Save(Data(1));
            store.Save(Data(2)); // bak = bản cũ hợp lệ
            File.WriteAllText(Main, SaveFixtures.Future());
            var s = new SaveStore(dir);
            Assert.That(s.Load().status, Is.EqualTo(SaveStatus.FutureVersion));
        }

        [Test]
        public void Save_WhenMainCorrupt_DoesNotRotateCorruptIntoBak()
        {
            store.Save(Data(1));
            store.Save(Data(2)); // bak = 1
            File.WriteAllText(Main, "hỏng giữa phiên");
            var same = new SaveStore(dir); // phiên mới, chưa Load
            Assert.That(same.Save(Data(3)), Is.True);
            SaveEnvelope.Parse(File.ReadAllBytes(Bak), out SaveData b);
            Assert.That(b.xu, Is.EqualTo(1), "bak tốt được giữ nguyên");
            Assert.That(Corrupts().Length, Is.EqualTo(1));
        }

        [Test]
        public void LegacyBareMain_ThenSave_WritesEnvelope_BakIsLegacyBare()
        {
            File.WriteAllText(Main, SaveFixtures.V3);
            var s = new SaveStore(dir);
            Assert.That(s.Load().status, Is.EqualTo(SaveStatus.Ok));
            Assert.That(s.Save(Data(10)), Is.True);
            Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(Main)), Does.StartWith(SaveEnvelope.Magic));
            Assert.That(File.ReadAllText(Bak), Is.EqualTo(SaveFixtures.V3));
        }

        [Test]
        public void Delete_RemovesEverything_AndUnlocks()
        {
            store.Save(Data(1));
            store.Save(Data(2));
            File.WriteAllText(Main + ".corrupt-20260101000000", "x");
            File.WriteAllText(Main, SaveFixtures.Future());
            var s = new SaveStore(dir);
            s.Load();
            Assert.That(s.Locked, Is.True);
            s.Delete();
            Assert.That(Directory.GetFiles(dir), Is.Empty);
            Assert.That(s.Locked, Is.False);
            Assert.That(s.Save(Data(1)), Is.True);
        }

        [Test]
        public void Quarantine_KeepsAtMostThree()
        {
            for (int i = 0; i < 6; i++)
            {
                File.WriteAllText(Main, "hỏng " + i);
                var s = new SaveStore(dir);
                Assert.That(s.Load().status, Is.EqualTo(SaveStatus.Corrupt));
            }
            Assert.That(Corrupts().Length, Is.EqualTo(3));
        }

        [Test]
        public void Save_Null_False_AndDirIsAFile_FalseWithoutThrowing()
        {
            Assert.That(store.Save(null), Is.False);
            string asFile = Path.Combine(dir, "iamafile");
            File.WriteAllText(asFile, "x");
            var broken = new SaveStore(asFile);
            Assert.That(broken.Save(Data(1)), Is.False);
        }
    }
}
