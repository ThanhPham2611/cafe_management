using UnityEngine;
using UnityEngine.Rendering;

namespace Cafe
{
    /// Dựng phòng thuê cấp 2 (nhà ống ~6 x 8 m) hoàn toàn bằng code từ các khối palette, không cần import gì:
    ///  - sàn gạch bông (mỗi ô 0.5 m: nền kem + hoa văn hình thoi + chấm góc), tường kem + chân tường xanh bạc hà,
    ///  - tường sau, 2 tường hông, mặt tiền thấp có khung cửa, mái hiên sọc trên cửa, vỉa hè + lòng đường phía trước,
    ///  - KHÔNG có trần (camera nhìn từ trên xuống). Tường không có collider (không chắn chạm / không ảnh hưởng NavMesh);
    ///    NavMesh bị chặn ở tường vì sàn chỉ tồn tại trong phòng, ở ngưỡng cửa và ở vỉa hè.
    ///  - Collider "Ground" (BoxCollider) cho NavMesh + raycast chạm.
    /// Tường quay mặt về camera tự hạ thấp (RoomWall) để thấy bên trong khi xoay camera 360°.
    public static class EnvironmentBuilder
    {
        private const float Cell = GameBalance.CellSize;

        /// Dựng phòng, trả về root "Room_T2" (con của parent nếu có). mat = GameBootstrap.toonMaterial.
        public static GameObject BuildRoom(Material mat, Transform parent)
        {
            var root = new GameObject("Room_T2");
            if (parent != null) root.transform.SetParent(parent, false);
            Transform rt = root.transform;

            BuildInteriorFloor(rt, mat);
            BuildStreet(rt, mat);
            BuildColliders(rt);
            BuildWalls(rt, mat);
            // Mái hiên bỏ: ở độ cao 2,9 m, camera nhìn chếch xuống thấy nó nằm giữa phòng và che khách.
            // BuildAwning(rt, mat);
            BuildDetails(rt, mat);
            return root;
        }

        // ------------------------------------------------------------------ sàn

        private static void BuildInteriorFloor(Transform root, Material mat)
        {
            Vector3 o = RoomLayout.GridOrigin;
            var b = new PaletteMeshBuilder();
            b.AddUpQuad(0f, 0f, 0.001f, RoomLayout.Width, RoomLayout.Depth, TierConfig.CellGrout);

            float corner = Cell * 0.5f - 0.07f;
            for (int i = 0; i < RoomLayout.GridSize.x; i++)
            {
                for (int j = 0; j < RoomLayout.GridSize.y; j++)
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
            Mesh mesh = b.ToMesh("SM_Room_Floor", true);
            GameObject floor = PaletteBox.MakeFromMesh("Floor", mesh, mat, root, Vector3.zero);
            floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ vỉa hè, lòng đường

        private static void BuildStreet(Transform root, Material mat)
        {
            float zFront = RoomLayout.FrontWallOuterZ;
            float sd = RoomLayout.SidewalkDepth;
            float hw = RoomLayout.SidewalkHalfWidthVisual;
            float swMid = RoomLayout.SidewalkMidZ;

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
            var r = new PaletteMeshBuilder();
            float curbZ = zFront - sd - 0.1f;
            r.AddBox(new Vector3(0f, -0.03f, curbZ), new Vector3(hw * 2f, 0.06f, 0.2f), TierConfig.CellGrout);
            float roadD = 5.2f;
            float roadZ = curbZ - 0.1f - roadD * 0.5f;
            r.AddBox(new Vector3(0f, -0.15f, roadZ), new Vector3(hw * 2f, 0.1f, roadD), TierConfig.CellRoad);
            for (float x = -14f; x <= 14f; x += 3f)
                r.AddBox(new Vector3(x, -0.098f, roadZ), new Vector3(1.4f, 0.01f, 0.12f), TierConfig.CellRoadLine);
            r.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(80f, 0.2f, 80f), 39); // nền xa (rêu), ngay dưới mặt đường
            GameObject street = PaletteBox.MakeFromMesh("Street", r.ToMesh("SM_Room_Street", true), mat, root, Vector3.zero);
            street.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ collider

        private static void BuildColliders(Transform root)
        {
            float zFront = RoomLayout.FrontWallOuterZ;
            // Sàn trong nhà (NavMesh + chạm). Mặt trên ở y = 0.
            AddCollider(root, "Ground", new Vector3(0f, -0.05f, 0f), new Vector3(RoomLayout.Width, 0.1f, RoomLayout.Depth));
            // Ngưỡng cửa nối sàn trong nhà với vỉa hè (chồng lên 2 bên một chút để NavMesh liền mạch)
            AddCollider(root, "Ground_Door", new Vector3(RoomLayout.DoorCenterX, -0.05f, (zFront + RoomLayout.InteriorFrontZ) * 0.5f),
                new Vector3(RoomLayout.DoorHalfWidth * 2f, 0.1f, RoomLayout.WallThickness + 0.1f));
            // Vỉa hè
            AddCollider(root, "Ground_Sidewalk", new Vector3(0f, -0.05f, RoomLayout.SidewalkMidZ),
                new Vector3(RoomLayout.SidewalkHalfWidthCollider * 2f, 0.1f, RoomLayout.SidewalkDepth));
        }

        private static void AddCollider(Transform root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ tường

        private static void BuildWalls(Transform root, Material mat)
        {
            float hw = RoomLayout.Width * 0.5f;
            float hd = RoomLayout.Depth * 0.5f;
            float t = RoomLayout.WallThickness;

            MakeWall(root, mat, "Wall_Back", new Vector3(0f, 0f, hd + t * 0.5f), true, RoomLayout.Width, Vector3.forward, false, false, true);
            MakeWall(root, mat, "Wall_Front", new Vector3(0f, 0f, -(hd + t * 0.5f)), true, RoomLayout.Width, Vector3.back, true, false, false);
            MakeWall(root, mat, "Wall_Left", new Vector3(-(hw + t * 0.5f), 0f, 0f), false, RoomLayout.Depth + 2f * t, Vector3.left, false, true, false);
            MakeWall(root, mat, "Wall_Right", new Vector3(hw + t * 0.5f, 0f, 0f), false, RoomLayout.Depth + 2f * t, Vector3.right, false, true, false);
        }

        /// center: tâm đáy tường (y = 0). alongX: tường chạy dọc trục X (tường trước/sau) hay Z (tường hông).
        private static void MakeWall(Transform root, Material mat, string name, Vector3 center, bool alongX, float length,
                                     Vector3 outward, bool withDoor, bool windows, bool pictures)
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

            // đoạn tường (theo trục s); cửa cắt tường trước làm đôi
            float half = length * 0.5f;
            float dc = RoomLayout.DoorCenterX, dh = RoomLayout.DoorHalfWidth;
            if (withDoor)
            {
                AddWallSegment(lower, upper, alongX, -half, dc - dh, t, lowH, capH, upH);
                AddWallSegment(lower, upper, alongX, dc + dh, half, t, lowH, capH, upH);

                // lintel trên cửa + nẹp gỗ + trụ khung cửa
                float lintelH = RoomLayout.WallHeight - RoomLayout.DoorHeight;
                upper.AddBox(Pos(alongX, dc, RoomLayout.DoorHeight + lintelH * 0.5f - upBase), Size(alongX, dh * 2f, lintelH, t), TierConfig.CellWallCream);
                upper.AddBox(Pos(alongX, dc, RoomLayout.DoorHeight - 0.06f - upBase), Size(alongX, dh * 2f + 0.2f, 0.12f, t + 0.06f), TierConfig.CellWoodDark);
                for (int side = -1; side <= 1; side += 2)
                {
                    float s = dc + side * (dh + 0.04f);
                    lower.AddBox(Pos(alongX, s, upBase * 0.5f), Size(alongX, 0.08f, upBase, t + 0.06f), TierConfig.CellWoodDark);
                    float postH = RoomLayout.DoorHeight - upBase;
                    upper.AddBox(Pos(alongX, s, postH * 0.5f), Size(alongX, 0.08f, postH, t + 0.06f), TierConfig.CellWoodDark);
                }
            }
            else
            {
                AddWallSegment(lower, upper, alongX, -half, half, t, lowH, capH, upH);
            }

            if (windows)
            {
                AddWindow(upper, alongX, -1.5f, 1.85f - upBase, inward, t);
                AddWindow(upper, alongX, 1.5f, 1.85f - upBase, inward, t);
            }
            if (pictures)
            {
                AddPicture(upper, alongX, -2.2f, 2.3f - upBase, inward, t, 13);
                AddPicture(upper, alongX, 2.2f, 2.3f - upBase, inward, t, 37);
            }

            GameObject lo = PaletteBox.MakeFromMesh("Lower", lower.ToMesh("SM_" + name + "_Lower", true), mat, go.transform, Vector3.zero);
            GameObject up = PaletteBox.MakeFromMesh("Upper", upper.ToMesh("SM_" + name + "_Upper", true), mat, go.transform, new Vector3(0f, upBase, 0f));
            lo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            up.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            wall.Upper = up.transform;
        }

        private static void AddWallSegment(PaletteMeshBuilder lower, PaletteMeshBuilder upper, bool alongX, float a, float b,
                                           float t, float lowH, float capH, float upH)
        {
            float len = b - a;
            if (len <= 0.001f) return;
            float mid = (a + b) * 0.5f;
            lower.AddBox(Pos(alongX, mid, lowH * 0.5f), Size(alongX, len, lowH, t), TierConfig.CellWainscot);
            lower.AddBox(Pos(alongX, mid, lowH + capH * 0.5f), Size(alongX, len, capH, t + 0.04f), TierConfig.CellWoodDark);
            upper.AddBox(Pos(alongX, mid, upH * 0.5f), Size(alongX, len, upH, t), TierConfig.CellWallCream);
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

        // ------------------------------------------------------------------ mái hiên + chi tiết

        private static void BuildAwning(Transform root, Material mat)
        {
            // Mái hiên sọc đỏ-trắng chìa ra vỉa hè ngay trên cửa (nhỏ và cao để không che bên trong khi nhìn từ camera)
            var b = new PaletteMeshBuilder();
            const int stripes = 6;
            float width = 2.7f;
            float sw = width / stripes;
            float depth = 0.6f;
            float y = 2.9f;
            float zc = RoomLayout.FrontWallOuterZ - depth * 0.5f;
            for (int i = 0; i < stripes; i++)
            {
                int swatch = (i & 1) == 0 ? TierConfig.CellAwningRed : TierConfig.CellAwningWhite;
                float x = RoomLayout.DoorCenterX - width * 0.5f + (i + 0.5f) * sw;
                b.AddBox(new Vector3(x, y, zc), new Vector3(sw, 0.05f, depth), swatch);
                b.AddBox(new Vector3(x, y - 0.09f, RoomLayout.FrontWallOuterZ - depth + 0.02f), new Vector3(sw, 0.18f, 0.04f), swatch);
            }
            // hai thanh đỡ
            b.AddBox(new Vector3(RoomLayout.DoorCenterX - width * 0.5f + 0.03f, y - 0.1f, zc), new Vector3(0.04f, 0.04f, depth), TierConfig.CellWoodDark);
            b.AddBox(new Vector3(RoomLayout.DoorCenterX + width * 0.5f - 0.03f, y - 0.1f, zc), new Vector3(0.04f, 0.04f, depth), TierConfig.CellWoodDark);
            PaletteBox.MakeFromMesh("Awning", b.ToMesh("SM_Room_Awning", true), mat, root, Vector3.zero);
        }

        private static void BuildDetails(Transform root, Material mat)
        {
            // Thảm chùi chân ngay trong cửa (chỉ để nhìn, không collider)
            var b = new PaletteMeshBuilder();
            b.AddBox(new Vector3(RoomLayout.DoorCenterX, 0.009f, RoomLayout.InteriorFrontZ + 0.35f), new Vector3(1.1f, 0.012f, 0.55f), TierConfig.CellDoorMat);
            // ngưỡng cửa gỗ
            b.AddBox(new Vector3(RoomLayout.DoorCenterX, 0.004f, (RoomLayout.FrontWallOuterZ + RoomLayout.InteriorFrontZ) * 0.5f),
                new Vector3(RoomLayout.DoorHalfWidth * 2f, 0.01f, RoomLayout.WallThickness), TierConfig.CellWoodMid);
            GameObject d = PaletteBox.MakeFromMesh("Details", b.ToMesh("SM_Room_Details", true), mat, root, Vector3.zero);
            d.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
