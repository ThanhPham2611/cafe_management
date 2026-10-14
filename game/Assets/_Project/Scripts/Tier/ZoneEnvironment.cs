using UnityEngine;
using UnityEngine.Rendering;

namespace Cafe
{
    /// Dựng môi trường 3D của các khu phụ cấp 3 bằng khối palette (cùng cách với EnvironmentBuilder):
    ///  - BuildZone: khu đã mở = sàn + hàng rào / lan can / tường riêng + collider sàn (cho NavMesh) + collider nối với lỗ mở trên tường sảnh.
    ///  - BuildLot: khu chưa mở = bãi đất rào chắn kèm biển "chờ xây" (không collider, không đi vào được).
    /// Sàn của khu cách sàn sảnh 0,2 m (đúng chỗ tường dày 0,2 m) nên NavMesh chỉ nối hai bên qua collider nối ở lỗ mở.
    /// Tất cả nằm trong lưới 44 x 24 ô của RoomSpec cấp 3; xem ZoneMap cho bản đồ ô.
    internal static class ZoneEnvironment
    {
        private const float WallGap = 0.1f;          // sàn khu phụ cách đường biên 0,1 m (sàn sảnh cũng thu vào 0,1 m: khe 0,2 m = bề dày tường)
        private const float LinkWidth = 0.6f;        // collider nối qua tường, chồng lên sàn hai bên 0,2 m

        // Ô palette (art_bible §3)
        private const int Grass = 33, GrassLight = 32, HedgeDark = 34, Stone = 45, Soil = 46;
        private const int DeckA = 17, DeckB = 16, DeckC = 21;
        private const int VipWood = 20, VipCarpet = 25, VipGold = 57, VipRed = 56;
        private const int Orange = 59, White = 31, SignRed = 56, Paper = 14;

        private static readonly EnvironmentBuilder.WallStyle VipStyle = new EnvironmentBuilder.WallStyle
        {
            lower = VipWood, cap = VipGold, upper = 38
        };

        // ------------------------------------------------------------------ khu đã mở

        public static void BuildZone(Transform room, Material mat, RoomSpec r, ZoneId zone)
        {
            var go = new GameObject("Zone_" + zone);
            go.transform.SetParent(room, false);
            Transform zt = go.transform;
            Rect w = r.ZoneWorldRect(zone);
            switch (zone)
            {
                case ZoneId.Garden: BuildGarden(zt, mat, w); break;
                case ZoneId.Terrace: BuildTerrace(room, zt, mat, r, w); break;
                case ZoneId.Vip: BuildVip(room, zt, mat, r, w); break;
            }
        }

        private static void BuildGarden(Transform zt, Material mat, Rect w)
        {
            // Sàn đi được: bỏ khe sát tường sảnh (bên phải), nối bằng collider ở lỗ mở giữa tường
            AddGround(zt, "Ground", w.xMin, w.xMax - WallGap, w.yMin, w.yMax);
            AddGround(zt, "Ground_Link", w.xMax - LinkWidth * 0.5f, w.xMax + LinkWidth * 0.5f,
                -EnvironmentBuilder.AnnexOpeningHalf, EnvironmentBuilder.AnnexOpeningHalf);

            // Cỏ (ô sáng / tối xen kẽ 1 m) + lối đá từ lỗ mở vào sân
            var floor = new PaletteMeshBuilder();
            float cx = w.center.x, cz = w.center.y;
            floor.AddUpQuad(cx, cz, 0.002f, w.width, w.height, Grass);
            int cols = Mathf.RoundToInt(w.width), rows = Mathf.RoundToInt(w.height);
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                    if (((i + j) & 1) == 0)
                        floor.AddUpQuad(w.xMin + i + 0.5f, w.yMin + j + 0.5f, 0.004f, 0.96f, 0.96f, GrassLight);
            for (float x = w.xMax - 0.5f; x > w.xMin + 2.2f; x -= 0.78f)
                for (int k = -1; k <= 1; k++)
                    floor.AddUpQuad(x, k * 0.72f, 0.007f, 0.64f, 0.64f, Stone);
            MakeMesh(zt, mat, "Floor", floor, "SM_Zone_Garden_Floor");

            // Hàng rào cây, đèn dây, cây to bên ngoài (không chiếm ô lưới)
            var deco = new PaletteMeshBuilder();
            AddHedge(deco, w.xMin, w.center.y, 0.3f, w.height + 0.3f);              // trái
            AddHedge(deco, w.center.x, w.yMin, w.width + 0.3f, 0.3f);               // trước (phía vỉa hè)
            AddHedge(deco, w.center.x, w.yMax, w.width + 0.3f, 0.3f);               // sau
            AddPost(deco, w.xMin, w.yMin, 2.6f);
            AddPost(deco, w.xMin, w.yMax, 2.6f);
            AddPost(deco, w.center.x, w.yMin, 2.6f);
            AddPost(deco, w.center.x, w.yMax, 2.6f);
            AddStrand(deco, new Vector2(w.xMin, w.yMin), new Vector2(w.xMin, w.yMax), 2.55f, 0.35f);
            AddStrand(deco, new Vector2(w.xMin, w.yMin), new Vector2(w.center.x, w.yMin), 2.55f, 0.25f);
            AddStrand(deco, new Vector2(w.center.x, w.yMin), new Vector2(w.xMax - 0.1f, w.yMin), 2.55f, 0.25f);
            AddStrand(deco, new Vector2(w.xMin, w.yMax), new Vector2(w.center.x, w.yMax), 2.55f, 0.25f);
            AddStrand(deco, new Vector2(w.center.x, w.yMax), new Vector2(w.xMax - 0.1f, w.yMax), 2.55f, 0.25f);
            AddTree(deco, w.xMin - 0.9f, -4.4f, 1.0f);
            AddTree(deco, w.xMin - 1.0f, 0.2f, 1.25f);
            AddTree(deco, w.xMin - 0.9f, 4.6f, 1.0f);
            AddTree(deco, w.xMin + 1.2f, w.yMax + 1.0f, 0.9f);
            AddTree(deco, w.xMin + 4.0f, w.yMax + 1.1f, 1.1f);
            MakeMesh(zt, mat, "Decor", deco, "SM_Zone_Garden_Decor");
        }

        private static void BuildTerrace(Transform room, Transform zt, Material mat, RoomSpec r, Rect w)
        {
            AddGround(zt, "Ground", w.xMin + WallGap, w.xMax, w.yMin, w.yMax - WallGap);
            AddGround(zt, "Ground_Link", w.xMin - LinkWidth * 0.5f, w.xMin + LinkWidth * 0.5f,
                -EnvironmentBuilder.AnnexOpeningZ - EnvironmentBuilder.AnnexOpeningHalf, -EnvironmentBuilder.AnnexOpeningZ + EnvironmentBuilder.AnnexOpeningHalf);

            // Sàn gác gỗ: ván dọc theo trục X, dài 1,5 m, lệch hàng
            var floor = new PaletteMeshBuilder();
            floor.AddUpQuad(w.center.x, w.center.y, 0.001f, w.width, w.height, TierConfig.CellGrout);
            int[] tones = { DeckA, DeckB, DeckA, DeckC };
            const float plank = 0.25f, gap = 0.012f, length = 1.5f;
            int rows = Mathf.RoundToInt(w.height / plank);
            for (int j = 0; j < rows; j++)
            {
                float z = w.yMin + (j + 0.5f) * plank;
                float x = w.xMin - (j % 3) * (length / 3f);
                for (int n = 0; x < w.xMax; n++, x += length)
                {
                    float a = Mathf.Max(x, w.xMin), e = Mathf.Min(x + length, w.xMax);
                    if (e - a < 0.05f) continue;
                    floor.AddUpQuad((a + e) * 0.5f, z, 0.003f, e - a - gap, plank - gap, tones[(j * 5 + n * 3) % tones.Length]);
                }
            }
            MakeMesh(zt, mat, "Floor", floor, "SM_Zone_Terrace_Floor");

            // Lan can trắng (phải + trước) nhìn ra phố
            var rail = new PaletteMeshBuilder();
            AddBalustrade(rail, new Vector2(w.xMax, w.yMin), new Vector2(w.xMax, w.yMax));
            AddBalustrade(rail, new Vector2(w.xMin, w.yMin), new Vector2(w.xMax, w.yMin));
            // Cột pergola ở hai góc ngoài + xà ngang thấp, treo đèn lồng
            AddPost(rail, w.xMax, w.yMin, 2.6f);
            AddPost(rail, w.xMax, w.yMax - 0.15f, 2.6f);
            AddStrand(rail, new Vector2(w.xMax, w.yMin), new Vector2(w.xMax, w.yMax - 0.15f), 2.55f, 0.3f);
            MakeMesh(zt, mat, "Rail", rail, "SM_Zone_Terrace_Rail");

            EnsurePartition(room, mat, r);
        }

        private static void BuildVip(Transform room, Transform zt, Material mat, RoomSpec r, Rect w)
        {
            AddGround(zt, "Ground", w.xMin + WallGap, w.xMax, w.yMin + WallGap, w.yMax);
            AddGround(zt, "Ground_Link", w.xMin - LinkWidth * 0.5f, w.xMin + LinkWidth * 0.5f,
                EnvironmentBuilder.AnnexOpeningZ - EnvironmentBuilder.AnnexOpeningHalf, EnvironmentBuilder.AnnexOpeningZ + EnvironmentBuilder.AnnexOpeningHalf);

            // Gỗ tối + thảm đỏ viền vàng + hoa văn hình thoi ở giữa
            var floor = new PaletteMeshBuilder();
            float cx = w.center.x, cz = w.center.y;
            floor.AddUpQuad(cx, cz, 0.002f, w.width, w.height, VipWood);
            floor.AddUpQuad(cx, cz, 0.004f, w.width - 0.9f, w.height - 0.9f, VipCarpet);
            float bx = (w.width - 0.9f) * 0.5f, bz = (w.height - 0.9f) * 0.5f;
            floor.AddUpQuad(cx, cz - bz, 0.006f, w.width - 0.9f, 0.07f, VipGold);
            floor.AddUpQuad(cx, cz + bz, 0.006f, w.width - 0.9f, 0.07f, VipGold);
            floor.AddUpQuad(cx - bx, cz, 0.006f, 0.07f, w.height - 0.9f, VipGold);
            floor.AddUpQuad(cx + bx, cz, 0.006f, 0.07f, w.height - 0.9f, VipGold);
            floor.AddUpDiamond(cx, cz, 0.006f, 1.4f, VipGold);
            floor.AddUpDiamond(cx, cz, 0.008f, 1.28f, VipRed);
            MakeMesh(zt, mat, "Floor", floor, "SM_Zone_Vip_Floor");

            // Tường cao (phải + sau) có tranh; tường ngăn với lầu dựng chung
            EnvironmentBuilder.MakeWall(zt, mat, r, "Wall_VipRight", new Vector3(w.xMax, 0f, w.center.y), false, w.height + 0.2f,
                Vector3.right, null, false, true, VipStyle);
            EnvironmentBuilder.MakeWall(zt, mat, r, "Wall_VipBack", new Vector3(w.center.x, 0f, w.yMax), true, w.width + 0.2f,
                Vector3.forward, null, false, true, VipStyle);

            EnsurePartition(room, mat, r);
        }

        /// Tường ngăn lầu / VIP (z = 0): cần khi một trong hai khu mở; dựng một lần.
        private static void EnsurePartition(Transform room, Material mat, RoomSpec r)
        {
            if (room.Find("Partition_Right") != null) return;
            Rect w = r.ZoneWorldRect(ZoneId.Terrace);
            EnvironmentBuilder.MakeWall(room, mat, r, "Partition_Right", new Vector3(w.center.x, 0f, w.yMax), true, w.width,
                Vector3.back, null, false, false, EnvironmentBuilder.HallStyle);
        }

        // ------------------------------------------------------------------ khu chưa mở

        public static void BuildLot(Transform room, Material mat, RoomSpec r, ZoneId zone)
        {
            var go = new GameObject("Lot_" + zone);
            go.transform.SetParent(room, false);
            Rect w = r.ZoneWorldRect(zone);

            var b = new PaletteMeshBuilder();
            float cx = w.center.x, cz = w.center.y;
            // Nền đất nung + vài viên sỏi
            b.AddUpQuad(cx, cz, 0.002f, w.width - 0.1f, w.height - 0.1f, Soil);
            for (int i = 0; i < 9; i++)
            {
                float fx = Mathf.Repeat(i * 0.6180339f + (int)zone * 0.31f, 1f), fz = Mathf.Repeat(i * 0.4142136f + 0.2f, 1f);
                b.AddUpQuad(w.xMin + 0.5f + fx * (w.width - 1f), w.yMin + 0.5f + fz * (w.height - 1f), 0.004f, 0.5f, 0.38f, Stone);
            }

            // Hàng rào chắn cam / trắng (bỏ cạnh sát tường sảnh, đã có tường)
            // sân vườn nằm bên trái sảnh (tường sảnh ở cạnh phải khu), lầu / VIP nằm bên phải (tường sảnh ở cạnh trái khu)
            float outerX = zone == ZoneId.Garden ? w.xMin + 0.15f : w.xMax - 0.15f;
            AddBarrierLine(b, new Vector2(outerX, w.yMin + 0.15f), new Vector2(outerX, w.yMax - 0.15f));
            AddBarrierLine(b, new Vector2(w.xMin + 0.15f, w.yMin + 0.15f), new Vector2(w.xMax - 0.15f, w.yMin + 0.15f));
            AddBarrierLine(b, new Vector2(w.xMin + 0.15f, w.yMax - 0.15f), new Vector2(w.xMax - 0.15f, w.yMax - 0.15f));

            // Biển "chờ xây": cột + bảng đỏ, dải trắng, đồng xu vàng; hai nón cam
            b.AddBox(new Vector3(cx - 0.6f, 0.75f, cz), new Vector3(0.1f, 1.5f, 0.1f), TierConfig.CellWoodDark);
            b.AddBox(new Vector3(cx + 0.6f, 0.75f, cz), new Vector3(0.1f, 1.5f, 0.1f), TierConfig.CellWoodDark);
            b.AddBox(new Vector3(cx, 1.45f, cz), new Vector3(1.6f, 0.8f, 0.07f), SignRed);
            b.AddBox(new Vector3(cx, 1.62f, cz - 0.04f), new Vector3(1.35f, 0.14f, 0.05f), Paper);
            b.AddBox(new Vector3(cx, 1.3f, cz - 0.04f), new Vector3(0.7f, 0.2f, 0.05f), VipGold);
            AddCone(b, w.xMin + 0.7f, w.yMin + 0.7f);
            AddCone(b, w.xMax - 0.7f, w.yMin + 0.7f);

            MakeMesh(go.transform, mat, "Lot", b, "SM_Lot_" + zone);
        }

        // ------------------------------------------------------------------ khối dựng chung

        private static void MakeMesh(Transform parent, Material mat, string name, PaletteMeshBuilder b, string meshName)
        {
            GameObject g = PaletteBox.MakeFromMesh(name, b.ToMesh(meshName, true), mat, parent, Vector3.zero);
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// Collider sàn (mặt trên ở y = 0) cho NavMesh + raycast chạm. x0..x1, z0..z1 theo toạ độ thế giới.
        private static void AddGround(Transform parent, string name, float x0, float x1, float z0, float z1)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f);
            g.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, 0.1f, z1 - z0);
        }

        /// Hàng rào cây thấp: thân tối + mặt trên sáng.
        private static void AddHedge(PaletteMeshBuilder b, float cx, float cz, float sx, float sz)
        {
            b.AddBox(new Vector3(cx, 0.43f, cz), new Vector3(sx, 0.86f, sz), HedgeDark);
            b.AddBox(new Vector3(cx, 0.9f, cz), new Vector3(sx - 0.06f, 0.1f, sz - 0.06f), Grass);
        }

        private static void AddPost(PaletteMeshBuilder b, float x, float z, float height)
        {
            b.AddBox(new Vector3(x, height * 0.5f, z), new Vector3(0.11f, height, 0.11f), TierConfig.CellWoodDark);
        }

        /// Dây đèn: bóng vàng nhỏ cách nhau ~0,5 m, võng xuống giữa dây (sag m).
        private static void AddStrand(PaletteMeshBuilder b, Vector2 a, Vector2 c, float height, float sag)
        {
            float len = Vector2.Distance(a, c);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.5f));
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                Vector2 p = Vector2.Lerp(a, c, t);
                float y = height - sag * 4f * t * (1f - t);
                b.AddBox(new Vector3(p.x, y, p.y), new Vector3(0.12f, 0.12f, 0.12f), VipGold);
            }
        }

        /// Cây to: thân + ba tầng tán lá (hình hộp, hợp phong cách khối của game).
        private static void AddTree(PaletteMeshBuilder b, float x, float z, float scale)
        {
            b.AddBox(new Vector3(x, 0.75f * scale, z), new Vector3(0.26f * scale, 1.6f * scale, 0.26f * scale), TierConfig.CellWoodMid);
            b.AddBox(new Vector3(x, 1.7f * scale, z), new Vector3(1.7f * scale, 0.7f * scale, 1.7f * scale), HedgeDark);
            b.AddBox(new Vector3(x, 2.3f * scale, z), new Vector3(1.3f * scale, 0.7f * scale, 1.3f * scale), Grass);
            b.AddBox(new Vector3(x, 2.85f * scale, z), new Vector3(0.8f * scale, 0.55f * scale, 0.8f * scale), GrassLight);
        }

        /// Lan can: cột trắng mỗi 1 m, thanh trên gỗ óc chó, thanh giữa trắng.
        private static void AddBalustrade(PaletteMeshBuilder b, Vector2 a, Vector2 c)
        {
            float len = Vector2.Distance(a, c);
            int n = Mathf.Max(1, Mathf.RoundToInt(len));
            Vector2 dir = (c - a).normalized;
            bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
            Vector2 mid = (a + c) * 0.5f;
            Vector3 railSize = alongX ? new Vector3(len, 0.07f, 0.12f) : new Vector3(0.12f, 0.07f, len);
            Vector3 midSize = alongX ? new Vector3(len, 0.04f, 0.05f) : new Vector3(0.05f, 0.04f, len);
            b.AddBox(new Vector3(mid.x, 1.0f, mid.y), railSize, TierConfig.CellWoodDark);
            b.AddBox(new Vector3(mid.x, 0.55f, mid.y), midSize, TierConfig.CellWoodDark);
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, c, i / (float)n);
                b.AddBox(new Vector3(p.x, 0.5f, p.y), new Vector3(0.1f, 1.0f, 0.1f), TierConfig.CellWoodWhite);
            }
        }

        /// Hàng rào chắn công trường: cột gỗ mỗi 1 m, hai thanh chắn cam / trắng xen kẽ.
        private static void AddBarrierLine(PaletteMeshBuilder b, Vector2 a, Vector2 c)
        {
            float len = Vector2.Distance(a, c);
            int n = Mathf.Max(1, Mathf.RoundToInt(len));
            Vector2 dir = (c - a).normalized;
            bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, c, i / (float)n);
                b.AddBox(new Vector3(p.x, 0.45f, p.y), new Vector3(0.09f, 0.9f, 0.09f), TierConfig.CellWoodDark);
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Vector2.Lerp(a, c, (i + 0.5f) / n);
                float seg = len / n - 0.1f;
                Vector3 size = alongX ? new Vector3(seg, 0.12f, 0.04f) : new Vector3(0.04f, 0.12f, seg);
                b.AddBox(new Vector3(p.x, 0.78f, p.y), size, (i & 1) == 0 ? Orange : White);
                b.AddBox(new Vector3(p.x, 0.5f, p.y), size, (i & 1) == 0 ? White : Orange);
            }
        }

        private static void AddCone(PaletteMeshBuilder b, float x, float z)
        {
            b.AddBox(new Vector3(x, 0.04f, z), new Vector3(0.34f, 0.08f, 0.34f), TierConfig.CellRoad);
            b.AddBox(new Vector3(x, 0.3f, z), new Vector3(0.22f, 0.45f, 0.22f), Orange);
            b.AddBox(new Vector3(x, 0.32f, z), new Vector3(0.24f, 0.09f, 0.24f), White);
        }
    }
}
