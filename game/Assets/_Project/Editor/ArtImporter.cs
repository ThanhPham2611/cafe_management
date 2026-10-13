using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Đưa asset từ art_source/export vào Unity theo art_bible §8.1:
    /// copy FBX + palette, tạo M_Toon_Palette, prefab PF_* (BoxCollider), SO_Furniture_*, SO_Recipe_*.
    public static class ArtImporter
    {
        public const string ArtRoot = "Assets/_Project/Art";
        public const string MaterialPath = ArtRoot + "/Materials/M_Toon_Palette.mat";
        public const string PalettePath = ArtRoot + "/Textures/T_Palette_256.png";
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string FurnitureDataDir = "Assets/_Project/Data/Furniture";
        const string RecipeDataDir = "Assets/_Project/Data/Recipes";
        const string ShopIconDir = ArtRoot + "/UI/Shop"; // UiSpriteImporter biến PNG dưới Art/UI thành Sprite

        /// Danh mục Tier 1. Giá theo economy_monetization.md §3.3 / §3.6, footprint theo gen_furniture.py.
        struct Item
        {
            public string fbx, folder;
            public FurnitureKind kind;
            public string displayName;
            public int price, comfort, style;
            public Vector2Int footprint;
            public float seatHeight;
            // Cửa hàng (GDD §2.1, economy §3.3/§3.6)
            public bool hidden;          // không bán (xe đẩy, ly)
            public int priceStep, maxCount, attract;
            public string machineId;     // máy móc cấp 3 (MachineIds); rỗng = không phải máy
            // Cấp 2
            public int minTier;          // 0 hoặc 1 = có từ vỉa hè; 2 thuê mặt bằng; 3 nhà hàng
            public bool overhead;        // treo trên cao
            public float hangY;          // độ cao đáy model khi treo (m)
        }

        static readonly Item[] Catalog =
        {
            new Item { fbx = "SM_Stool_Plastic_Red", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế nhựa đỏ", price = 200, priceStep = 75, comfort = 1, style = 0, footprint = new Vector2Int(1, 1), seatHeight = 0.27f },
            new Item { fbx = "SM_Stool_Plastic_Blue", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế nhựa xanh dương", price = 200, priceStep = 75, comfort = 1, style = 0, footprint = new Vector2Int(1, 1), seatHeight = 0.27f },
            new Item { fbx = "SM_Stool_Plastic_Green", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế nhựa xanh lá", price = 200, priceStep = 75, comfort = 1, style = 0, footprint = new Vector2Int(1, 1), seatHeight = 0.27f },
            new Item { fbx = "SM_Table_Low_Blue_2x2", folder = "Furniture", kind = FurnitureKind.Table, displayName = "Bàn thấp nhựa xanh", price = 300, comfort = 1, style = 1, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Table_Low_Inox_2x2", folder = "Furniture", kind = FurnitureKind.Table, displayName = "Bàn thấp inox", price = 450, comfort = 1, style = 1, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Cart_Coffee_3x2", folder = "Counters", kind = FurnitureKind.BrewStation, displayName = "Xe đẩy cà phê", hidden = true, price = 0, comfort = 0, style = 2, footprint = new Vector2Int(3, 2) },
            new Item { fbx = "SM_Umbrella_Street_4x4", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Dù che nắng", price = 1200, comfort = 2, style = 2, footprint = new Vector2Int(4, 4) },
            new Item { fbx = "SM_Plant_Pot_Small", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Chậu cây nhỏ", price = 250, comfort = 0, style = 2, attract = 5, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Plant_Areca_Tall", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Cây cau tiểu trâm", price = 400, comfort = 0, style = 3, attract = 5, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Board_Menu_A", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Bảng menu đứng", price = 600, comfort = 0, style = 2, attract = 12, maxCount = 1, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Stool_Plastic_Yellow", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế nhựa vàng", price = 200, priceStep = 75, comfort = 1, style = 0, footprint = new Vector2Int(1, 1), seatHeight = 0.27f },
            new Item { fbx = "SM_Jar_Tea_Big", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Bình trà đá to", price = 800, comfort = 1, style = 1, attract = 3, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Fan_Stand", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Quạt đứng", price = 600, comfort = 3, style = 0, footprint = new Vector2Int(1, 1) },
            // ---- Cấp 2: thuê mặt bằng (docs/phase2_spec.md). Kích thước/chiều cao theo báo cáo Tech Artist.
            new Item { fbx = "SM_Counter_Bar_4x2", folder = "Counters", kind = FurnitureKind.BrewStation, displayName = "Quầy bar", hidden = true, minTier = 2, price = 0, style = 4, footprint = new Vector2Int(4, 2) },
            new Item { fbx = "SM_Sink_Counter", folder = "Counters", kind = FurnitureKind.Decor, displayName = "Bồn rửa", hidden = true, minTier = 2, price = 0, footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Shelf_Cups", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Kệ ly", minTier = 2, price = 800, style = 3, footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Table_Wood_2Seat", folder = "Furniture", kind = FurnitureKind.Table, displayName = "Bàn gỗ", minTier = 2, price = 700, priceStep = 120, comfort = 2, style = 3, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Chair_Wood", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế gỗ", minTier = 2, price = 300, comfort = 3, style = 2, footprint = new Vector2Int(1, 1), seatHeight = 0.45f },
            new Item { fbx = "SM_Fan_Ceiling", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Quạt trần", minTier = 2, price = 900, comfort = 4, style = 1, footprint = new Vector2Int(1, 1), overhead = true, hangY = 2.485f },
            new Item { fbx = "SM_Lamp_Pendant", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Đèn thả", minTier = 2, price = 500, style = 4, attract = 3, footprint = new Vector2Int(1, 1), overhead = true, hangY = 1.88f },
            new Item { fbx = "SM_Plant_Pothos_Hanging", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Chậu trầu bà treo", minTier = 2, price = 450, style = 3, attract = 5, footprint = new Vector2Int(1, 1), overhead = true, hangY = 1.99f },
            new Item { fbx = "SM_Door_Rolling_Shutter", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Cửa cuốn", hidden = true, minTier = 2, price = 0, footprint = new Vector2Int(3, 1) },
            new Item { fbx = "SM_Trash_Bin", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Thùng rác", minTier = 2, price = 150, footprint = new Vector2Int(1, 1) },
            // Bộ trang trí kiểu Việt (cấp 2; art_source/blend/furniture_tier2b.blend). Chỉ số: Đà Lạt / Sài Gòn xưa / Hội An.
            new Item { fbx = "SM_Chair_Rattan", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế mây", minTier = 2, price = 450, comfort = 4, style = 4, footprint = new Vector2Int(1, 1), seatHeight = 0.45f },
            new Item { fbx = "SM_Table_Round_Tile", folder = "Furniture", kind = FurnitureKind.Table, displayName = "Bàn tròn gạch bông", minTier = 2, price = 900, comfort = 2, style = 5, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Plant_Monstera", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Cây trầu bà lá xẻ", minTier = 2, price = 550, style = 4, attract = 5, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Lamp_Floor", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Đèn cây", minTier = 2, price = 650, comfort = 1, style = 4, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Bookshelf_Low", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Kệ sách thấp", minTier = 2, price = 1200, style = 5, footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Radio_Stand", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Radio cổ", minTier = 2, price = 700, style = 4, attract = 3, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Lantern_Hoian", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Đèn lồng Hội An", minTier = 2, price = 650, style = 5, attract = 3, footprint = new Vector2Int(1, 1), overhead = true, hangY = 1.86f },
            // Nội thất nhà hàng (cấp 3; art_source/blend/furniture_tier3.blend). Giá theo economy §3.6 (ghế 1.500–8.000, bàn 3.000–15.000, trang trí 3.000–30.000, máy lạnh 12.000–40.000).
            new Item { fbx = "SM_Chair_Leather", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế da", minTier = 3, price = 1800, comfort = 6, style = 3, footprint = new Vector2Int(1, 1), seatHeight = 0.46f },
            new Item { fbx = "SM_Armchair_Velvet", folder = "Furniture", kind = FurnitureKind.Seat, displayName = "Ghế bành nhung", minTier = 3, price = 3500, comfort = 8, style = 5, footprint = new Vector2Int(2, 2), seatHeight = 0.48f },
            new Item { fbx = "SM_Table_Marble_Round", folder = "Furniture", kind = FurnitureKind.Table, displayName = "Bàn đá cẩm thạch", minTier = 3, price = 4500, comfort = 2, style = 7, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Aircon_Standing", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Máy lạnh tủ đứng", minTier = 3, price = 14000, comfort = 10, style = 1, maxCount = 4, footprint = new Vector2Int(1, 1) },
            new Item { fbx = "SM_Chandelier_Brass", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Đèn chùm đồng", minTier = 3, price = 9000, style = 12, attract = 4, maxCount = 4, footprint = new Vector2Int(2, 2), overhead = true, hangY = 1.79f },
            new Item { fbx = "SM_Plant_Fig_Large", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Cây bàng lá đàn", minTier = 3, price = 3200, style = 8, attract = 6, maxCount = 6, footprint = new Vector2Int(2, 2) },
            new Item { fbx = "SM_Easel_Art", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Giá vẽ tranh Hội An", minTier = 3, price = 6500, style = 10, attract = 3, maxCount = 3, footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Piano_Upright", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Đàn piano", minTier = 3, price = 28000, style = 15, attract = 8, maxCount = 1, footprint = new Vector2Int(3, 1) },
            // Máy móc (economy §3.5): đặt trong quán mới có tác dụng (MachineRules). Mỗi loại tối đa 1.
            new Item { fbx = "SM_Machine_Grinder", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Máy xay 2 họng", minTier = 3, price = 25000, style = 4, maxCount = 1, machineId = "grinder", footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Machine_ColdBrew", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Máy cold brew", minTier = 3, price = 60000, style = 6, maxCount = 1, machineId = "cold_brew", footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Machine_Espresso", folder = "Furniture", kind = FurnitureKind.Decor, displayName = "Máy espresso", minTier = 3, price = 150000, style = 10, maxCount = 1, machineId = "espresso", footprint = new Vector2Int(2, 1) },
            new Item { fbx = "SM_Cup_Phin_SuaDa", folder = "Props", kind = FurnitureKind.Decor, displayName = "Ly cà phê", hidden = true, price = 0, comfort = 0, style = 0, footprint = new Vector2Int(1, 1) },
        };

        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static string ExportDir => Path.Combine(RepoRoot, "art_source", "export");

        public static void ImportAll()
        {
            CopyIn(Path.Combine(RepoRoot, "art_source", "palette", "T_Palette_256.png"), PalettePath);
            foreach (var it in Catalog)
            {
                CopyIn(Path.Combine(ExportDir, it.fbx + ".fbx"), $"{ArtRoot}/Models/{it.folder}/{it.fbx}.fbx");
                var icon = Path.Combine(ExportDir, "icons", "ICON_" + it.fbx.Substring(3) + ".png");
                if (File.Exists(icon)) CopyIn(icon, $"{ShopIconDir}/ICON_{it.fbx.Substring(3)}.png");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var mat = EnsureMaterial();
            foreach (var it in Catalog)
            {
                var prefab = BuildPrefab(it, mat);
                if (prefab != null) EnsureFurnitureDef(it, prefab);
            }
            EnsureRecipes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cafe] Import xong {Catalog.Length} model.");
        }

        static void CopyIn(string src, string assetPath)
        {
            if (!File.Exists(src))
            {
                Debug.LogError($"[Cafe] Thiếu file nguồn: {src} (chạy tools/gen_furniture.py trước)");
                return;
            }
            var dst = Path.Combine(RepoRoot, "game", assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        public static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("Cafe/Toon_Palette");
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                mat = new Material(shader) { name = "M_Toon_Palette" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject BuildPrefab(Item it, Material mat)
        {
            var modelPath = $"{ArtRoot}/Models/{it.folder}/{it.fbx}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[Cafe] Không load được {modelPath}");
                return null;
            }

            var id = it.fbx.Substring(3); // bỏ "SM_"
            var root = new GameObject("PF_" + id);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            if (it.hangY > 0f) inst.transform.localPosition = new Vector3(0f, it.hangY, 0f); // đồ treo: nâng đáy model lên

            var bounds = new Bounds();
            bool first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = bounds.size;

            Directory.CreateDirectory(PrefabDir);
            var path = $"{PrefabDir}/PF_{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void EnsureFurnitureDef(Item it, GameObject prefab)
        {
            var id = it.fbx.Substring(3);
            var path = $"{FurnitureDataDir}/SO_Furniture_{id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<FurnitureDef>(path);
            if (def == null)
            {
                Directory.CreateDirectory(FurnitureDataDir);
                def = ScriptableObject.CreateInstance<FurnitureDef>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.id = id;
            def.displayName = it.displayName;
            def.prefab = prefab;
            def.footprint = it.footprint;
            def.price = it.price;
            def.kind = it.kind;
            def.comfort = it.comfort;
            def.style = it.style;
            def.seatHeight = it.seatHeight;
            def.shopVisible = !it.hidden;
            def.minTier = it.minTier > 0 ? it.minTier : 1;
            def.overhead = it.overhead;
            def.hangY = it.hangY;
            def.priceStep = it.priceStep;
            def.maxCount = it.maxCount;
            def.attract = it.attract;
            def.machineId = it.machineId;
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShopIconDir}/ICON_{id}.png");
            EditorUtility.SetDirty(def);
        }

        /// 4 món Tier 1 (economy_monetization.md §2.1, GDD §4.1).
        static void EnsureRecipes()
        {
            Recipe("tra_da", "Trà đá", 30, 5, 2f, 0, 1.0f, BrewStep.Tra, BrewStep.Da);
            Recipe("ca_phe_den", "Cà phê đen", 80, 25, 4f, 0, 1.2f, BrewStep.Phin, BrewStep.Da);
            Recipe("ca_phe_sua", "Cà phê sữa", 100, 35, 5f, 0, 1.6f, BrewStep.Phin, BrewStep.SuaDac, BrewStep.Da);
            Recipe("bac_xiu", "Bạc xỉu", 120, 45, 6f, 3, 1.0f, BrewStep.SuaDac, BrewStep.SuaTuoi, BrewStep.Phin, BrewStep.Da);
            RecipeImporter.EnsureTier2Recipes(); // Trà chanh + 3 món ẩn (Phase 2)
            RecipeImporter.EnsureTier3Recipes(); // Bạc xỉu kem muối, cold brew, robusta (Phase 3)
        }

        static void Recipe(string id, string name, int price, int cost, float seconds, int unlockDay,
                           float weight, params BrewStep[] steps)
        {
            var path = $"{RecipeDataDir}/SO_Recipe_{id}.asset";
            var r = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
            if (r == null)
            {
                Directory.CreateDirectory(RecipeDataDir);
                r = ScriptableObject.CreateInstance<RecipeDef>();
                AssetDatabase.CreateAsset(r, path);
            }
            r.id = id;
            r.displayName = name;
            r.price = price;
            r.ingredientCost = cost;
            r.brewSeconds = seconds;
            r.steps = steps;
            r.unlockTier = 1;
            r.unlockDay = unlockDay;
            r.orderWeight = weight;
            EditorUtility.SetDirty(r);
        }

        public static FurnitureDef[] LoadCatalog() => LoadAll<FurnitureDef>(FurnitureDataDir);
        public static RecipeDef[] LoadRecipes() => LoadAll<RecipeDef>(RecipeDataDir);

        static T[] LoadAll<T>(string dir) where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { dir });
            var result = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return result;
        }
    }

    /// Thông số import cho model và palette trong _Project/Art (art_bible §3, §8.1).
    public class CafeAssetPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/_Project/Art/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.isReadable = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.generateSecondaryUV = false;
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith("T_Palette_256.png")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // tránh lem màu giữa các ô
            ti.alphaSource = TextureImporterAlphaSource.None;
        }
    }
}
