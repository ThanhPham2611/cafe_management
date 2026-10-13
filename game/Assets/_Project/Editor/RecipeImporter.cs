using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Tạo/cập nhật các công thức Tier 2 (Phase 2, economy §2.1-2.2) và Tier 3 (economy §2.1) thành SO_Recipe_*.asset.
    /// Số liệu nằm ở Tier2RecipeData (dùng chung với RecipeBook ở runtime). Idempotent: chạy lại chỉ ghi đè trường dữ liệu,
    /// không nhân đôi asset. ArtImporter.EnsureRecipes gọi hàm này ở cuối.
    /// 4 món Tier 1 vẫn do ArtImporter.EnsureRecipes quản lý (không đụng ở đây).
    public static class RecipeImporter
    {
        const string RecipeDataDir = "Assets/_Project/Data/Recipes";

        [MenuItem("Cafe/Data/Ensure Tier-2 Recipes")]
        static void EnsureTier2RecipesMenu()
        {
            EnsureTier2Recipes();
            EnsureTier3Recipes();
            AssetDatabase.SaveAssets();
            AssignRecipesToOpenScene();
            Debug.Log("[Cafe] Đã tạo/cập nhật công thức Tier 2 (trà chanh, cà phê muối, cốt dừa, trứng) và Tier 3 (bạc xỉu kem muối, cold brew, robusta).");
        }

        /// Giá / vốn / thời gian pha đúng economy_monetization.md §2.1 (xem Tier2RecipeData).
        public static void EnsureTier2Recipes()
        {
            foreach (Tier2RecipeData.Entry e in Tier2RecipeData.All)
            {
                var path = $"{RecipeDataDir}/SO_Recipe_{e.id}.asset";
                var r = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
                if (r == null)
                {
                    Directory.CreateDirectory(RecipeDataDir);
                    r = ScriptableObject.CreateInstance<RecipeDef>();
                    AssetDatabase.CreateAsset(r, path);
                }
                Tier2RecipeData.Apply(r, e);
                EditorUtility.SetDirty(r);
            }
        }

        /// 3 món cấp 3 (economy §2.1 bảng T3, xem Tier3RecipeData).
        public static void EnsureTier3Recipes()
        {
            foreach (Tier3RecipeData.Entry e in Tier3RecipeData.All)
            {
                var path = $"{RecipeDataDir}/SO_Recipe_{e.id}.asset";
                var r = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
                if (r == null)
                {
                    Directory.CreateDirectory(RecipeDataDir);
                    r = ScriptableObject.CreateInstance<RecipeDef>();
                    AssetDatabase.CreateAsset(r, path);
                }
                Tier3RecipeData.Apply(r, e);
                EditorUtility.SetDirty(r);
            }
        }

        /// Gán lại GameBootstrap.recipes trong scene đang mở (scene dựng từ trước không tự biết asset mới).
        /// Hoặc chạy lại "Cafe/3. Build Prototype Scene" để dựng scene mới (ArtImporter.LoadRecipes lấy hết asset trong thư mục).
        [MenuItem("Cafe/Data/Assign Recipes To Open Scene")]
        static void AssignRecipesToOpenScene()
        {
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot == null)
            {
                Debug.LogWarning("[Cafe] Không thấy GameBootstrap trong scene đang mở; chạy Cafe/3. Build Prototype Scene để dựng lại.");
                return;
            }
            boot.recipes = ArtImporter.LoadRecipes();
            EditorUtility.SetDirty(boot);
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Debug.Log($"[Cafe] GameBootstrap.recipes = {boot.recipes.Length} công thức (nhớ lưu scene).");
        }
    }
}
