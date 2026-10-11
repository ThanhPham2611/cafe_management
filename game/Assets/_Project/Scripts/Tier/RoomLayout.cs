using UnityEngine;

namespace Cafe
{
    /// Hình học phòng thuê cấp 2 (nhà ống ~6 x 8 m, nhìn từ -Z về +Z) và bố cục nội thất mặc định.
    /// Hệ toạ độ: tâm phòng ở gốc (0,0,0); mặt tiền / cửa ở -Z (phía camera), tường sau ở +Z.
    /// Lưới 0.5 m: 12 x 16 ô, ô (0,0) ở góc trước-trái; trục y của ô = trục Z thế giới.
    public static class RoomLayout
    {
        public const float Width = 6f;                    // X
        public const float Depth = 8f;                    // Z
        public const float WallThickness = 0.2f;
        public const float WallHeight = 3.2f;
        public const float WainscotHeight = 0.6f;         // tường thấp còn lại khi bị cắt bớt
        public const float DoorHalfWidth = 0.75f;         // cửa rộng 1,5 m
        public const float DoorCenterX = 0f;
        public const float DoorHeight = 2.4f;

        public static readonly Vector2Int GridSize = new Vector2Int(12, 16);
        public static readonly Vector3 GridOrigin = new Vector3(-Width * 0.5f, 0f, -Depth * 0.5f);

        // Vỉa hè trước mặt tiền (khách đi dọc từ trái sang phải)
        public const float SidewalkDepth = 2.5f;
        public const float SidewalkHalfWidthCollider = 10f;
        public const float SidewalkHalfWidthVisual = 16f;

        // Khung camera mặc định cho phòng thuê (rộng/sâu hơn vỉa hè): lùi xa hơn và kéo tâm về phía mặt tiền để thấy cả vỉa hè
        public const float CameraDistance = 13f;
        public const float CameraPivotZ = 0.2f; // -1.6 làm quầy bar nằm sau thanh chip trên cùng

        public static float InteriorFrontZ { get { return -Depth * 0.5f; } }
        public static float InteriorBackZ { get { return Depth * 0.5f; } }
        public static float FrontWallOuterZ { get { return InteriorFrontZ - WallThickness; } }
        public static float SidewalkMidZ { get { return FrontWallOuterZ - SidewalkDepth * 0.5f; } }

        public static Vector3 SpawnPoint { get { return new Vector3(-8.5f, 0f, SidewalkMidZ); } }
        public static Vector3 ExitPoint { get { return new Vector3(8.5f, 0f, SidewalkMidZ); } }

        // ------------------------------------------------------------------ bố cục mặc định

        /// Đặt quầy bar, bồn rửa, kệ ly, 3 bộ bàn + 2 ghế, quạt, đèn thả. Thiếu def (chưa import model T2)
        /// thì dùng đồ cấp 1 tương đương, ưu tiên lấy từ kho để người chơi không bị nhận đồ trùng.
        public static void PlaceDefault(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            if (g == null || grid == null) return;
            PlaceBackWall(g, grid);
            PlaceTableSets(g, grid, tm);
            PlaceDecor(g, grid);
        }

        /// Chỉ đảm bảo có quầy pha (save T2 lỗi / bị thiếu quầy).
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

        private static void PlaceBackWall(GameBootstrap g, GridSystem grid)
        {
            PlacedFurniture bar = PlaceBar(g, grid);
            int barX = bar != null ? bar.Cell.x : grid.Size.x / 2 - 2;
            int barRight = bar != null ? bar.Cell.x + bar.Footprint.x : barX + 4;

            FurnitureDef sink = Usable(g.GetDef(TierConfig.IdSink));
            if (sink != null)
            {
                Vector2Int fp = FootprintOf(sink, 2);
                TryPlace(grid, sink, new Vector2Int(barRight, grid.Size.y - fp.y), 2);
            }
            FurnitureDef shelf = Usable(g.GetDef(TierConfig.IdShelf));
            if (shelf != null)
            {
                Vector2Int fp = FootprintOf(shelf, 2);
                TryPlace(grid, shelf, new Vector2Int(Mathf.Max(0, barX - fp.x), grid.Size.y - fp.y), 2);
            }
        }

        private static void PlaceTableSets(GameBootstrap g, GridSystem grid, TierManager tm)
        {
            // Góc dưới-trái của bàn (ô). Hai bàn hàng trước chừa lối cửa ở giữa (ô 4..7).
            Vector2Int[] spots =
            {
                new Vector2Int(1, 4),
                new Vector2Int(9, 4),
                new Vector2Int(5, 8),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                bool tableFromInv;
                FurnitureDef table = Acquire(g, tm, TierConfig.IdTable, TierConfig.IdTableFallback, FurnitureKind.Table, out tableFromInv);
                if (table == null) return;
                PlacedFurniture t = TryPlace(grid, table, spots[i], 0);
                if (t == null) continue;
                if (tableFromInv && tm != null) tm.TakeFromInventory(table.id);

                // Hai ghế hai bên bàn, quay mặt vào bàn (giống bố cục vỉa hè: xoay 1 bên trái, xoay 3 bên phải)
                Vector2Int tc = t.Cell, tf = t.Footprint;
                PlaceChair(g, grid, tm, new Vector2Int(tc.x - 1, tc.y + (tf.y - 1) / 2), 1);
                PlaceChair(g, grid, tm, new Vector2Int(tc.x + tf.x, tc.y + tf.y / 2), 3);
            }
        }

        private static void PlaceChair(GameBootstrap g, GridSystem grid, TierManager tm, Vector2Int cell, int rot)
        {
            bool fromInv;
            FurnitureDef chair = Acquire(g, tm, TierConfig.IdChair, TierConfig.IdChairFallback, FurnitureKind.Seat, out fromInv);
            if (chair == null) return;
            PlacedFurniture c = TryPlace(grid, chair, cell, rot);
            if (c != null && fromInv && tm != null) tm.TakeFromInventory(chair.id);
        }

        private static void PlaceDecor(GameBootstrap g, GridSystem grid)
        {
            FurnitureDef fan = Usable(g.GetDef(TierConfig.IdFan));
            if (fan != null) TryPlace(grid, fan, new Vector2Int(grid.Size.x / 2, 6), 0);

            FurnitureDef lamp = Usable(g.GetDef(TierConfig.IdLamp));
            if (lamp != null)
            {
                TryPlace(grid, lamp, new Vector2Int(2, 6), 0);
                TryPlace(grid, lamp, new Vector2Int(grid.Size.x - 3, 6), 0);
            }
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

        /// def chính (đồ cấp 2) nếu có; không thì đồ cùng loại trong kho; không thì def cấp 1 dự phòng.
        private static FurnitureDef Acquire(GameBootstrap g, TierManager tm, string primaryId, string fallbackId,
                                            FurnitureKind kind, out bool fromInventory)
        {
            fromInventory = false;
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
