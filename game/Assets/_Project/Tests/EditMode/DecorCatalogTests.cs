using System.Collections.Generic;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class DecorCatalogTests
    {
        [Test]
        public void Catalog_IdsUnique_PaletteCellsValid_AndEachSlotHasAFreeDefault()
        {
            var ids = new HashSet<string>();
            foreach (DecorSlot slot in new[] { DecorSlot.Wall, DecorSlot.Floor })
            {
                foreach (DecorOption o in DecorCatalog.For(slot))
                {
                    Assert.That(ids.Add(o.id), Is.True, "trùng id " + o.id);
                    Assert.That(o.slot, Is.EqualTo(slot), o.id);
                    Assert.That(o.priceXu > 0 && o.priceGems > 0, Is.False, o.id + ": chỉ một loại tiền");
                    Assert.That(o.minTier, Is.InRange(2, 3), o.id);
                    if (slot == DecorSlot.Wall)
                        foreach (int c in new[] { o.lower, o.cap, o.upper }) Assert.That(c, Is.InRange(0, 62), o.id + ": ô palette");
                    Assert.That(o.IsFree, Is.EqualTo(o.family == DecorFamily.None), o.id + ": kiểu mặc định miễn phí, kiểu trả tiền có họ phong cách");
                }
                Assert.That(DecorCatalog.Find(DecorCatalog.DefaultId(slot, 2)).IsFree, Is.True);
                Assert.That(DecorCatalog.Find(DecorCatalog.DefaultId(slot, 3)).IsFree, Is.True);
            }
        }

        [Test]
        public void Defaults_KeepTheOldLook()
        {
            var s = new DecorState();
            Assert.That(s.WallFor(2), Is.EqualTo("w_cream"));
            Assert.That(s.FloorFor(2), Is.EqualTo("f_encaustic"));
            Assert.That(s.FloorFor(3), Is.EqualTo("f_parquet"));
            DecorOption w = DecorCatalog.Find("w_cream");
            Assert.That(new[] { w.lower, w.cap, w.upper }, Is.EqualTo(new[] { TierConfig.CellWainscot, TierConfig.CellWoodDark, TierConfig.CellWallCream }));
            Assert.That(s.StyleBonus(2), Is.EqualTo(0));
            Assert.That(s.StyleBonus(3), Is.EqualTo(0));
        }

        [Test]
        public void CanBuy_ChecksOwnershipTierLevelAndMoney()
        {
            var s = new DecorState();
            Assert.That(s.CanBuy("w_cream", 2, 1, 0, 0), Is.EqualTo(DecorCheck.Owned));
            Assert.That(s.CanBuy("nope", 2, 99, 99999, 999), Is.EqualTo(DecorCheck.Unknown));
            Assert.That(s.CanBuy("w_jade", 2, 30, 99999, 0), Is.EqualTo(DecorCheck.NeedTier));
            Assert.That(s.CanBuy("f_parquet", 2, 30, 0, 0), Is.EqualTo(DecorCheck.NeedTier), "gỗ ghép miễn phí nhưng chỉ ở nhà hàng");
            Assert.That(s.CanBuy("f_parquet", 3, 1, 0, 0), Is.EqualTo(DecorCheck.Owned));
            Assert.That(s.CanBuy("w_hoian", 2, 8, 99999, 0), Is.EqualTo(DecorCheck.NeedLevel));
            Assert.That(s.CanBuy("w_hoian", 2, 9, 2499, 0), Is.EqualTo(DecorCheck.NeedXu));
            Assert.That(s.CanBuy("w_hoian", 2, 9, 2500, 0), Is.EqualTo(DecorCheck.Ok));
            Assert.That(s.CanBuy("f_hue", 2, 1, 0, 199), Is.EqualTo(DecorCheck.NeedGems));
            Assert.That(s.CanBuy("f_hue", 2, 1, 0, 200), Is.EqualTo(DecorCheck.Ok));
        }

        [Test]
        public void Apply_NeedsOwnership_AndChoiceIsPerTier()
        {
            var s = new DecorState();
            Assert.That(s.Apply("w_mint", 2), Is.False, "chưa mua");
            s.MarkOwned("w_mint");
            Assert.That(s.Apply("w_mint", 2), Is.True);
            Assert.That(s.WallFor(2), Is.EqualTo("w_mint"));
            Assert.That(s.WallFor(3), Is.EqualTo("w_cream"), "lên nhà hàng: phòng mới dùng mặc định");
            Assert.That(s.Apply("f_parquet", 2), Is.False, "gỗ ghép chỉ từ cấp 3");
            Assert.That(s.Apply("w_mint", 3), Is.True, "đã mua thì dùng được ở mọi cấp");
        }

        [Test]
        public void StyleBonus_PerSlot_PlusMatchingFamily()
        {
            var s = new DecorState();
            s.MarkOwned("w_mint");
            s.MarkOwned("f_terracotta");
            s.MarkOwned("f_terrazzo");
            s.Apply("w_mint", 2);
            Assert.That(s.StyleBonus(2), Is.EqualTo(DecorCatalog.SlotBonusT2));
            s.Apply("f_terrazzo", 2); // hiện đại, khác họ
            Assert.That(s.StyleBonus(2), Is.EqualTo(2 * DecorCatalog.SlotBonusT2));
            s.Apply("f_terracotta", 2); // hoài cổ = cùng họ với bạc hà
            Assert.That(s.StyleBonus(2), Is.EqualTo(2 * DecorCatalog.SlotBonusT2 + DecorCatalog.MatchBonusT2));
            s.Apply("w_mint", 3);
            s.Apply("f_terracotta", 3);
            Assert.That(s.StyleBonus(3), Is.EqualTo(2 * DecorCatalog.SlotBonusT3 + DecorCatalog.MatchBonusT3));
            Assert.That(DecorCatalog.StyleBonus(1, "w_mint", "f_terracotta"), Is.EqualTo(0), "cấp 1 không có phòng");
        }

        [Test]
        public void SaveRoundTrip_KeepsOwnedAndChoices_DropsUnownedChoices()
        {
            var s = new DecorState();
            s.MarkOwned("w_hoian");
            s.MarkOwned("f_hue");
            s.Apply("w_hoian", 2);
            s.Apply("f_hue", 3);
            var data = new SaveData();
            s.WriteToSave(data);

            var t = new DecorState();
            t.Load(data);
            Assert.That(t.WallFor(2), Is.EqualTo("w_hoian"));
            Assert.That(t.FloorFor(2), Is.EqualTo("f_encaustic"));
            Assert.That(t.FloorFor(3), Is.EqualTo("f_hue"));
            Assert.That(t.Owns("w_hoian") && t.Owns("f_hue"), Is.True);

            // file bị sửa tay: chọn món chưa mua / id lạ → mặc định
            data.decorOwned = new[] { "zzz" };
            data.decorChoices = new[] { new DecorChoiceSave { tier = 2, wall = "w_brick", floor = "" } };
            t.Load(data);
            Assert.That(t.WallFor(2), Is.EqualTo("w_cream"));
            Assert.That(t.Owns("w_brick"), Is.False);

            t.Load(new SaveData()); // save cũ không có trường trang trí
            Assert.That(t.WallFor(2), Is.EqualTo("w_cream"));
        }
    }
}
