using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Cafe.EditorTools
{
    /// Dựng Scenes/Boot.unity (scene số 0): camera nền kem + EventSystem + BootLoader (màn tải, logo, mẹo),
    /// gán theme + logo, rồi đặt Build Settings = [Boot, Prototype_Tier1]. Chạy sau PrototypeSceneBuilder.Build()
    /// (vì hàm đó ghi lại Build Settings chỉ với scene chính). Mở thẳng Prototype_Tier1 trong Editor vẫn chạy bình thường.
    public static class BootSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Boot.unity";
        public const string MainScenePath = "Assets/_Project/Scenes/Prototype_Tier1.unity";
        public const string LogoPath = "Assets/_Project/Art/UI/Logo/logo_wide.png";

        [MenuItem("Cafe/4. Build Boot Scene")]
        public static void Build()
        {
            // NewScene(Single) bỏ scene đang mở: hỏi lưu trước (batch mode thì bỏ qua), xong mở lại scene cũ
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = Application.isBatchMode ? "" : SceneManager.GetActiveScene().path;

            UiTheme theme = UiSpriteImporter.EnsureTheme();
            Sprite logo = LoadLogo();
            if (logo == null) Debug.LogWarning("[Cafe] Chưa có " + LogoPath + " - màn tải / tiêu đề dùng chữ lớn thay logo. Chạy lại Cafe/4. Build Boot Scene khi có logo.");
            if (theme != null)
            {
                theme.logoWide = logo; // màn tiêu đề (scene chính) đọc logo từ theme
                EditorUtility.SetDirty(theme);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme != null ? theme.cream : new Color32(0xFF, 0xF4, 0xDC, 0xFF);
            cam.orthographic = true;
            cam.cullingMask = 0; // chỉ UI overlay
            camGo.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var go = new GameObject("BootLoader");
            var loader = go.AddComponent<BootLoader>();
            loader.theme = theme;
            loader.logo = logo;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            ApplyBuildSettings();
            if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            Debug.Log("[Cafe] Đã dựng " + ScenePath + " (Build Settings: Boot = 0, Prototype_Tier1 = 1)");
        }

        /// Boot ở index 0, Prototype_Tier1 ở index 1.
        public static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
            };
        }

        /// Danh sách scene cho BuildPipeline: có Boot.unity thì để đầu, rồi scene chính.
        public static string[] BuildScenes()
        {
            if (File.Exists(ScenePath)) return new[] { ScenePath, MainScenePath };
            return new[] { MainScenePath };
        }

        static Sprite LoadLogo()
        {
            if (!File.Exists(LogoPath)) return null;
            AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        }
    }
}
