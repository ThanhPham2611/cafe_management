using System.Collections.Generic;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class ShopUnlocksTests
    {
        [Test]
        public void UnlistedItems_AreOpenFromTheStart()
        {
            Assert.That(ShopUnlocks.RequiredLevel("Stool_Plastic_Red"), Is.EqualTo(1));
            Assert.That(ShopUnlocks.RequiredLevel("Machine_Espresso"), Is.EqualTo(1), "máy pha không bị khóa");
            Assert.That(ShopUnlocks.RequiredLevel(null), Is.EqualTo(1));
            Assert.That(ShopUnlocks.IsUnlocked("Plant_Pot_Small", 1), Is.True);
        }

        [Test]
        public void Gating_FollowsLevel()
        {
            Assert.That(ShopUnlocks.RequiredLevel("Lantern_Hoian"), Is.EqualTo(9));
            Assert.That(ShopUnlocks.IsUnlocked("Lantern_Hoian", 8), Is.False);
            Assert.That(ShopUnlocks.IsUnlocked("Lantern_Hoian", 9), Is.True);
            Assert.That(ShopUnlocks.IsUnlocked("Lantern_Hoian", 30), Is.True);
        }

        [Test]
        public void EveryGate_IsReachable_AndNothingNeededOnEntryIsLockedOnEntry()
        {
            foreach (KeyValuePair<string, int> kv in ShopUnlocks.All)
            {
                Assert.That(kv.Value, Is.InRange(2, LevelRules.MaxLevel), kv.Key);
                // mỗi món phải mở được bằng đúng RP của level đó (không đòi level cao hơn mức tối đa)
                Assert.That(LevelRules.RpFor(kv.Value), Is.LessThanOrEqualTo(LevelRules.RpFor(LevelRules.MaxLevel)));
            }
            // vào Mặt bằng ở Lv6 và Nhà hàng ở Lv16: các món rẻ nhất mỗi cấp mở sẵn lúc vào (không bị khóa ngay khi vừa mua cấp)
            Assert.That(ShopUnlocks.IsUnlocked("Lamp_Pendant", 6), Is.True);
            Assert.That(ShopUnlocks.IsUnlocked("Plant_Pothos_Hanging", 6), Is.True);
            Assert.That(ShopUnlocks.IsUnlocked("Plant_Fig_Large", 16), Is.True);
            Assert.That(ShopUnlocks.IsUnlocked("Chair_Rattan", 6), Is.True);
        }

        [Test]
        public void Unlocks_AreSpreadOutOverTheLongTierThreeStretch()
        {
            // cấp 3 kéo dài từ Lv16: các mốc mở khóa rải ở ít nhất 5 level khác nhau để luôn có thứ để hướng tới
            var levels = new HashSet<int>();
            foreach (KeyValuePair<string, int> kv in ShopUnlocks.All)
                if (kv.Value >= 16) levels.Add(kv.Value);
            Assert.That(levels.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void UnlockedAt_ListsTheItemsOpeningExactlyAtThatLevel()
        {
            Assert.That(ShopUnlocks.UnlockedAt(9), Is.EqualTo(new[] { "Lantern_Hoian" }));
            Assert.That(ShopUnlocks.UnlockedAt(20), Is.EqualTo(new[] { "Aircon_Standing", "Armchair_Velvet" }));
            Assert.That(ShopUnlocks.UnlockedAt(1), Is.Empty);
        }

        [Test]
        public void CounterLamps_UnlockAtTheirLevels()
        {
            Assert.That(ShopUnlocks.RequiredLevel("Lamp_Bulb"), Is.EqualTo(7));
            Assert.That(ShopUnlocks.RequiredLevel("Lamp_Desk"), Is.EqualTo(13));
        }

        [Test]
        public void Titles_ChangeAtTheTitleLevels_AndEveryLevelHasOne()
        {
            Assert.That(LevelTitles.TitleFor(1), Is.EqualTo("Chủ quán tập sự"));
            Assert.That(LevelTitles.TitleFor(2), Is.EqualTo("Chủ quán tập sự"));
            Assert.That(LevelTitles.TitleFor(3), Is.EqualTo("Quán của xóm"));
            Assert.That(LevelTitles.TitleFor(41), Is.EqualTo("Huyền thoại cà phê Việt"));
            Assert.That(LevelTitles.TitleFor(999), Is.EqualTo("Huyền thoại cà phê Việt"));
            Assert.That(LevelTitles.IsTitleLevel(1), Is.False);
            Assert.That(LevelTitles.IsTitleLevel(6), Is.True, "★2 là mốc danh hiệu");
            Assert.That(LevelTitles.IsTitleLevel(16), Is.True, "★3 là mốc danh hiệu");
            Assert.That(LevelTitles.IsTitleLevel(7), Is.False);
            for (int lv = 1; lv <= LevelRules.MaxLevel; lv++) Assert.That(LevelTitles.TitleFor(lv), Is.Not.Empty);
        }
    }
}
