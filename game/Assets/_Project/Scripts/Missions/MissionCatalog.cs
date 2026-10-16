using System;

namespace Cafe
{
    public enum MissionKind
    {
        Serve,        // phục vụ N khách
        SellRecipe,   // bán N ly một món cụ thể
        ThreeStar,    // pha N ly đạt 3★ (từ cấp 2)
        SellHidden,   // bán N món ẩn (từ cấp 2, khi đã biết ít nhất một món ẩn)
        NoWalkaway,   // kết thúc một ngày không khách nào bỏ về
        CleanDay,     // giữ độ sạch >= 80 cả một ngày (từ cấp 2)
        EarnTips,     // nhận N Xu tiền tip
        BuyItems,        // mua N món đồ cho quán
        RegularFavorite, // phục vụ món ruột cho khách quen N lần
        Experiment       // thử nghiệm công thức N lần ở Sổ công thức (từ cấp 2)
    }

    public sealed class MissionTemplate
    {
        public readonly string id;
        public readonly MissionKind kind;
        public readonly string recipeId;   // chỉ cho SellRecipe
        public readonly string label;      // tên món / đại lượng dùng trong câu mô tả
        public readonly int minTier;
        public readonly int rewardBase;    // Xu ở cấp 1; nhân với MissionCatalog.TierScale
        private readonly int[] targets;    // mục tiêu theo cấp 1..4
        public readonly int slot;          // 0 vận hành, 1 tay nghề / món, 2 quán / khách quen: mỗi ngày chọn mỗi khe một nhiệm vụ
        public readonly int rewardRp;      // thưởng thêm, không nhân theo cấp
        public readonly int rewardGems;

        public MissionTemplate(string id, MissionKind kind, string recipeId, string label, int minTier, int rewardBase, int[] targets,
            int slot = 0, int rewardRp = 0, int rewardGems = 0)
        {
            this.id = id;
            this.kind = kind;
            this.recipeId = recipeId;
            this.label = label;
            this.minTier = minTier;
            this.rewardBase = rewardBase;
            this.targets = targets;
            this.slot = slot;
            this.rewardRp = rewardRp;
            this.rewardGems = rewardGems;
        }

        public int TargetFor(int tier)
        {
            int i = Math.Max(1, Math.Min(tier, targets.Length)) - 1;
            return Math.Max(1, targets[i]);
        }
    }

    /// Dữ liệu ngữ cảnh khi sinh nhiệm vụ: món nào đã mở, có món ẩn nào chưa.
    public sealed class MissionContext
    {
        public Func<string, bool> RecipeKnown;
        public bool HasHiddenKnown;
    }

    /// Bảng nhiệm vụ ngày (economy_monetization.md §11.1). Thưởng Xu = rewardBase × TierScale.
    public static class MissionCatalog
    {
        private static readonly int[] TierScales = { 1, 4, 12, 30 };

        public static readonly MissionTemplate[] All =
        {
            new MissionTemplate("serve_30", MissionKind.Serve, null, null, 1, 50, new[] { 30, 60, 120, 240 }, 0),
            new MissionTemplate("sell_ca_phe_sua", MissionKind.SellRecipe, "ca_phe_sua", "Cà phê sữa", 1, 50, new[] { 10, 15, 25, 40 }, 1),
            new MissionTemplate("sell_tra_da", MissionKind.SellRecipe, "tra_da", "Trà đá", 1, 50, new[] { 12, 20, 32, 50 }, 1),
            new MissionTemplate("sell_ca_phe_den", MissionKind.SellRecipe, "ca_phe_den", "Cà phê đen", 1, 50, new[] { 8, 14, 22, 36 }, 1),
            new MissionTemplate("sell_bac_xiu", MissionKind.SellRecipe, "bac_xiu", "Bạc xỉu", 1, 60, new[] { 8, 12, 20, 32 }, 1),
            new MissionTemplate("three_star_5", MissionKind.ThreeStar, null, null, 2, 80, new[] { 5, 5, 8, 12 }, 1),
            new MissionTemplate("hidden_3", MissionKind.SellHidden, null, null, 2, 100, new[] { 3, 3, 5, 8 }, 1),
            new MissionTemplate("no_walkaway", MissionKind.NoWalkaway, null, null, 1, 80, new[] { 1, 1, 1, 1 }, 0),
            new MissionTemplate("clean_80", MissionKind.CleanDay, null, null, 2, 80, new[] { 1, 1, 1, 1 }, 0),
            new MissionTemplate("tips_300", MissionKind.EarnTips, null, null, 1, 60, new[] { 300, 800, 2400, 6000 }, 0),
            // Điều hướng người chơi vào trang trí / khách quen / khám phá món; thưởng chủ yếu RP và Hạt (economy §20)
            new MissionTemplate("buy_items", MissionKind.BuyItems, null, null, 1, 40, new[] { 1, 2, 3, 4 }, 2, 20),
            new MissionTemplate("regular_fav", MissionKind.RegularFavorite, null, null, 1, 50, new[] { 1, 1, 2, 3 }, 2, 25),
            new MissionTemplate("try_recipe", MissionKind.Experiment, null, null, 2, 40, new[] { 3, 3, 3, 3 }, 1, 15, 3),
        };

        public static MissionTemplate Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
                if (All[i].id == id) return All[i];
            return null;
        }

        /// 1 / 4 / 12 / 30 cho cấp 1..4.
        public static int TierScale(int tier)
        {
            return TierScales[Math.Max(1, Math.Min(tier, TierScales.Length)) - 1];
        }

        public static bool IsEligible(MissionTemplate t, int tier, MissionContext ctx)
        {
            if (t == null || tier < t.minTier) return false;
            if (t.kind == MissionKind.SellRecipe)
                return ctx != null && ctx.RecipeKnown != null && ctx.RecipeKnown(t.recipeId);
            if (t.kind == MissionKind.SellHidden)
                return ctx != null && ctx.HasHiddenKnown;
            return true;
        }
    }

    /// Ngày nhiệm vụ đổi lúc 04:00 giờ Việt Nam (UTC+7), tức 21:00 UTC.
    /// TODO(Backend): dùng giờ server khi có Firebase (economy §8.2); hiện chặn chỉnh giờ lùi bằng MissionBook.EnsureToday.
    public static class MissionClock
    {
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const double ShiftHours = 3.0; // 24h − (7h giờ VN − 4h mốc đổi ngày) cộng vào UTC để mốc rơi đúng 00:00

        public static int DayKey(DateTime utcNow)
        {
            return (int)Math.Floor((utcNow.AddHours(ShiftHours) - Epoch).TotalDays);
        }

        public static double SecondsUntilReset(DateTime utcNow)
        {
            DateTime shifted = utcNow.AddHours(ShiftHours);
            DateTime next = shifted.Date.AddDays(1);
            return (next - shifted).TotalSeconds;
        }
    }
}
