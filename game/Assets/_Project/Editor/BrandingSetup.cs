using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Logo / app icon / splash (docs/onboarding_spec.md §3). Ảnh sinh bởi tools/make_logo.py vào Art/UI/Logo.
    /// Batch: Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.BrandingSetup.Apply
    public static class BrandingSetup
    {
        public const string ProductName = ProjectSetup.ProductName;
        public const string LogoDir = "Assets/_Project/Art/UI/Logo";
        public const string IconFull = LogoDir + "/logo_icon_1024.png";
        public const string AdaptiveFg = LogoDir + "/icon_adaptive_fg_432.png";
        public const string AdaptiveBg = LogoDir + "/icon_adaptive_bg_432.png";
        static readonly Color SplashBg = new Color32(0x4A, 0x2C, 0x1E, 255);

        [MenuItem("Cafe/4. Apply Branding (logo, icon, splash)")]
        public static void Apply()
        {
            ImportTextures();

            PlayerSettings.productName = ProductName;

            var android = NamedBuildTarget.Android;
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFull);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveFg);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveBg);
            if (full == null || fg == null || bg == null)
            {
                Debug.LogWarning("[Branding] Thiếu PNG trong " + LogoDir + " (chạy: python tools/make_logo.py). Bỏ qua icon.");
            }
            else
            {
                // Icon mặc định (mọi nền tảng) + Android legacy / round / adaptive
                // AndroidPlatformIconKind (UnityEditor.Android) chỉ biên dịch khi active target là Android -> lấy bằng reflection
                SetKind(android, "Legacy", full, null);
                SetKind(android, "Round", full, null);
                SetKind(android, "Adaptive", bg, fg);   // layer 0 = background, 1 = foreground
            }

            // Splash: tắt logo Unity + splash mặc định, nền cà phê. (Android 12 splash dùng icon adaptive ở trên;
            // Unity không có API riêng cho ảnh splash Android 12 nên bỏ qua.)
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.backgroundColor = SplashBg;

            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] Branding: productName=" + ProductName + ", icon + splash đã áp dụng.");
        }

        static void SetKind(NamedBuildTarget target, string kindName, Texture2D first, Texture2D second)
        {
            var type = System.Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions");
            var prop = type?.GetProperty(kindName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var kind = prop?.GetValue(null) as PlatformIconKind;
            if (kind == null)
            {
                Debug.LogWarning("[Branding] Không tìm thấy AndroidPlatformIconKind." + kindName + " (cần Android Build Support). Bỏ qua.");
                return;
            }
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons)
            {
                if (second != null) icon.SetTextures(first, second);
                else icon.SetTexture(first);
            }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        static void ImportTextures()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LogoDir });
            foreach (var path in guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png")))
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool changed = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || !ti.alphaIsTransparency;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                // nén do UiSpriteImporter quyết định (ASTC trừ ảnh nguồn icon app)
                if (changed) ti.SaveAndReimport();
            }
        }
    }
}
