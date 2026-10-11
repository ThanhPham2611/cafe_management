using UnityEngine;

namespace Cafe
{
    /// Luật giá / giới hạn của cửa hàng (GDD §2.1). Số món đã sở hữu = số món đang đặt trên lưới.
    public static class ShopRules
    {
        public const float SellRatio = 0.5f;

        public static bool IsShopItem(FurnitureDef d)
        {
            return d != null && d.shopVisible && d.kind != FurnitureKind.BrewStation && d.prefab != null;
        }

        /// Có bán / đặt được ở cấp quán này không (minTier <= cấp; onlyTier != 0 thì chỉ đúng cấp đó).
        public static bool AvailableInTier(FurnitureDef d, int tier)
        {
            return d != null && d.minTier <= tier && (d.onlyTier == 0 || d.onlyTier == tier);
        }

        /// Số món cùng loại đang nằm trong kho (đã mua, chưa đặt) - chỉ có sau khi chuyển cấp.
        public static int InventoryCount(FurnitureDef d)
        {
            GameBootstrap g = GameBootstrap.Instance;
            return (d != null && g != null && g.Tier != null) ? g.Tier.InventoryCount(d.id) : 0;
        }

        /// Số món đã sở hữu = đang đặt trên lưới + đang nằm trong kho.
        public static int OwnedCount(GridSystem grid, FurnitureDef d)
        {
            if (grid == null || d == null) return 0;
            int n = 0;
            for (int i = 0; i < grid.Placed.Count; i++)
            {
                FurnitureDef p = grid.Placed[i].Def;
                if (p == d || (!string.IsNullOrEmpty(d.id) && p.id == d.id)) n++;
            }
            return n + InventoryCount(d);
        }

        /// Giá món kế tiếp = price + priceStep × số đã có.
        public static int Price(GridSystem grid, FurnitureDef d)
        {
            return d.price + d.priceStep * OwnedCount(grid, d);
        }

        public static bool AtMax(GridSystem grid, FurnitureDef d)
        {
            return d.maxCount > 0 && OwnedCount(grid, d) >= d.maxCount;
        }

        /// Bán lại = 50% giá của món mua gần nhất (giá lúc đã sở hữu n-1 món).
        public static int SellValue(GridSystem grid, FurnitureDef d)
        {
            int owned = Mathf.Max(1, OwnedCount(grid, d));
            return Mathf.RoundToInt((d.price + d.priceStep * (owned - 1)) * SellRatio);
        }

        /// Σ attract của mọi món đang đặt (chưa chặn trần).
        public static int AttractSum(GridSystem grid)
        {
            if (grid == null) return 0;
            int sum = 0;
            for (int i = 0; i < grid.Placed.Count; i++) sum += grid.Placed[i].Def.attract;
            return sum;
        }
    }
}
