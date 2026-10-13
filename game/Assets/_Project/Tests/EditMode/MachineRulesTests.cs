using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class MachineRulesTests
    {
        private static float Factor(params string[] owned)
        {
            var set = new HashSet<string>(owned);
            return MachineRules.BrewTimeFactor(set.Contains);
        }

        [Test]
        public void NoMachines_NoSpeedup()
        {
            Assert.That(Factor(), Is.EqualTo(1f));
            Assert.That(MachineRules.BrewTimeFactor(null), Is.EqualTo(1f));
        }

        [Test]
        public void GrinderAndEspresso_StackMultiplicatively()
        {
            Assert.That(Factor(MachineIds.Grinder), Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(Factor(MachineIds.Espresso), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(Factor(MachineIds.Grinder, MachineIds.Espresso), Is.EqualTo(0.85f * 0.75f).Within(1e-5f));
        }

        [Test]
        public void ColdBrew_DoesNotChangeBrewTime_ItUnlocksARecipe()
        {
            Assert.That(Factor(MachineIds.ColdBrew), Is.EqualTo(1f));
        }

        [Test]
        public void PerfectTimeSlack_GrowsWithSpeedup_AndIsBounded()
        {
            Assert.That(MachineRules.PerfectTimeSlack(1f), Is.EqualTo(1f));
            Assert.That(MachineRules.PerfectTimeSlack(0.5f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(MachineRules.PerfectTimeSlack(0.1f), Is.EqualTo(2f).Within(1e-5f), "không nới quá gấp đôi");
            Assert.That(MachineRules.PerfectTimeSlack(2f), Is.EqualTo(1f), "không bao giờ làm cửa sổ 3★ hẹp lại");
        }

        [Test]
        public void BrewerSeconds_UsesMachineFactor()
        {
            float plain = BrewerRules.BrewSeconds(10f, 1);
            float fast = BrewerRules.BrewSeconds(10f, 1, Factor(MachineIds.Grinder, MachineIds.Espresso));
            Assert.That(fast, Is.LessThan(plain));
            Assert.That(fast, Is.EqualTo(plain * 0.85f * 0.75f).Within(1e-4f));
            Assert.That(BrewerRules.BrewSeconds(0.1f, 5, 0.1f), Is.GreaterThanOrEqualTo(1f), "luôn có thời gian pha tối thiểu");
        }

        [Test]
        public void DisplayNamesAndEffects_ExistForEveryMachine()
        {
            foreach (string id in MachineIds.All)
            {
                Assert.That(MachineRules.DisplayName(id), Is.Not.Empty, id);
                Assert.That(MachineRules.Effect(id), Is.Not.Empty, id);
            }
            Assert.That(MachineRules.DisplayName("khong_co"), Is.Empty);
        }
    }

    public class Tier3RecipeDataTests
    {
        [Test]
        public void Entries_AreValid()
        {
            var ids = new HashSet<string>();
            foreach (Tier3RecipeData.Entry e in Tier3RecipeData.All)
            {
                Assert.That(ids.Add(e.id), Is.True, "id trùng: " + e.id);
                Assert.That(e.steps, Is.Not.Null.And.Not.Empty, e.id);
                Assert.That(e.price, Is.GreaterThan(e.cost), e.id + ": giá phải lớn hơn vốn");
                Assert.That(e.seconds, Is.GreaterThan(0f), e.id);
                Assert.That(e.weight, Is.GreaterThan(0f), e.id);
                if (!string.IsNullOrEmpty(e.requiredMachine))
                    Assert.That(MachineIds.All, Does.Contain(e.requiredMachine), e.id + ": máy không tồn tại");
            }
        }

        [Test]
        public void Prices_MatchEconomyTable()
        {
            var byId = new Dictionary<string, Tier3RecipeData.Entry>();
            foreach (Tier3RecipeData.Entry e in Tier3RecipeData.All) byId[e.id] = e;
            Assert.That(byId["cold_brew_cam_sa"].price, Is.EqualTo(200));
            Assert.That(byId["bac_xiu_kem_muoi"].price, Is.EqualTo(230));
            Assert.That(byId["phin_robusta_cau_dat"].price, Is.EqualTo(300));
        }

        [Test]
        public void IdsDoNotCollideWithTier2Recipes()
        {
            var t2 = new HashSet<string>();
            foreach (Tier2RecipeData.Entry e in Tier2RecipeData.All) t2.Add(e.id);
            foreach (Tier3RecipeData.Entry e in Tier3RecipeData.All) Assert.That(t2, Does.Not.Contain(e.id), e.id);
        }

        [Test]
        public void StepSequences_DoNotShadowAHiddenTier2Recipe()
        {
            // Thử pha tự do khớp chuỗi bước để khám phá món ẩn: món cấp 3 không được trùng chuỗi của món ẩn cấp 2.
            foreach (Tier3RecipeData.Entry e3 in Tier3RecipeData.All)
            {
                foreach (Tier2RecipeData.Entry e2 in Tier2RecipeData.All)
                {
                    if (!e2.hidden || e2.steps.Length != e3.steps.Length) continue;
                    bool same = true;
                    for (int i = 0; i < e2.steps.Length; i++)
                        if (e2.steps[i] != e3.steps[i]) { same = false; break; }
                    Assert.That(same, Is.False, e3.id + " trùng chuỗi bước của " + e2.id);
                }
            }
        }

        [Test]
        public void Apply_SetsTierAndMachine()
        {
            var r = (RecipeDef)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RecipeDef));
            Tier3RecipeData.Entry cold = default;
            foreach (Tier3RecipeData.Entry e in Tier3RecipeData.All) if (e.id == "cold_brew_cam_sa") cold = e;
            Tier3RecipeData.Apply(r, cold);
            Assert.That(r.unlockTier, Is.EqualTo(3));
            Assert.That(r.hidden, Is.False);
            Assert.That(r.requiredMachine, Is.EqualTo(MachineIds.ColdBrew));
        }
    }
}
