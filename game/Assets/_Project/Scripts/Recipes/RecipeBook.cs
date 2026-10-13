using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Hằng số cân bằng của hệ công thức (economy §2.1-2.3, §3.4; phase2_spec §3).
    /// TODO(Kinh tế): chuyển sang GameBalance/Remote Config khi cân lại sau mô phỏng.
    public static class RecipeBalance
    {
        /// Thử pha tự do: phẳng 20 Xu mỗi nguyên liệu.
        public const int ExperimentIngredientXu = 20;
        public const int MaxExperimentSteps = 6;
        /// "Món lỗi" bán được 40% giá trung bình các món đã biết.
        public const float FailedDrinkFraction = 0.4f;
        /// Món ẩn: 3 sao cần tổng thời gian <= brewSeconds x 1,2 và dụng cụ cấp >= 2.
        public const float PerfectTimeFactorHidden = 1.2f;
        public const int PerfectToolLevel = 2;
        public const int MaxToolLevel = 4;
        /// Chi phí nâng dụng cụ: cấp 1->2, 2->3, 3->4 (phase2_spec §3: 1.500/2.500/4.000 Xu).
        public static readonly int[] ToolUpgradeCost = { 1500, 2500, 4000 };
        public const int MenuLimitT2 = 6;
        /// Khóa trong SaveData.toolLevels để nhớ gợi ý đã mua (SaveData v3 chưa có trường riêng).
        public const string HintKeyPrefix = "hint:";

        /// Tier 1: mọi món (không giới hạn). Tier 2: 6 món. Tier 3+: +2 mỗi cấp (tạm).
        public static int MenuLimit(int tier)
        {
            return tier <= 1 ? int.MaxValue : MenuLimitT2 + 2 * (tier - 2);
        }
    }

    /// Dịch vụ công thức: món đã biết, menu hôm nay, khám phá món ẩn, gợi ý (Hạt), cấp dụng cụ, chọn món cho khách.
    /// Truy cập qua GameBootstrap.Recipes. Lưu/đọc qua SaveData.knownRecipes / menuToday / toolLevels.
    public class RecipeBook
    {
        /// TIER gán hàm trả về cấp quán hiện tại (mặc định 1). Gán lại mỗi lần vào game (Reset khi nạp domain).
        public static Func<int> TierProvider = DefaultTier;

        /// Chỉ cho autopilot (-autorecipes): coi như đã Tier >= 2 để món ẩn được gọi/menu hoạt động ở Tier 1.
        public static bool DebugIgnoreTier;

        private static int DefaultTier()
        {
            return 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TierProvider = DefaultTier;
            DebugIgnoreTier = false;
        }

        public static int CurrentTier
        {
            get
            {
                int t = TierProvider != null ? TierProvider() : 1;
                if (t < 1) t = 1;
                if (DebugIgnoreTier && t < 2) t = 2;
                return t;
            }
        }

        private readonly GameBootstrap game;
        private readonly List<RecipeDef> ordered = new List<RecipeDef>();
        private readonly Dictionary<string, RecipeDef> byId = new Dictionary<string, RecipeDef>();
        private readonly HashSet<string> known = new HashSet<string>();
        private readonly List<string> menu = new List<string>();
        private readonly HashSet<string> hinted = new HashSet<string>();
        private readonly Dictionary<string, int> toolLevels = new Dictionary<string, int>();
        private readonly List<string> toolIds = new List<string>();
        private readonly List<RecipeDef> newlyKnown = new List<RecipeDef>(4);

        /// Bất kỳ thay đổi nào (biết thêm món, menu, gợi ý, dụng cụ, đổi cấp).
        public event Action Changed;
        /// Vừa khám phá một món (qua thử nghiệm / Discover).
        public event Action<RecipeDef> Discovered;

        public IReadOnlyList<RecipeDef> Ordered { get { return ordered; } }
        public IReadOnlyList<string> ToolIds { get { return toolIds; } }
        public int KnownCount { get { return known.Count; } }
        public int MenuCount { get { return menu.Count; } }

        public RecipeBook(GameBootstrap game)
        {
            this.game = game;
            if (game.recipes != null)
            {
                for (int i = 0; i < game.recipes.Length; i++) Index(game.recipes[i]);
            }

            // Scene dựng trước khi có asset Tier 2 (chưa chạy Cafe/Data/Ensure Tier-2 Recipes + gán vào scene):
            // tự tạo bản trong bộ nhớ từ cùng bảng dữ liệu để tính năng vẫn chạy được.
            List<RecipeDef> synthesized = null;
            for (int i = 0; i < Tier2RecipeData.All.Length; i++)
            {
                Tier2RecipeData.Entry e = Tier2RecipeData.All[i];
                if (byId.ContainsKey(e.id)) continue;
                RecipeDef r = ScriptableObject.CreateInstance<RecipeDef>();
                r.hideFlags = HideFlags.HideAndDontSave;
                Tier2RecipeData.Apply(r, e);
                Index(r);
                if (synthesized == null) synthesized = new List<RecipeDef>(4);
                synthesized.Add(r);
            }
            for (int i = 0; i < Tier3RecipeData.All.Length; i++)
            {
                Tier3RecipeData.Entry e = Tier3RecipeData.All[i];
                if (byId.ContainsKey(e.id)) continue;
                RecipeDef r = ScriptableObject.CreateInstance<RecipeDef>();
                r.hideFlags = HideFlags.HideAndDontSave;
                Tier3RecipeData.Apply(r, e);
                Index(r);
                if (synthesized == null) synthesized = new List<RecipeDef>(4);
                synthesized.Add(r);
            }
            if (synthesized != null)
            {
                Debug.LogWarning("[RecipeBook] GameBootstrap.recipes thiếu " + synthesized.Count +
                                 " công thức Tier 2/3: dùng bản tạm trong bộ nhớ. Chạy menu Cafe/Data/Ensure Tier-2 Recipes rồi lưu scene.");
                int old = game.recipes != null ? game.recipes.Length : 0;
                var merged = new RecipeDef[old + synthesized.Count];
                if (old > 0) System.Array.Copy(game.recipes, merged, old);
                for (int i = 0; i < synthesized.Count; i++) merged[old + i] = synthesized[i];
                game.recipes = merged;
            }

            // cấp -> ẩn/hiện -> giá: Tier 1 trước, món thường trước món ẩn
            ordered.Sort((a, b) =>
            {
                int c = a.unlockTier.CompareTo(b.unlockTier);
                if (c != 0) return c;
                c = a.hidden.CompareTo(b.hidden);
                if (c != 0) return c;
                return a.price.CompareTo(b.price);
            });
            GameEvents.DayStarted += OnDayStarted;
        }

        public void Dispose()
        {
            GameEvents.DayStarted -= OnDayStarted;
        }

        private void Index(RecipeDef r)
        {
            if (r == null || string.IsNullOrEmpty(r.id) || byId.ContainsKey(r.id)) return;
            byId[r.id] = r;
            ordered.Add(r);
            if (r.requiredTools == null) return;
            for (int k = 0; k < r.requiredTools.Length; k++)
            {
                string tid = r.requiredTools[k];
                if (!string.IsNullOrEmpty(tid) && !toolIds.Contains(tid)) toolIds.Add(tid);
            }
        }

        // ------------------------------------------------------------------ tra cứu

        public RecipeDef Get(string id)
        {
            RecipeDef r;
            return (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out r)) ? r : null;
        }

        public bool IsKnown(string id)
        {
            return !string.IsNullOrEmpty(id) && known.Contains(id);
        }

        public bool IsKnown(RecipeDef r)
        {
            return r != null && known.Contains(r.id);
        }

        /// Có hiện trong Sổ công thức không: đã biết, hoặc món ẩn đã tới cấp mở.
        public bool IsListed(RecipeDef r)
        {
            if (r == null) return false;
            if (known.Contains(r.id)) return true;
            return r.hidden && r.unlockTier <= CurrentTier;
        }

        public bool HasHint(string id)
        {
            return !string.IsNullOrEmpty(id) && hinted.Contains(id);
        }

        /// Món có trong menu hôm nay. Tier 1: mọi món đã biết đều bán.
        public bool InMenu(RecipeDef r)
        {
            if (r == null || !known.Contains(r.id)) return false;
            return CurrentTier <= 1 || menu.Contains(r.id);
        }

        public int MenuLimit { get { return RecipeBalance.MenuLimit(CurrentTier); } }

        /// Thử pha tự do mở từ Tier 2.
        public bool CanExperiment { get { return CurrentTier >= 2; } }

        /// Giá trung bình các món đã biết (cho "món lỗi" = 40%).
        public int AverageKnownPrice()
        {
            int tier = CurrentTier;
            int sum = 0, n = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                RecipeDef r = ordered[i];
                if (!known.Contains(r.id) || r.unlockTier > tier) continue;
                sum += r.price;
                n++;
            }
            return n > 0 ? Mathf.RoundToInt(sum / (float)n) : 80;
        }

        // ------------------------------------------------------------------ nạp / lưu

        /// save = null: chơi mới. Gọi một lần trong GameBootstrap.Build (sau khi TierProvider đã được gán).
        public void Load(SaveData save)
        {
            known.Clear();
            menu.Clear();
            hinted.Clear();
            toolLevels.Clear();

            if (save != null)
            {
                if (save.knownRecipes != null)
                {
                    for (int i = 0; i < save.knownRecipes.Length; i++)
                        if (byId.ContainsKey(save.knownRecipes[i] ?? "")) known.Add(save.knownRecipes[i]);
                }
                if (save.menuToday != null)
                {
                    for (int i = 0; i < save.menuToday.Length; i++)
                    {
                        string id = save.menuToday[i];
                        if (!string.IsNullOrEmpty(id) && known.Contains(id) && !menu.Contains(id)) menu.Add(id);
                    }
                }
                if (save.toolLevels != null)
                {
                    for (int i = 0; i < save.toolLevels.Length; i++)
                    {
                        PurchaseCount pc = save.toolLevels[i];
                        if (string.IsNullOrEmpty(pc.furnitureId)) continue;
                        if (pc.furnitureId.StartsWith(RecipeBalance.HintKeyPrefix, StringComparison.Ordinal))
                            hinted.Add(pc.furnitureId.Substring(RecipeBalance.HintKeyPrefix.Length));
                        else
                            toolLevels[pc.furnitureId] = Mathf.Clamp(pc.count, 1, RecipeBalance.MaxToolLevel);
                    }
                }
            }

            // Chơi mới / save cũ: mọi món thường của Tier 1 đã biết; menu = tất cả món đã biết
            SyncTierRecipes(false);
            if (menu.Count == 0)
            {
                for (int i = 0; i < ordered.Count; i++)
                    if (known.Contains(ordered[i].id) && menu.Count < MenuLimit) menu.Add(ordered[i].id);
            }
            RaiseChanged();
        }

        /// Ghi vào SaveData v3: knownRecipes, menuToday, toolLevels (+ gợi ý đã mua với khóa "hint:<id>").
        public void Save(SaveData d)
        {
            if (d == null) return;
            var ids = new List<string>(known.Count);
            for (int i = 0; i < ordered.Count; i++)
                if (known.Contains(ordered[i].id)) ids.Add(ordered[i].id);
            d.knownRecipes = ids.ToArray();
            d.menuToday = menu.ToArray();

            var tools = new List<PurchaseCount>(toolLevels.Count + hinted.Count);
            foreach (KeyValuePair<string, int> kv in toolLevels)
            {
                if (kv.Value <= 1) continue;
                tools.Add(new PurchaseCount { furnitureId = kv.Key, count = kv.Value });
            }
            foreach (string h in hinted)
                tools.Add(new PurchaseCount { furnitureId = RecipeBalance.HintKeyPrefix + h, count = 1 });
            d.toolLevels = tools.ToArray();
        }

        // ------------------------------------------------------------------ cấp quán

        /// TIER gọi khi lên cấp: mở món thường của cấp mới, báo toast.
        public void NotifyTierChanged()
        {
            SyncTierRecipes(true);
            RaiseChanged();
        }

        private void OnDayStarted(int day)
        {
            SyncTierRecipes(true);
        }

        /// Món thường (không ẩn) có unlockTier <= cấp hiện tại tự động được biết. Tier 1: menu = mọi món đã biết.
        private void SyncTierRecipes(bool announce)
        {
            int tier = CurrentTier;
            newlyKnown.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                RecipeDef r = ordered[i];
                if (r.hidden || r.unlockTier > tier) continue;
                if (known.Add(r.id)) newlyKnown.Add(r);
            }

            if (tier <= 1)
            {
                RebuildMenuAllKnown();
            }
            else
            {
                // Loại id không còn biết; thêm món mới nếu còn chỗ
                for (int i = menu.Count - 1; i >= 0; i--)
                    if (!known.Contains(menu[i])) menu.RemoveAt(i);
                for (int i = 0; i < newlyKnown.Count; i++) TryAddToMenu(newlyKnown[i], false);
            }

            if (announce)
            {
                for (int i = 0; i < newlyKnown.Count; i++)
                    GameEvents.RaiseToast(string.Format(Strings.NewRecipeFmt, Strings.FullName(newlyKnown[i])));
            }
            if (newlyKnown.Count > 0) RaiseChanged();
        }

        // ------------------------------------------------------------------ menu hôm nay

        private void RebuildMenuAllKnown()
        {
            menu.Clear();
            for (int i = 0; i < ordered.Count; i++)
                if (known.Contains(ordered[i].id)) menu.Add(ordered[i].id);
        }

        private bool TryAddToMenu(RecipeDef r, bool toastWhenFull)
        {
            if (menu.Contains(r.id)) return true;
            int limit = MenuLimit;
            if (menu.Count >= limit)
            {
                if (toastWhenFull) GameEvents.RaiseToast(string.Format(RecipeStrings.MenuFullFmt, limit));
                return false;
            }
            menu.Add(r.id);
            return true;
        }

        /// Bật/tắt món trong menu. Tier 1 không đổi được (bán mọi món). Trả true nếu trạng thái sau cùng = on.
        public bool SetOnMenu(string id, bool on)
        {
            RecipeDef r = Get(id);
            if (r == null || !known.Contains(id)) return false;
            int tier = CurrentTier;
            if (tier <= 1)
            {
                GameEvents.RaiseToast(RecipeStrings.MenuLockedT1);
                return true;
            }
            bool has = menu.Contains(id);
            if (on == has) return has;
            if (on)
            {
                if (!TryAddToMenu(r, true)) return false;
            }
            else
            {
                if (menu.Count <= 1)
                {
                    GameEvents.RaiseToast(RecipeStrings.MenuNeedOne);
                    return true;
                }
                menu.Remove(id);
            }
            RaiseChanged();
            game.SaveSnapshot();
            return on;
        }

        // ------------------------------------------------------------------ chọn món cho khách

        /// Món khách có thể gọi: đã biết, trong menu, đúng cấp, đã tới ngày mở. Không có thì rơi về mọi món đã biết.
        public RecipeDef PickRecipe(int day)
        {
            int tier = CurrentTier;
            RecipeDef pick = Roll(day, tier, true);
            return pick != null ? pick : Roll(day, tier, false);
        }

        private bool Orderable(RecipeDef r, int day, int tier, bool requireMenu)
        {
            if (r == null || !known.Contains(r.id)) return false;
            if (r.unlockTier > tier || day < r.unlockDay) return false;
            if (!MachineReady(r)) return false;
            return !requireMenu || tier <= 1 || menu.Contains(r.id);
        }

        /// Món cần máy thì máy phải đang đặt trong quán.
        public bool MachineReady(RecipeDef r)
        {
            return r == null || string.IsNullOrEmpty(r.requiredMachine) || (game.Grid != null && game.Grid.HasMachine(r.requiredMachine));
        }

        private RecipeDef Roll(int day, int tier, bool requireMenu)
        {
            RecipeDef[] all = game.recipes;
            if (all == null) return null;
            float total = 0f;
            for (int i = 0; i < all.Length; i++)
                if (Orderable(all[i], day, tier, requireMenu)) total += Mathf.Max(0.01f, all[i].orderWeight);
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            RecipeDef last = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (!Orderable(all[i], day, tier, requireMenu)) continue;
                last = all[i];
                roll -= Mathf.Max(0.01f, last.orderWeight);
                if (roll <= 0f) return last;
            }
            return last;
        }

        // ------------------------------------------------------------------ khám phá

        /// Mở khóa món (thử nghiệm đúng / debug). Thêm vào menu nếu còn chỗ, hiện hiệu ứng ăn mừng. Trả true nếu mới.
        public bool Discover(string id)
        {
            RecipeDef r = Get(id);
            if (r == null || known.Contains(id)) return false;
            known.Add(id);
            bool onMenu = true;
            if (CurrentTier <= 1) RebuildMenuAllKnown();
            else onMenu = TryAddToMenu(r, false);

            string name = Strings.FullName(r);
            UiFx.FloatTextScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f),
                string.Format(RecipeStrings.DiscoveredFmt, name), HudBuilder.Accent);
            GameEvents.RaiseToast(onMenu
                ? string.Format(RecipeStrings.AddedToMenuFmt, name)
                : string.Format(RecipeStrings.MenuFullFmt, MenuLimit));

            RaiseChanged();
            if (Discovered != null) Discovered(r);
            game.SaveSnapshot();
            return true;
        }

        /// Chuỗi bước khớp chính xác một món. hiddenOnly: chỉ xét món ẩn đã tới cấp mở.
        public RecipeDef FindMatch(IList<BrewStep> seq, bool hiddenOnly)
        {
            if (seq == null || seq.Count == 0) return null;
            int tier = CurrentTier;
            for (int i = 0; i < ordered.Count; i++)
            {
                RecipeDef r = ordered[i];
                if (hiddenOnly && (!r.hidden || r.unlockTier > tier)) continue;
                if (r.steps == null || r.steps.Length != seq.Count) continue;
                bool same = true;
                for (int k = 0; k < seq.Count; k++)
                {
                    if (r.steps[k] != seq[k]) { same = false; break; }
                }
                if (same) return r;
            }
            return null;
        }

        /// Còn món ẩn chưa biết nào dài hơn mà bắt đầu bằng chuỗi này? (để thử nghiệm không tự chốt quá sớm)
        public bool HasLongerHiddenWithPrefix(IList<BrewStep> seq)
        {
            int tier = CurrentTier;
            for (int i = 0; i < ordered.Count; i++)
            {
                RecipeDef r = ordered[i];
                if (!r.hidden || r.unlockTier > tier || known.Contains(r.id)) continue;
                if (r.steps == null || r.steps.Length <= seq.Count) continue;
                bool prefix = true;
                for (int k = 0; k < seq.Count; k++)
                {
                    if (r.steps[k] != seq[k]) { prefix = false; break; }
                }
                if (prefix) return true;
            }
            return false;
        }

        /// Khớp đúng một món ẩn chưa biết -> khám phá. Trả true nếu vừa khám phá.
        public bool TryDiscoverFromSequence(BrewStep[] sequence)
        {
            RecipeDef r = FindMatch(sequence, true);
            if (r == null || known.Contains(r.id)) return false;
            return Discover(r.id);
        }

        // ------------------------------------------------------------------ gợi ý (Hạt)

        /// Mua gợi ý "Mẹo bà nội": hiện ra thứ tự các bước của món ẩn (vẫn phải tự pha thử để mở khóa).
        public bool BuyHint(string id)
        {
            RecipeDef r = Get(id);
            if (r == null || !r.hidden || known.Contains(id) || hinted.Contains(id)) return false;
            int cost = Mathf.Max(0, r.hintCostGems);
            if (game.Gems == null || !game.Gems.TrySpend(cost))
            {
                int missing = cost - (game.Gems != null ? game.Gems.Gems : 0);
                GameEvents.RaiseToast(string.Format(RecipeStrings.NeedGemsFmt, Mathf.Max(1, missing)));
                return false;
            }
            hinted.Add(id);
            GameEvents.RaiseToast(RecipeStrings.HintBought);
            RaiseChanged();
            game.SaveSnapshot();
            return true;
        }

        // ------------------------------------------------------------------ dụng cụ & chất lượng

        public int GetToolLevel(string toolId)
        {
            int v;
            return (!string.IsNullOrEmpty(toolId) && toolLevels.TryGetValue(toolId, out v)) ? Mathf.Max(1, v) : 1;
        }

        /// Giá nâng lên cấp kế; -1 nếu đã tối đa.
        public int ToolUpgradeCost(string toolId)
        {
            int lv = GetToolLevel(toolId);
            if (lv >= RecipeBalance.MaxToolLevel) return -1;
            return RecipeBalance.ToolUpgradeCost[Mathf.Clamp(lv - 1, 0, RecipeBalance.ToolUpgradeCost.Length - 1)];
        }

        public bool TryUpgradeTool(string toolId)
        {
            int cost = ToolUpgradeCost(toolId);
            if (cost < 0) return false;
            if (game.Wallet.Xu < cost)
            {
                GameEvents.RaiseToast(Strings.NotEnoughXu);
                return false;
            }
            game.Wallet.Spend(cost);
            int lv = GetToolLevel(toolId) + 1;
            toolLevels[toolId] = lv;
            GameEvents.RaiseToast(string.Format(RecipeStrings.ToolUpgradedFmt, RecipeStrings.ToolName(toolId), lv));
            RaiseChanged();
            game.NotifyShopTransaction(-cost); // dời mốc Xu đầu ngày để thoát-vào không hoàn tiền; rồi lưu
            return true;
        }

        /// Cấp dụng cụ thấp nhất trong số dụng cụ món cần. int.MaxValue nếu món không đòi dụng cụ.
        public int MinToolLevel(RecipeDef r)
        {
            if (r == null || r.requiredTools == null || r.requiredTools.Length == 0) return int.MaxValue;
            int min = int.MaxValue;
            for (int i = 0; i < r.requiredTools.Length; i++)
                min = Mathf.Min(min, GetToolLevel(r.requiredTools[i]));
            return min;
        }

        /// Dụng cụ có cho phép 3 sao không (chỉ món ẩn bị chặn bởi dụng cụ cấp 1).
        public bool ToolAllowsThreeStars(RecipeDef r)
        {
            return r == null || !r.hidden || MinToolLevel(r) >= RecipeBalance.PerfectToolLevel;
        }

        /// Hạn thời gian cho "pha hoàn hảo": món thường = brewSeconds (luật Tier 1), món ẩn = brewSeconds x 1,2.
        public float PerfectSeconds(RecipeDef r)
        {
            if (r == null) return 0f;
            float seconds = r.hidden ? r.brewSeconds * RecipeBalance.PerfectTimeFactorHidden : r.brewSeconds;
            return seconds * MachineRules.PerfectTimeSlack(game.BrewTimeFactor); // máy tăng tốc: cửa sổ 3★ rộng hơn
        }

        /// 3 sao khi không sai bước nào và: món thường nhanh <= brewSeconds; món ẩn nhanh <= brewSeconds x 1,2 và dụng cụ >= cấp 2.
        public bool IsPerfect(RecipeDef r, float elapsed)
        {
            if (r == null) return false;
            return elapsed <= PerfectSeconds(r) && ToolAllowsThreeStars(r);
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed();
        }
    }
}
