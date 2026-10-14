using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cafe
{
    /// Dựng phòng của quán cấp 2 (nhà ống ~6 x 8 m) và cấp 3 (nhà hàng ~10 x 12 m) hoàn toàn bằng code từ các khối palette, không cần import gì:
    ///  - sàn gạch bông (mỗi ô 0.5 m: nền kem + hoa văn hình thoi + chấm góc), tường kem + chân tường xanh bạc hà,
    ///  - tường sau, 2 tường hông, mặt tiền thấp có khung cửa, vỉa hè + lòng đường phía trước,
    ///  - KHÔNG có trần (camera nhìn từ trên xuống). Tường không có collider (không chắn chạm / không ảnh hưởng NavMesh);
    ///    NavMesh bị chặn ở tường vì sàn chỉ tồn tại trong phòng, ở ngưỡng cửa và ở vỉa hè.
    ///  - Collider "Ground" (BoxCollider) cho NavMesh + raycast chạm.
    /// Tường quay mặt về camera tự hạ thấp (RoomWall) để thấy bên trong khi xoay camera 360°.
    /// Cấp 3 có thêm các khu phụ hai bên sảnh (ZoneEnvironment): khu đã mở có sàn + tường riêng và lỗ mở trên tường sảnh,
    /// khu chưa mở là bãi đất rào chờ xây.
    public static class EnvironmentBuilder
    {
        private const float Cell = GameBalance.CellSize;

        /// Màu của một bức tường: chân tường, nẹp, thân tường.
        internal struct WallStyle
        {
            public int lower, cap, upper;
        }

        internal static readonly WallStyle HallStyle = new WallStyle
        {
            lower = TierConfig.CellWainscot, cap = TierConfig.CellWoodDark, upper = TierConfig.CellWallCream
        };

        /// Dựng phòng theo RoomSpec của cấp, trả về root "Room_T{cấp}" (con của parent nếu có). mat = GameBootstrap.toonMaterial.
        /// zoneMask: các khu phụ đã mở (bit ZoneInfo.Bit), chỉ có tác dụng khi phòng có khu (cấp 3).
        public static GameObject BuildRoom(Material mat, Transform parent, RoomSpec r, int zoneMask = 0)
        {
            var root = new GameObject("Room_T" + r.Tier);
            if (parent != null) root.transform.SetParent(parent, false);
            Transform rt = root.transform;

            BuildInteriorFloor(rt, mat, r);
            BuildStreet(rt, mat, r);
            BuildColliders(rt, r);
            BuildWalls(rt, mat, r);
            RefreshZones(rt, mat, r, zoneMask);
            BuildDetails(rt, mat, r);
            return root;
        }

        /// Dựng (hoặc dựng lại) phần phụ thuộc các khu đang mở: hai tường hông của sảnh (có lỗ mở sang khu đã mở), bãi đất của khu
        /// chưa mở, sàn + tường + collider của khu đã mở (chỉ dựng một lần). Gọi lúc mở thêm khu; mọi thứ bị phá ở đây đều
        /// KHÔNG có collider nên NavMesh có thể bake lại ngay trong cùng frame.
        public static void RefreshZones(Transform room, Material mat, RoomSpec r, int zoneMask)
        {
            DestroyChild(room, "Wall_Left");
            DestroyChild(room, "Wall_Right");
            for (int i = 0; i < ZoneInfo.Buyable.Length; i++) DestroyChild(room, "Lot_" + ZoneInfo.Buyable[i]);
            BuildSideWalls(room, mat, r, zoneMask);
            if (!r.HasZones) return;
            for (int i = 0; i < ZoneInfo.Buyable.Length; i++)
            {
                ZoneId z = ZoneInfo.Buyable[i];
                if ((zoneMask & ZoneInfo.Bit(z)) != 0)
                {
                    if (room.Find("Zone_" + z) == null) ZoneEnvironment.BuildZone(room, mat, r, z);
                }
                else
                {
                    ZoneEnvironment.BuildLot(room, mat, r, z);
                }
            }
        }

        private static void DestroyChild(Transform room, string name)
        {
            Transform t = room.Find(name);
            if (t == null) return;
            t.name = "_destroyed"; // Destroy chỉ chạy cuối khung: đổi tên để Find lần sau (cùng khung) không thấy bản cũ
            t.gameObject.SetActive(false);
            Object.Destroy(t.gameObject);
        }

        // ------------------------------------------------------------------ sàn

        private static void BuildInteriorFloor(Transform root, Material mat, RoomSpec r)
        {
            var b = new PaletteMeshBuilder();
            b.AddUpQuad(0f, 0f, 0.001f, r.Width, r.Depth, TierConfig.CellGrout);
            if (r.Tier >= 3) AddParquet(b, r);
            else AddEncausticTiles(b, r);
            Mesh mesh = b.ToMesh("SM_Room_Floor", true);
            GameObject floor = PaletteBox.MakeFromMesh("Floor", mesh, mat, root, Vector3.zero);
            floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// Cấp 2: gạch bông 0,5 m (nền kem + hoa văn hình thoi + chấm góc).
        private static void AddEncausticTiles(PaletteMeshBuilder b, RoomSpec r)
        {
            Vector3 o = r.GridOrigin;
            float corner = Cell * 0.5f - 0.07f;
            for (int i = 0; i < r.GridSize.x; i++)
            {
                for (int j = 0; j < r.GridSize.y; j++)
                {
                    float cx = o.x + (i + 0.5f) * Cell;
                    float cz = o.z + (j + 0.5f) * Cell;
                    bool alt = ((i + j) & 1) == 1;
                    int accent = alt ? TierConfig.CellTileBlue : TierConfig.CellTileRed;
                    int dots = alt ? TierConfig.CellTileRed : TierConfig.CellTileBlue;

                    b.AddUpQuad(cx, cz, 0.003f, Cell - 0.03f, Cell - 0.03f, TierConfig.CellTileCream);
                    b.AddUpDiamond(cx, cz, 0.005f, 0.19f, accent);
                    b.AddUpDiamond(cx, cz, 0.007f, 0.085f, TierConfig.CellTileYellow);
                    b.AddUpQuad(cx - corner, cz - corner, 0.005f, 0.08f, 0.08f, dots);
                    b.AddUpQuad(cx + corner, cz - corner, 0.005f, 0.08f, 0.08f, dots);
                    b.AddUpQuad(cx - corner, cz + corner, 0.005f, 0.08f, 0.08f, dots);
                    b.AddUpQuad(cx + corner, cz + corner, 0.005f, 0.08f, 0.08f, dots);
                }
            }
        }

        /// Cấp 3: sàn gỗ ghép thanh sáng màu (thanh rộng 25 cm, dài 2 m, lệch hàng) — êm mắt hơn gạch bông để nội thất nổi bật.
        private static void AddParquet(PaletteMeshBuilder b, RoomSpec r)
        {
            const float plank = 0.25f, gap = 0.012f, length = 2f;
            int[] tones = { TierConfig.CellWoodWhite, TierConfig.CellWoodLight, TierConfig.CellWoodWhite, TierConfig.CellCream, TierConfig.CellWoodLight };
            int cols = Mathf.RoundToInt(r.Width / plank);
            float z0 = -r.Depth * 0.5f, z1 = r.Depth * 0.5f;
            for (int i = 0; i < cols; i++)
            {
                float x = -r.Width * 0.5f + (i + 0.5f) * plank;
                float z = z0 - (i % 3) * (length / 3f);
                for (int n = 0; z < z1; n++, z += length)
                {
                    float a = Mathf.Max(z, z0), e = Mathf.Min(z + length, z1);
                    if (e - a < 0.05f) continue;
                    b.AddUpQuad(x, (a + e) * 0.5f, 0.003f, plank - gap, e - a - gap, tones[(i * 7 + n * 3) % tones.Length]);
                }
            }
        }

        // ------------------------------------------------------------------ vỉa hè, lòng đường

        private static void BuildStreet(Transform root, Material mat, RoomSpec r)
        {
            float zFront = r.FrontWallOuterZ;
            float sd = RoomLayout.SidewalkDepth;
            float hw = RoomLayout.SidewalkHalfWidthVisual;
            float swMid = r.SidewalkMidZ;

            // Vỉa hè: nền vữa + tấm lát 1 m
            var s = new PaletteMeshBuilder();
            s.AddUpQuad(0f, swMid, 0.001f, hw * 2f, sd, TierConfig.CellGrout);
            int cols = Mathf.RoundToInt(hw * 2f);
            const int rows = 3;
            float slabD = sd / rows;
            for (int i = 0; i < cols; i++)
            {
                float cx = -hw + (i + 0.5f);
                for (int j = 0; j < rows; j++)
                {
                    float cz = zFront - (j + 0.5f) * slabD;
                    s.AddUpQuad(cx, cz, 0.003f, 0.96f, slabD - 0.04f, TierConfig.CellPavement);
                }
            }
            GameObject sw = PaletteBox.MakeFromMesh("Sidewalk", s.ToMesh("SM_Room_Sidewalk", true), mat, root, Vector3.zero);
            sw.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Bó vỉa, lòng đường (+ vạch), nền xa
            var road = new PaletteMeshBuilder();
            float curbZ = zFront - sd - 0.1f;
            road.AddBox(new Vector3(0f, -0.03f, curbZ), new Vector3(hw * 2f, 0.06f, 0.2f), TierConfig.CellGrout);
            float roadD = 5.2f;
            float roadZ = curbZ - 0.1f - roadD * 0.5f;
            road.AddBox(new Vector3(0f, -0.15f, roadZ), new Vector3(hw * 2f, 0.1f, roadD), TierConfig.CellRoad);
            for (float x = -14f; x <= 14f; x += 3f)
                road.AddBox(new Vector3(x, -0.098f, roadZ), new Vector3(1.4f, 0.01f, 0.12f), TierConfig.CellRoadLine);
            road.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(80f, 0.2f, 80f), 39); // nền xa (rêu), ngay dưới mặt đường
            GameObject street = PaletteBox.MakeFromMesh("Street", road.ToMesh("SM_Room_Street", true), mat, root, Vector3.zero);
            street.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ collider

        private static void BuildColliders(Transform root, RoomSpec r)
        {
            float zFront = r.FrontWallOuterZ;
            // Sàn trong nhà (NavMesh + chạm). Mặt trên ở y = 0. Phòng có khu phụ: sàn sảnh thu vào 0,1 m mỗi bên (tường hông nằm
            // đúng trên đường biên) để giữa sàn sảnh và sàn khu phụ có khe 0,2 m, nối với nhau chỉ qua collider ở lỗ mở.
            float groundW = r.HasZones ? r.Width - 0.2f : r.Width;
            AddCollider(root, "Ground", new Vector3(0f, -0.05f, 0f), new Vector3(groundW, 0.1f, r.Depth));
            // Ngưỡng cửa nối sàn trong nhà với vỉa hè (chồng lên 2 bên một chút để NavMesh liền mạch)
            AddCollider(root, "Ground_Door", new Vector3(RoomLayout.DoorCenterX, -0.05f, (zFront + r.InteriorFrontZ) * 0.5f),
                new Vector3(r.DoorHalfWidth * 2f, 0.1f, RoomLayout.WallThickness + 0.1f));
            // Vỉa hè
            AddCollider(root, "Ground_Sidewalk", new Vector3(0f, -0.05f, r.SidewalkMidZ),
                new Vector3(r.SidewalkHalfWidthCollider * 2f, 0.1f, RoomLayout.SidewalkDepth));
        }

        private static void AddCollider(Transform root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ tường

        private static void BuildWalls(Transform root, Material mat, RoomSpec r)
        {
            float hd = r.Depth * 0.5f;
            float t = RoomLayout.WallThickness;

            MakeWall(root, mat, r, "Wall_Back", new Vector3(0f, 0f, hd + t * 0.5f), true, r.Width, Vector3.forward, null, false, true, HallStyle);
            MakeWall(root, mat, r, "Wall_Front", new Vector3(0f, 0f, -(hd + t * 0.5f)), true, r.Width, Vector3.back,
                new[] { new Vector2(RoomLayout.DoorCenterX, r.DoorHalfWidth) }, false, false, HallStyle);
        }

        /// Lỗ mở trên tường hông sảnh sang khu phụ (toạ độ dọc tường = z): sân vườn ở giữa tường trái, lầu và VIP ở nửa trước / nửa sau tường phải.
        internal const float AnnexOpeningHalf = 1.5f;
        internal const float AnnexOpeningZ = 3f;

        /// Tường hông sảnh. Cấp 3 (có khu phụ) tường nằm đúng trên đường biên giữa sảnh và khu phụ.
        private static void BuildSideWalls(Transform root, Material mat, RoomSpec r, int zoneMask)
        {
            float hw = r.Width * 0.5f;
            float t = RoomLayout.WallThickness;
            float x = r.HasZones ? hw : hw + t * 0.5f;
            float len = r.Depth + 2f * t;

            Vector2[] left = null, right = null;
            if (r.HasZones)
            {
                if ((zoneMask & ZoneInfo.Bit(ZoneId.Garden)) != 0) left = new[] { new Vector2(0f, AnnexOpeningHalf) };
                var rl = new List<Vector2>(2);
                if ((zoneMask & ZoneInfo.Bit(ZoneId.Terrace)) != 0) rl.Add(new Vector2(-AnnexOpeningZ, AnnexOpeningHalf));
                if ((zoneMask & ZoneInfo.Bit(ZoneId.Vip)) != 0) rl.Add(new Vector2(AnnexOpeningZ, AnnexOpeningHalf));
                if (rl.Count > 0) right = rl.ToArray();
            }
            MakeWall(root, mat, r, "Wall_Left", new Vector3(-x, 0f, 0f), false, len, Vector3.left, left, true, false, HallStyle);
            MakeWall(root, mat, r, "Wall_Right", new Vector3(x, 0f, 0f), false, len, Vector3.right, right, true, false, HallStyle);
        }

        /// center: tâm đáy tường (y = 0). alongX: tường chạy dọc trục X (tường trước/sau) hay Z (tường hông).
        /// openings: các lỗ mở (cửa ra vào, lỗ sang khu phụ) dạng (tâm, nửa rộng) theo trục dọc tường; null = tường liền.
        internal static GameObject MakeWall(Transform root, Material mat, RoomSpec r, string name, Vector3 center, bool alongX, float length,
                                            Vector3 outward, IList<Vector2> openings, bool windows, bool pictures, WallStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            var wall = go.AddComponent<RoomWall>();
            wall.Outward = outward;

            float t = RoomLayout.WallThickness;
            float lowH = RoomLayout.WainscotHeight;
            const float capH = 0.06f;
            float upBase = lowH + capH;                       // gốc (pivot) của phần tường trên
            float upH = RoomLayout.WallHeight - upBase;
            Vector3 inward = -outward;

            var lower = new PaletteMeshBuilder();
            var upper = new PaletteMeshBuilder();

            // đoạn tường (theo trục s) giữa các lỗ mở; mỗi lỗ có lintel + nẹp gỗ + trụ khung
            float half = length * 0.5f;
            float cursor = -half;
            if (openings != null && openings.Count > 0)
            {
                var sorted = new List<Vector2>(openings);
                sorted.Sort((a, b) => a.x.CompareTo(b.x));
                for (int k = 0; k < sorted.Count; k++)
                {
                    float oc = sorted[k].x, oh = sorted[k].y;
                    AddWallSegment(lower, upper, alongX, cursor, oc - oh, t, lowH, capH, upH, style);
                    cursor = oc + oh;

                    float lintelH = RoomLayout.WallHeight - RoomLayout.DoorHeight;
                    upper.AddBox(Pos(alongX, oc, RoomLayout.DoorHeight + lintelH * 0.5f - upBase), Size(alongX, oh * 2f, lintelH, t), style.upper);
                    upper.AddBox(Pos(alongX, oc, RoomLayout.DoorHeight - 0.06f - upBase), Size(alongX, oh * 2f + 0.2f, 0.12f, t + 0.06f), TierConfig.CellWoodDark);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float s = oc + side * (oh + 0.04f);
                        lower.AddBox(Pos(alongX, s, upBase * 0.5f), Size(alongX, 0.08f, upBase, t + 0.06f), TierConfig.CellWoodDark);
                        float postH = RoomLayout.DoorHeight - upBase;
                        upper.AddBox(Pos(alongX, s, postH * 0.5f), Size(alongX, 0.08f, postH, t + 0.06f), TierConfig.CellWoodDark);
                    }
                }
            }
            AddWallSegment(lower, upper, alongX, cursor, half, t, lowH, capH, upH, style);

            if (windows)
            {
                foreach (float s in Spots(r, length, 3.0f, 1.5f))
                {
                    if (NearOpening(s, openings, 0.95f)) continue; // cửa sổ không đè lên lỗ mở
                    AddWindow(upper, alongX, s, 1.85f - upBase, inward, t);
                }
            }
            if (pictures)
            {
                int i = 0;
                foreach (float s in Spots(r, length, 3.4f, 2.2f))
                    AddPicture(upper, alongX, s, 2.3f - upBase, inward, t, (i++ & 1) == 0 ? 13 : 37);
            }

            GameObject lo = PaletteBox.MakeFromMesh("Lower", lower.ToMesh("SM_" + name + "_Lower", true), mat, go.transform, Vector3.zero);
            GameObject up = PaletteBox.MakeFromMesh("Upper", upper.ToMesh("SM_" + name + "_Upper", true), mat, go.transform, new Vector3(0f, upBase, 0f));
            lo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            up.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            wall.Upper = up.transform;
            return go;
        }

        private static bool NearOpening(float s, IList<Vector2> openings, float margin)
        {
            if (openings == null) return false;
            for (int i = 0; i < openings.Count; i++)
                if (Mathf.Abs(s - openings[i].x) < openings[i].y + margin) return true;
            return false;
        }

        private static void AddWallSegment(PaletteMeshBuilder lower, PaletteMeshBuilder upper, bool alongX, float a, float b,
                                           float t, float lowH, float capH, float upH, WallStyle style)
        {
            float len = b - a;
            if (len <= 0.001f) return;
            float mid = (a + b) * 0.5f;
            lower.AddBox(Pos(alongX, mid, lowH * 0.5f), Size(alongX, len, lowH, t), style.lower);
            lower.AddBox(Pos(alongX, mid, lowH + capH * 0.5f), Size(alongX, len, capH, t + 0.04f), style.cap);
            upper.AddBox(Pos(alongX, mid, upH * 0.5f), Size(alongX, len, upH, t), style.upper);
        }

        /// Cửa sổ ở mặt trong tường: khung gỗ + kính (ô bạc hà) + chấn song.
        private static void AddWindow(PaletteMeshBuilder upper, bool alongX, float s, float yLocal, Vector3 inward, float t)
        {
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.03f), Size(alongX, 1.5f, 1.3f, 0.06f), TierConfig.CellWoodMid);
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.035f), Size(alongX, 1.3f, 1.1f, 0.07f), TierConfig.CellWainscot);
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.045f), Size(alongX, 0.05f, 1.1f, 0.09f), TierConfig.CellWoodMid);
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.045f), Size(alongX, 1.3f, 0.05f, 0.09f), TierConfig.CellWoodMid);
        }

        /// Tranh nhỏ treo trên tường (khung gỗ tối + ruột màu).
        private static void AddPicture(PaletteMeshBuilder upper, bool alongX, float s, float yLocal, Vector3 inward, float t, int swatch)
        {
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.02f), Size(alongX, 0.56f, 0.44f, 0.04f), TierConfig.CellWoodDark);
            upper.AddBox(Item(alongX, s, yLocal, inward, t * 0.5f + 0.025f), Size(alongX, 0.44f, 0.32f, 0.05f), swatch);
        }

        /// Vị trí chi tiết dọc tường (cửa sổ, tranh). Cấp 2: hai điểm ±fixedHalf; cấp 3: chia đều theo khoảng spacing.
        private static IEnumerable<float> Spots(RoomSpec r, float length, float spacing, float fixedHalf)
        {
            if (r.Tier < 3)
            {
                yield return -fixedHalf;
                yield return fixedHalf;
                yield break;
            }
            int n = Mathf.Max(2, Mathf.FloorToInt(length / spacing));
            for (int i = 0; i < n; i++) yield return (i + 0.5f) * length / n - length * 0.5f;
        }

        /// Vị trí trong toạ độ local của tường: s dọc theo tường, y độ cao.
        private static Vector3 Pos(bool alongX, float s, float y)
        {
            return alongX ? new Vector3(s, y, 0f) : new Vector3(0f, y, s);
        }

        /// Kích thước: len dọc theo tường, h cao, thick theo pháp tuyến.
        private static Vector3 Size(bool alongX, float len, float h, float thick)
        {
            return alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
        }

        private static Vector3 Item(bool alongX, float s, float y, Vector3 inward, float offsetFromCenter)
        {
            return Pos(alongX, s, y) + inward * offsetFromCenter;
        }

        // ------------------------------------------------------------------ chi tiết

        private static void BuildDetails(Transform root, Material mat, RoomSpec r)
        {
            // Thảm chùi chân ngay trong cửa (chỉ để nhìn, không collider)
            var b = new PaletteMeshBuilder();
            b.AddBox(new Vector3(RoomLayout.DoorCenterX, 0.009f, r.InteriorFrontZ + 0.35f), new Vector3(1.1f, 0.012f, 0.55f), TierConfig.CellDoorMat);
            // ngưỡng cửa gỗ
            b.AddBox(new Vector3(RoomLayout.DoorCenterX, 0.004f, (r.FrontWallOuterZ + r.InteriorFrontZ) * 0.5f),
                new Vector3(r.DoorHalfWidth * 2f, 0.01f, RoomLayout.WallThickness), TierConfig.CellWoodMid);
            GameObject d = PaletteBox.MakeFromMesh("Details", b.ToMesh("SM_Room_Details", true), mat, root, Vector3.zero);
            d.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
