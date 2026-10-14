using UnityEngine;

namespace Cafe
{
    /// Hình học một phòng của quán (cấp 2: nhà ống ~6 x 8 m; cấp 3: nhà hàng ~10 x 12 m + các khu phụ ở hai bên).
    /// Hệ toạ độ: tâm phòng ở gốc (0,0,0); mặt tiền / cửa ở -Z (phía camera), tường sau ở +Z.
    /// Lưới 0.5 m, ô (0,0) ở góc trước-trái; trục y của ô = trục Z thế giới.
    /// Width / Depth là kích thước SẢNH CHÍNH; AnnexWidth là bề rộng mỗi dải khu phụ ở hai bên sảnh (0 = không có khu phụ):
    /// lưới rộng Width + 2 x AnnexWidth nên toạ độ ô của sảnh không đổi khi mở thêm khu.
    public sealed class RoomSpec
    {
        public readonly int Tier;
        public readonly float Width;                 // X
        public readonly float Depth;                 // Z
        public readonly float DoorHalfWidth;
        public readonly float CameraDistance;
        public readonly float CameraPivotZ;
        public readonly float SidewalkHalfWidthCollider;
        public readonly float WalkInset;             // khách đi vào/ra cách tường hông bao xa trên vỉa hè
        public readonly float AnnexWidth;            // bề rộng mỗi dải khu phụ (trái / phải sảnh), 0 = không có

        public RoomSpec(int tier, float width, float depth, float doorHalfWidth, float cameraDistance, float cameraPivotZ, float walkInset,
                        float annexWidth = 0f)
        {
            Tier = tier;
            Width = width;
            Depth = depth;
            DoorHalfWidth = doorHalfWidth;
            CameraDistance = cameraDistance;
            CameraPivotZ = cameraPivotZ;
            WalkInset = walkInset;
            AnnexWidth = annexWidth;
            SidewalkHalfWidthCollider = Mathf.Max(10f, width * 0.5f + walkInset + 1.5f);
        }

        public bool HasZones { get { return AnnexWidth > 0f; } }
        /// Bề rộng cả lưới (sảnh + hai dải khu phụ).
        public float TotalWidth { get { return Width + 2f * AnnexWidth; } }
        public int AnnexCells { get { return Mathf.RoundToInt(AnnexWidth / GameBalance.CellSize); } }

        public Vector2Int GridSize
        {
            get { return new Vector2Int(Mathf.RoundToInt(TotalWidth / GameBalance.CellSize), Mathf.RoundToInt(Depth / GameBalance.CellSize)); }
        }

        public Vector3 GridOrigin { get { return new Vector3(-TotalWidth * 0.5f, 0f, -Depth * 0.5f); } }

        /// Bản đồ khu của lưới; null khi phòng không có khu phụ (cấp 2).
        public ZoneMap CreateZoneMap()
        {
            if (!HasZones) return null;
            return new ZoneMap(AnnexCells, Mathf.RoundToInt(Width / GameBalance.CellSize), Mathf.RoundToInt(Depth / GameBalance.CellSize));
        }

        /// Hình chữ nhật (x, z thế giới) của một khu; chỉ dùng khi HasZones.
        public Rect ZoneWorldRect(ZoneId zone)
        {
            ZoneMap m = CreateZoneMap();
            RectInt c = m.Rects[(int)zone];
            Vector3 o = GridOrigin;
            float cs = GameBalance.CellSize;
            return new Rect(o.x + c.xMin * cs, o.z + c.yMin * cs, c.width * cs, c.height * cs);
        }
        public float InteriorFrontZ { get { return -Depth * 0.5f; } }
        public float InteriorBackZ { get { return Depth * 0.5f; } }
        public float FrontWallOuterZ { get { return InteriorFrontZ - RoomLayout.WallThickness; } }
        public float SidewalkMidZ { get { return FrontWallOuterZ - RoomLayout.SidewalkDepth * 0.5f; } }
        public Vector3 SpawnPoint { get { return new Vector3(-(Width * 0.5f + WalkInset), 0f, SidewalkMidZ); } }
        public Vector3 ExitPoint { get { return new Vector3(Width * 0.5f + WalkInset, 0f, SidewalkMidZ); } }
    }

    /// Phòng theo cấp quán + bố cục nội thất mặc định.
    public static class RoomLayout
    {
        public const float WallThickness = 0.2f;
        public const float WallHeight = 3.2f;
        public const float WainscotHeight = 0.6f;         // tường thấp còn lại khi bị cắt bớt
        public const float DoorCenterX = 0f;
        public const float DoorHeight = 2.4f;

        // Vỉa hè trước mặt tiền (khách đi dọc từ trái sang phải)
        public const float SidewalkDepth = 2.5f;
        public const float SidewalkHalfWidthVisual = 16f;

        // camera: lùi xa hơn và kéo tâm về phía mặt tiền để thấy cả vỉa hè
        public static readonly RoomSpec Tier2 = new RoomSpec(2, 6f, 8f, 0.75f, 13f, 0.2f, 5.5f);
        public static readonly RoomSpec Tier3 = new RoomSpec(3, 10f, 12f, 1.1f, 19.5f, 0.4f, 5.5f, 6f);

        public static RoomSpec For(int tier)
        {
            return tier >= 3 ? Tier3 : Tier2;
        }

        // ------------------------------------------------------------------ bố cục mặc định

        // Góc dưới-trái của bàn (ô). Hai bàn hàng trước chừa lối cửa ở giữa.
        private static readonly Vector2Int[] SpotsTier2 =
        {
            new Vector2Int(1, 4),
            new Vector2Int(9, 4),
            new Vector2Int(5, 8),
        };

        // Nhà hàng 20 x 24 ô: 10 chỗ đặt bộ bàn (bàn 2x2 + ghế hai bên = 4x2 ô); lối cửa ở giữa hàng trước, hành lang trước quầy.
        private static readonly Vector2Int[] SpotsTier3 =
        {
            new Vector2Int(2, 6), new Vector2Int(14, 6),
            new Vector2Int(2, 10), new Vector2Int(8, 10), new Vector2Int(14, 10),
            new Vector2Int(2, 14), new Vector2Int(8, 14), new Vector2Int(14, 14),
            new Vector2Int(2, 18), new Vector2Int(14, 18),
        };

        /// Số bộ bàn ghế tặng sẵn khi mở nhà hàng (bộ trong kho được dùng trước, thiếu thì tặng thêm cho đủ).
        public const int Tier3BaselineSets = 6;

        /// Đặt quầy bar, bồn rửa, kệ ly, các bộ bàn + 2 ghế, quạt, đèn thả. Đồ đã mua nằm trong kho được dùng trước
        /// (không nhận trùng); thiếu def (chưa import model) thì dùng đồ cấp 1 tương đương.
        public static void PlaceDefault(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            if (g == null || grid == null) return;
            bool restaurant = tm != null && tm.Current >= 3;
            PlaceBackWall(g, grid, tm);
            PlaceTableSets(g, grid, tm, restaurant ? SpotsTier3 : SpotsTier2, restaurant ? Tier3BaselineSets : SpotsTier2.Length, restaurant,
                HallOrigin(grid));
            if (restaurant) PlaceDecorTier3(g, grid, tm);
            else PlaceDecorTier2(g, grid, tm);
        }

        /// Ô góc dưới-trái của sảnh chính (lưới cấp 3 có thêm dải khu phụ bên trái nên sảnh không bắt đầu ở ô 0).
        public static Vector2Int HallOrigin(GridSystem grid)
        {
            return grid.Zones != null ? grid.Zones.Hall.min : Vector2Int.zero;
        }

        /// Chỗ đặt bộ bàn ghế tặng khi mở khu: toạ độ ô trong khu (từ góc dưới-trái của khu), chừa lối vào từ lỗ mở trên tường sảnh.
        private static readonly Vector2Int[] StarterSpotsGarden = { new Vector2Int(2, 4), new Vector2Int(8, 4) };
        private static readonly Vector2Int[] StarterSpotsAnnex = { new Vector2Int(6, 1), new Vector2Int(6, 9) };

        /// Tặng GameBalance.ZoneStarterSets bộ bàn + 2 ghế khi mở khu (đồ trong kho được dùng trước, thiếu thì tặng cho đủ).
        /// Trả về số bộ đã đặt.
        public static int PlaceZoneStarter(GameBootstrap g, GridSystem grid, TierManager tm, ZoneId zone)
        {
            if (g == null || grid == null || grid.Zones == null || zone == ZoneId.Hall) return 0;
            Vector2Int[] spots = zone == ZoneId.Garden ? StarterSpotsGarden : StarterSpotsAnnex;
            int before = grid.Seats.Count;
            Vector2Int origin = grid.Zones.Rects[(int)zone].min;
            PlaceTableSets(g, grid, tm, spots, GameBalance.ZoneStarterSets, true, origin);
            return (grid.Seats.Count - before) / 2;
        }

        /// Chỉ đảm bảo có quầy pha (save lỗi / bị thiếu quầy).
        public static bool EnsureBar(GameBootstrap g, GridSystem grid)
        {
            if (grid.BrewStationFurniture != null) return true;
            return PlaceBar(g, grid) != null;
        }

        private static PlacedFurniture PlaceBar(GameBootstrap g, GridSystem grid)
        {
            FurnitureDef bar = Usable(g.GetDef(TierConfig.IdBar));
            if (bar == null) bar = Usable(g.GetDef(TierConfig.IdBarFallback));
            if (bar == null)
            {
                Debug.LogWarning("[RoomLayout] Không có FurnitureDef quầy pha chế (" + TierConfig.IdBar + " / " + TierConfig.IdBarFallback + ")");
                return null;
            }
            // Xoay 180°: mặt trước (+Z của model, nơi chủ quán đứng và gắn biển) hướng ra phía khách/camera
            Vector2Int fp = FootprintOf(bar, 2);
            var cell = new Vector2Int(grid.Size.x / 2 - fp.x / 2, grid.Size.y - fp.y);
            return TryPlace(grid, bar, cell, 2);
        }

        private static void PlaceBackWall(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            PlacedFurniture bar = PlaceBar(g, grid);
            int barX = bar != null ? bar.Cell.x : grid.Size.x / 2 - 2;
            int barRight = bar != null ? bar.Cell.x + bar.Footprint.x : barX + 4;

            bool fromInv;
            FurnitureDef sink = Obtain(g, tm, TierConfig.IdSink, out fromInv);
            if (sink != null)
            {
                Vector2Int fp = FootprintOf(sink, 2);
                if (TryPlace(grid, sink, new Vector2Int(barRight, grid.Size.y - fp.y), 2) != null && fromInv) tm.TakeFromInventory(sink.id);
            }
            FurnitureDef shelf = Obtain(g, tm, TierConfig.IdShelf, out fromInv);
            if (shelf != null)
            {
                Vector2Int fp = FootprintOf(shelf, 2);
                if (TryPlace(grid, shelf, new Vector2Int(Mathf.Max(0, barX - fp.x), grid.Size.y - fp.y), 2) != null && fromInv) tm.TakeFromInventory(shelf.id);
            }
        }

        private static void PlaceTableSets(GameBootstrap g, GridSystem grid, TierManager tm, Vector2Int[] spots, int baseline, bool inventoryFirst,
                                           Vector2Int origin)
        {
            // Cấp 3: mọi bàn đang nằm trong kho được bày ra trước (tối đa số chỗ), thiếu thì tặng thêm cho đủ baseline.
            int sets = baseline;
            if (inventoryFirst && tm != null) sets = Mathf.Max(baseline, tm.InventoryKindCount(FurnitureKind.Table));
            sets = Mathf.Min(sets, spots.Length);

            for (int i = 0; i < sets; i++)
            {
                bool tableFromInv;
                FurnitureDef table = Acquire(g, tm, TierConfig.IdTable, TierConfig.IdTableFallback, FurnitureKind.Table, inventoryFirst, out tableFromInv);
                if (table == null) return;
                PlacedFurniture t = TryPlace(grid, table, spots[i] + origin, 0);
                if (t == null) continue;
                if (tableFromInv && tm != null) tm.TakeFromInventory(table.id);

                // Hai ghế hai bên bàn, quay mặt vào bàn (giống bố cục vỉa hè: xoay 1 bên trái, xoay 3 bên phải)
                Vector2Int tc = t.Cell, tf = t.Footprint;
                PlaceChair(g, grid, tm, new Vector2Int(tc.x - 1, tc.y + (tf.y - 1) / 2), 1, inventoryFirst);
                PlaceChair(g, grid, tm, new Vector2Int(tc.x + tf.x, tc.y + tf.y / 2), 3, inventoryFirst);
            }
        }

        private static void PlaceChair(GameBootstrap g, GridSystem grid, TierManager tm, Vector2Int cell, int rot, bool inventoryFirst)
        {
            bool fromInv;
            FurnitureDef chair = Acquire(g, tm, TierConfig.IdChair, TierConfig.IdChairFallback, FurnitureKind.Seat, inventoryFirst, out fromInv);
            if (chair == null) return;
            PlacedFurniture c = TryPlace(grid, chair, cell, rot);
            if (c != null && fromInv && tm != null) tm.TakeFromInventory(chair.id);
        }

        private static void PlaceDecorTier2(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            PlaceOverhead(g, grid, tm, TierConfig.IdFan, new Vector2Int(grid.Size.x / 2, 6));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(2, 6));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(grid.Size.x - 3, 6));
        }

        private static void PlaceDecorTier3(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            // Quạt và đèn thả treo trên cao (không chiếm ô): rải đều trên các hàng bàn của sảnh chính
            Vector2Int o = HallOrigin(grid);
            int w = grid.Zones != null ? grid.Zones.Hall.width : grid.Size.x;
            PlaceOverhead(g, grid, tm, TierConfig.IdFan, new Vector2Int(o.x + w / 4, 12));
            PlaceOverhead(g, grid, tm, TierConfig.IdFan, new Vector2Int(o.x + w * 3 / 4, 12));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(o.x + w / 4, 8));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(o.x + w * 3 / 4, 8));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(o.x + w / 4, 16));
            PlaceOverhead(g, grid, tm, TierConfig.IdLamp, new Vector2Int(o.x + w * 3 / 4, 16));
        }

        private static void PlaceOverhead(GameBootstrap g, GridSystem grid, TierManager tm, string id, Vector2Int cell)
        {
            bool fromInv;
            FurnitureDef d = Obtain(g, tm, id, out fromInv);
            if (d == null) return;
            if (TryPlace(grid, d, cell, 0) != null && fromInv) tm.TakeFromInventory(d.id);
        }

        // ------------------------------------------------------------------ helpers

        private static FurnitureDef Usable(FurnitureDef d)
        {
            return d != null && d.prefab != null ? d : null;
        }

        private static Vector2Int FootprintOf(FurnitureDef d, int rot)
        {
            return GridSystem.RotatedFootprint(d.footprint, rot);
        }

        /// Món theo id: ưu tiên bản đang nằm trong kho (người chơi đã mua, không tặng trùng), không thì def tặng sẵn.
        private static FurnitureDef Obtain(GameBootstrap g, TierManager tm, string id, out bool fromInventory)
        {
            fromInventory = tm != null && tm.InventoryCount(id) > 0;
            return Usable(g.GetDef(id));
        }

        /// inventoryFirst: lấy món cùng loại trong kho trước (cấp 3). Không thì def chính (đồ cấp 2) nếu có; không thì
        /// đồ cùng loại trong kho; không thì def cấp 1 dự phòng.
        private static FurnitureDef Acquire(GameBootstrap g, TierManager tm, string primaryId, string fallbackId,
                                            FurnitureKind kind, bool inventoryFirst, out bool fromInventory)
        {
            fromInventory = false;
            if (inventoryFirst && tm != null)
            {
                FurnitureDef owned = tm.FindInventoryDef(kind, kind == FurnitureKind.Seat);
                if (owned != null)
                {
                    fromInventory = true;
                    return owned;
                }
            }
            FurnitureDef d = Usable(g.GetDef(primaryId));
            if (d != null) return d;
            if (tm != null)
            {
                d = tm.FindInventoryDef(kind, kind == FurnitureKind.Seat);
                if (d != null)
                {
                    fromInventory = true;
                    return d;
                }
            }
            return Usable(g.GetDef(fallbackId));
        }

        /// Đặt tại ô mong muốn; bị chặn thì tìm ô trống gần nhất (vòng 1..6 ô).
        public static PlacedFurniture TryPlace(GridSystem grid, FurnitureDef d, Vector2Int cell, int rot)
        {
            if (d == null) return null;
            Vector2Int fp = GridSystem.RotatedFootprint(d.footprint, rot);
            cell.x = Mathf.Clamp(cell.x, 0, Mathf.Max(0, grid.Size.x - fp.x));
            cell.y = Mathf.Clamp(cell.y, 0, Mathf.Max(0, grid.Size.y - fp.y));
            PlacedFurniture pf = grid.Place(d, cell, rot);
            if (pf != null) return pf;

            for (int r = 1; r <= 6; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (c.x < 0 || c.y < 0 || c.x + fp.x > grid.Size.x || c.y + fp.y > grid.Size.y) continue;
                        pf = grid.Place(d, c, rot);
                        if (pf != null) return pf;
                    }
                }
            }
            Debug.LogWarning("[RoomLayout] Không đặt được " + d.id + " gần ô " + cell);
            return null;
        }
    }
}
