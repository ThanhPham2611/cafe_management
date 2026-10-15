using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Cafe.EditorTools
{
    /// Dựng scene Prototype_Tier1: vỉa hè + lòng đường + mặt tiền nhà, nắng sáng (art_bible §6.1),
    /// và 1 GameBootstrap với dữ liệu + bố cục mặc định. Gameplay tự dựng phần còn lại lúc chạy.
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Prototype_Tier1.unity";
        const string GeneratedDir = ArtImporter.ArtRoot + "/Models/Generated";

        // Ô palette (art_bible §3)
        const int SidewalkTile = 45, Road = 51, Curb = 47, WallCream = 11, Shutter = 49, RoofTile = 46;

        static readonly Vector3 GridOrigin = new Vector3(-4f, 0f, -2.5f);
        static readonly Vector2Int GridSize = new Vector2Int(16, 10);

        public static void Build()
        {
            var mat = ArtImporter.EnsureMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            CreateCamera();
            CreateStreet(mat);
            CreateEventSystem();
            CreateBootstrap(mat);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Giữ Boot.unity ở index 0 nếu đã dựng (BootSceneBuilder)
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in BootSceneBuilder.BuildScenes()) buildScenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[Cafe] Đã dựng {ScenePath}");
        }

        static void SetupLighting()
        {
            var sun = new GameObject("Directional Light (Golden Hour)").AddComponent<Light>();
            sun.type = LightType.Directional;
            // Nắng sáng ấm nhưng không ám cam: giữ màu palette đúng (đã thử #FFC98A × 1.3 -> vỉa hè thành cam)
            sun.color = new Color32(0xFF, 0xEB, 0xD2, 0xFF);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0xBF, 0xD3, 0xE6, 0xFF);
            RenderSettings.ambientEquatorColor = new Color32(0xE8, 0xC9, 0xA0, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x7A, 0x5A, 0x4A, 0xFF);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xBF, 0xD3, 0xE6, 0xFF);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 60f;
            go.transform.position = new Vector3(0f, 8f, -9f);
            go.transform.LookAt(new Vector3(0f, 0f, 0.3f));
            go.AddComponent<AudioListener>();
        }

        /// Mặt tiền nhà phía sau (+Z), vỉa hè ở giữa, lòng đường phía trước (−Z) gần camera.
        static void CreateStreet(Material mat)
        {
            Directory.CreateDirectory(GeneratedDir);
            var env = new GameObject("Environment");

            var ground = Block("Ground", new Vector3(12f, 0.1f, 7f), new Vector3(0f, -0.05f, 0.5f), SidewalkTile, mat, env);
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;

            Block("Curb", new Vector3(12f, 0.12f, 0.25f), new Vector3(0f, -0.04f, -3.1f), Curb, mat, env);
            Block("Road", new Vector3(16f, 0.1f, 5f), new Vector3(0f, -0.17f, -5.7f), Road, mat, env);
            Block("Wall_Shophouse", new Vector3(12f, 3.4f, 0.3f), new Vector3(0f, 1.7f, 4.15f), WallCream, mat, env);
            Block("Shutter_Door", new Vector3(3.2f, 2.4f, 0.06f), new Vector3(-2.5f, 1.2f, 3.98f), Shutter, mat, env);
            Block("Awning_Roof", new Vector3(12.4f, 0.12f, 0.9f), new Vector3(0f, 3.0f, 3.7f), RoofTile, mat, env);

            foreach (Transform t in env.transform) t.gameObject.isStatic = true;
        }

        static GameObject Block(string name, Vector3 size, Vector3 center, int swatch, Material mat, GameObject parent)
        {
            var meshPath = $"{GeneratedDir}/SM_Env_{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = PaletteMesh.Box(size, swatch);
                mesh.name = "SM_Env_" + name;
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else
            {
                var fresh = PaletteMesh.Box(size, swatch);
                EditorUtility.CopySerialized(fresh, mesh);
                mesh.name = "SM_Env_" + name;
                Object.DestroyImmediate(fresh);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static void CreateBootstrap(Material mat)
        {
            var go = new GameObject("GameBootstrap");
            var boot = go.AddComponent<GameBootstrap>();
            boot.catalog = ArtImporter.LoadCatalog();
            boot.recipes = ArtImporter.LoadRecipes();
            boot.toonMaterial = mat;
            boot.customerPrefabs = CharacterImporter.LoadPrefabs();
            boot.ownerPrefabs = CharacterImporter.LoadOwnerPrefabs();
            boot.staffPrefabs = CharacterImporter.LoadStaffPrefabs();
            boot.regularsJson = ImportStoryJson("regulars.json");
            boot.barksJson = ImportStoryJson("barks.json");
            boot.uiTheme = UiSpriteImporter.EnsureTheme();
            boot.gridOrigin = GridOrigin;
            boot.gridSize = GridSize;

            // Khách đi dọc vỉa hè từ trái sang phải
            boot.spawnPoint = Marker("SpawnPoint", new Vector3(-5.5f, 0f, -2.2f), go);
            boot.exitPoint = Marker("ExitPoint", new Vector3(5.5f, 0f, -2.2f), go);

            // Bố cục mặc định Tier 1: xe đẩy sát tường, 2 bàn × 2 ghế, dù ở góc phải.
            // Xe đẩy xoay 180° để biển hiệu (+Z của model) nhìn ra phố / về camera.
            boot.defaultLayout = new[]
            {
                new LayoutEntry("Cart_Coffee_3x2", new Vector2Int(6, 7), 2),
                new LayoutEntry("Umbrella_Street_4x4", new Vector2Int(12, 5), 0),
                new LayoutEntry("Table_Low_Blue_2x2", new Vector2Int(3, 2), 0),
                new LayoutEntry("Stool_Plastic_Red", new Vector2Int(2, 2), 1),
                new LayoutEntry("Stool_Plastic_Blue", new Vector2Int(5, 3), 3),
                new LayoutEntry("Table_Low_Inox_2x2", new Vector2Int(9, 2), 0),
                new LayoutEntry("Stool_Plastic_Green", new Vector2Int(8, 3), 1),
                new LayoutEntry("Stool_Plastic_Red", new Vector2Int(11, 2), 3),
            };
        }

        /// Copy docs/story/<file> -> Assets/_Project/Data/Story/ và trả về TextAsset (docs/ là nguồn gốc).
        static TextAsset ImportStoryJson(string file)
        {
            string src = Path.Combine(ArtImporter.RepoRoot, "docs", "story", file);
            if (!File.Exists(src)) { Debug.LogWarning("[Cafe] Thiếu " + src); return null; }
            const string dir = "Assets/_Project/Data/Story";
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", dir));
            string dst = Path.Combine(ArtImporter.RepoRoot, "game", dir, file);
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dir + "/" + file, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/" + file);
        }

        static Transform Marker(string name, Vector3 pos, GameObject parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent.transform, false);
            t.position = pos;
            return t;
        }
    }

    /// Mesh hộp với mọi UV dồn vào tâm 1 ô palette, dùng chung M_Toon_Palette.
    public static class PaletteMesh
    {
        public static Mesh Box(Vector3 size, int swatch)
        {
            int row = swatch / 8, col = swatch % 8;
            var uv = new Vector2((col + 0.5f) / 8f, 1f - (row + 0.5f) / 8f);
            var h = size * 0.5f;
            Vector3[] n = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var uvs = new Vector2[24];
            var tris = new int[36];
            for (int f = 0; f < 6; f++)
            {
                var normal = n[f];
                var a = normal.y != 0 ? Vector3.right : Vector3.up;
                var u = Vector3.Cross(normal, a).normalized;
                var v = Vector3.Cross(normal, u);
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 0 || k == 3) ? -1 : 1;
                    float sv = (k < 2) ? -1 : 1;
                    var p = normal + u * su + v * sv;
                    verts[f * 4 + k] = Vector3.Scale(p, h);
                    norms[f * 4 + k] = normal;
                    uvs[f * 4 + k] = uv;
                }
                int b = f * 4;
                // Unity: mặt trước khi Cross(p1 - p0, p2 - p0) cùng hướng normal
                tris[f * 6 + 0] = b; tris[f * 6 + 1] = b + 1; tris[f * 6 + 2] = b + 2;
                tris[f * 6 + 3] = b; tris[f * 6 + 4] = b + 2; tris[f * 6 + 5] = b + 3;
            }
            var mesh = new Mesh { vertices = verts, normals = norms, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
