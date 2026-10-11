using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Đưa nhân vật khách từ art_source/export/characters vào Unity:
    /// copy FBX -> Art/Models/Characters, tạo PF_Char_<Variant> (material dùng chung + CapsuleCollider để chạm).
    /// Thông số import model do CafeAssetPostprocessor (ArtImporter.cs) áp cho mọi thứ dưới /_Project/Art/Models/.
    public static class CharacterImporter
    {
        const string ModelDir = ArtImporter.ArtRoot + "/Models/Characters";
        const string PrefabDir = "Assets/_Project/Prefabs/Characters";
        const string OwnerModelDir = ModelDir + "/Owner";
        const string OwnerPrefabDir = PrefabDir + "/Owner";
        static readonly string[] OwnerPresets = { "M1", "M2", "F1", "F2" }; // index = OwnerLook.preset
        const string StaffModelDir = ModelDir + "/Staff";
        const string StaffPrefabDir = PrefabDir + "/Staff";
        static readonly string[] StaffPresets = { "M1", "M2", "F1", "F2" };  // index = StaffSave.preset

        static string SourceDir { get { return Path.Combine(ArtImporter.RepoRoot, "art_source", "export", "characters"); } }

        public static GameObject[] ImportAll()
        {
            var result = new List<GameObject>();
            if (!Directory.Exists(SourceDir))
            {
                Debug.LogError("[Cafe] Thiếu thư mục " + SourceDir + " (chạy tools/gen_characters.py trước)");
                return result.ToArray();
            }

            string[] files = Directory.GetFiles(SourceDir, "SM_Char_Customer_*.fbx");
            System.Array.Sort(files);
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", ModelDir));
            foreach (string f in files)
            {
                string dst = Path.Combine(ArtImporter.RepoRoot, "game", ModelDir, Path.GetFileName(f));
                File.Copy(f, dst, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material mat = ArtImporter.EnsureMaterial();
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", PrefabDir));
            foreach (string f in files)
            {
                string fbx = Path.GetFileNameWithoutExtension(f); // SM_Char_Customer_Student
                GameObject prefab = BuildPrefab(fbx, mat);
                if (prefab != null) result.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] Import xong " + result.Count + " nhân vật khách.");
            return result.ToArray();
        }

        /// Các prefab PF_Char_* đã import, để PrototypeSceneBuilder gán vào GameBootstrap.customerPrefabs.
        public static GameObject[] LoadPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab PF_Char_", new[] { PrefabDir });
            var result = new List<GameObject>(guids.Length);
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                // chủ quán nằm ở Prefabs/Characters/Owner/ và không phải khách
                if (path.Contains("/Owner/") || path.Contains("/Staff/")) continue;
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) result.Add(p);
            }
            return result.ToArray();
        }

        /// Copy SM_Char_Owner_*.fbx -> Art/Models/Characters/Owner, dựng PF_Char_Owner_<M1|M2|F1|F2>.
        /// Trả về theo thứ tự M1, M2, F1, F2 (bỏ qua preset thiếu file).
        public static GameObject[] ImportOwners()
        {
            var result = new List<GameObject>();
            if (!Directory.Exists(SourceDir))
            {
                Debug.LogError("[Cafe] Thiếu thư mục " + SourceDir + " (chạy tools/gen_characters.py trước)");
                return result.ToArray();
            }

            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", OwnerModelDir));
            foreach (string preset in OwnerPresets)
            {
                string src = Path.Combine(SourceDir, "SM_Char_Owner_" + preset + ".fbx");
                if (!File.Exists(src)) { Debug.LogWarning("[Cafe] Thiếu " + src); continue; }
                File.Copy(src, Path.Combine(ArtImporter.RepoRoot, "game", OwnerModelDir, Path.GetFileName(src)), true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material mat = ArtImporter.EnsureMaterial();
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", OwnerPrefabDir));
            foreach (string preset in OwnerPresets)
            {
                GameObject prefab = BuildOwnerPrefab(preset, mat);
                if (prefab != null) result.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] Import xong " + result.Count + " chủ quán.");
            return result.ToArray();
        }

        /// Copy SM_Char_Staff_*.fbx -> Art/Models/Characters/Staff, dựng PF_Char_Staff_<M1|M2|F1|F2>.
        public static GameObject[] ImportStaff()
        {
            var result = new List<GameObject>();
            if (!Directory.Exists(SourceDir)) return result.ToArray();
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", StaffModelDir));
            foreach (string preset in StaffPresets)
            {
                string src = Path.Combine(SourceDir, "SM_Char_Staff_" + preset + ".fbx");
                if (!File.Exists(src)) { Debug.LogWarning("[Cafe] Thiếu " + src); continue; }
                File.Copy(src, Path.Combine(ArtImporter.RepoRoot, "game", StaffModelDir, Path.GetFileName(src)), true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material mat = ArtImporter.EnsureMaterial();
            Directory.CreateDirectory(Path.Combine(ArtImporter.RepoRoot, "game", StaffPrefabDir));
            foreach (string preset in StaffPresets)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(StaffModelDir + "/SM_Char_Staff_" + preset + ".fbx");
                if (model == null) continue;
                var root = new GameObject("PF_Char_Staff_" + preset);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                var col = root.AddComponent<CapsuleCollider>();
                col.direction = 1; col.radius = 0.3f; col.height = 1.3f; col.center = new Vector3(0f, 0.65f, 0f);
                result.Add(PrefabUtility.SaveAsPrefabAsset(root, StaffPrefabDir + "/PF_Char_Staff_" + preset + ".prefab"));
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] Import xong " + result.Count + " nhân viên.");
            return result.ToArray();
        }

        /// PF_Char_Staff_* đã import, thứ tự M1, M2, F1, F2 (index = StaffSave.preset). Preset thiếu -> null.
        public static GameObject[] LoadStaffPrefabs()
        {
            var result = new GameObject[StaffPresets.Length];
            for (int i = 0; i < StaffPresets.Length; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(StaffPrefabDir + "/PF_Char_Staff_" + StaffPresets[i] + ".prefab");
            return result;
        }

        /// PF_Char_Owner_* đã import, thứ tự M1, M2, F1, F2 (index = OwnerLook.preset). Preset thiếu -> null.
        public static GameObject[] LoadOwnerPrefabs()
        {
            var result = new GameObject[OwnerPresets.Length];
            for (int i = 0; i < OwnerPresets.Length; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                    OwnerPrefabDir + "/PF_Char_Owner_" + OwnerPresets[i] + ".prefab");
            return result;
        }

        static GameObject BuildOwnerPrefab(string preset, Material mat)
        {
            string modelPath = OwnerModelDir + "/SM_Char_Owner_" + preset + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("[Cafe] Không load được " + modelPath);
                return null;
            }

            var root = new GameObject("PF_Char_Owner_" + preset);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = 0.3f;
            col.height = 1.3f;
            col.center = new Vector3(0f, 0.65f, 0f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OwnerPrefabDir + "/PF_Char_Owner_" + preset + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildPrefab(string fbxName, Material mat)
        {
            string modelPath = ModelDir + "/" + fbxName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("[Cafe] Không load được " + modelPath);
                return null;
            }

            string id = fbxName.Substring("SM_Char_Customer_".Length);
            var root = new GameObject("PF_Char_" + id);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = 0.3f;
            col.height = 1.2f;
            col.center = new Vector3(0f, 0.6f, 0f);

            string path = PrefabDir + "/PF_Char_" + id + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
