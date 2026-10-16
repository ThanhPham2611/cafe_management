using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    /// Kiểm dữ liệu truyện khách quen (regulars.json, barks.json) theo quy ước docs/story/README.md, để thêm chương / nhân vật
    /// không làm hỏng game âm thầm (thiếu món ruột, chương lệch thứ tự, thoại tràn khung...).
    public class StoryDataTests
    {
        private static string StoryPath(string file) { return Path.Combine(Application.dataPath, "_Project/Data/Story", file); }
        private static string DataPath(string folder) { return Path.Combine(Application.dataPath, "_Project/Data", folder); }

        private static RegularDefinition[] Load()
        {
            return JsonUtility.FromJson<RegularDataRoot>(File.ReadAllText(StoryPath("regulars.json"))).regulars;
        }

        [Test]
        public void SixRegulars_EachWithFourChapters_UniqueIds()
        {
            RegularDefinition[] all = Load();
            Assert.That(all.Length, Is.EqualTo(6));
            var ids = new HashSet<string>();
            var chapterIds = new HashSet<string>();
            foreach (RegularDefinition r in all)
            {
                Assert.That(ids.Add(r.id), Is.True, "trùng id nhân vật " + r.id);
                Assert.That(r.chapters.Length, Is.EqualTo(RegularsManager.ShippedChaptersPerRegular), r.id);
                foreach (StoryChapterDefinition c in r.chapters)
                    Assert.That(chapterIds.Add(c.id), Is.True, "trùng id chương " + c.id);
            }
        }

        [Test]
        public void EveryRegularIsListedInTheManager_AndTheOtherWayAround()
        {
            // InitialIds là danh sách nhân vật bật trong game: mỗi người trong JSON phải được bật, nếu không chương viết xong mà không ai thấy
            var field = typeof(RegularsManager).GetField("InitialIds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var enabled = new HashSet<string>((string[])field.GetValue(null));
            var inJson = new HashSet<string>();
            foreach (RegularDefinition r in Load()) inJson.Add(r.id);
            Assert.That(inJson, Is.EquivalentTo(enabled));
        }

        [Test]
        public void Chapters_AreOrderedByBond_WithinRange_AndEndWithAReward()
        {
            foreach (RegularDefinition r in Load())
            {
                int last = 0;
                foreach (StoryChapterDefinition c in r.chapters)
                {
                    Assert.That(c.requiresBond, Is.InRange(1, 10), c.id);
                    Assert.That(c.requiresBond, Is.GreaterThan(last), c.id + ": thân thiết phải tăng dần");
                    last = c.requiresBond;
                    Assert.That(c.reward, Is.Not.Null, c.id);
                    Assert.That(c.reward.amount, Is.GreaterThan(0), c.id);
                    Assert.That(c.title, Is.Not.Empty, c.id);
                }
            }
        }

        [Test]
        public void Lines_FitTheDialog_AndSpeakersAreValid()
        {
            foreach (RegularDefinition r in Load())
            {
                foreach (StoryChapterDefinition c in r.chapters)
                {
                    Assert.That(c.lines.Length, Is.InRange(3, 6), c.id + ": 3-6 dòng mỗi đoạn");
                    foreach (StoryLineDefinition l in c.lines)
                    {
                        Assert.That(l.text, Is.Not.Empty, c.id);
                        Assert.That(l.text.Length, Is.LessThanOrEqualTo(70), c.id + ": \"" + l.text + "\" quá 70 ký tự");
                        Assert.That(l.speaker == r.id || l.speaker == "owner" || l.speaker == "narrator", Is.True, c.id + ": speaker lạ '" + l.speaker + "'");
                    }
                }
            }
        }

        [Test]
        public void FavoritesAndRewards_PointAtRealAssets()
        {
            string[] allowedTypes = { "xu", "furniture", "recipe", "hat" };
            foreach (RegularDefinition r in Load())
            {
                Assert.That(File.Exists(Path.Combine(DataPath("Recipes"), "SO_Recipe_" + r.favoriteRecipe + ".asset")), Is.True, r.id + ": món ruột " + r.favoriteRecipe + " không có");
                Assert.That(r.unlockTier, Is.InRange(1, 3), r.id);
                Assert.That(r.visitChancePerDay, Is.InRange(0.05f, 1f), r.id);
                foreach (StoryChapterDefinition c in r.chapters)
                {
                    StoryRewardDefinition rw = c.reward;
                    Assert.That(allowedTypes, Does.Contain(rw.type), c.id);
                    if (rw.type == "furniture")
                        Assert.That(File.Exists(Path.Combine(DataPath("Furniture"), "SO_Furniture_" + rw.id + ".asset")), Is.True, c.id + ": đồ " + rw.id);
                    if (rw.type == "recipe")
                        Assert.That(File.Exists(Path.Combine(DataPath("Recipes"), "SO_Recipe_" + rw.id + ".asset")), Is.True, c.id + ": công thức " + rw.id);
                }
            }
        }

        [Test]
        public void DocsAndDataCopiesAreIdentical()
        {
            string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/story/regulars.json"));
            Assert.That(File.Exists(docs), Is.True);
            Assert.That(File.ReadAllText(docs), Is.EqualTo(File.ReadAllText(StoryPath("regulars.json"))), "docs/story và Data/Story phải giống hệt (builder copy docs -> Data)");
        }

        [Test]
        public void Barks_AllGroupsPresent_AndShort()
        {
            string json = File.ReadAllText(StoryPath("barks.json"));
            BarkData data = JsonUtility.FromJson<BarkData>(json);
            Assert.That(data, Is.Not.Null);
            foreach (string group in BarkBook.Groups)
            {
                string[] lines = BarkBook.LinesOf(data, group);
                Assert.That(lines.Length, Is.GreaterThanOrEqualTo(5), group);
                foreach (string l in lines) Assert.That(l.Length, Is.InRange(1, 40), group + ": " + l);
            }
        }
    }
}
