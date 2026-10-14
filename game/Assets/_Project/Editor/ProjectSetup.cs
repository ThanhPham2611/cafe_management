using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Player Settings cho mobile màn hình ngang (tech_architecture.md, art_bible §6.2).
    public static class ProjectSetup
    {
        public const string BundleId = "com.quancaphe.viet";
        /// Tên sản phẩm (hiện dưới icon / thanh tiêu đề). Một chỗ duy nhất; BrandingSetup dùng lại.
        /// Có dấu tiếng Việt: đổi tên làm thư mục save Windows (persistentDataPath) đổi theo, save dev cũ ở thư mục tên cũ.
        public const string ProductName = "Quán Cà Phê Việt";

        public static void Apply()
        {
            PlayerSettings.companyName = "QuanCaPhe";
            PlayerSettings.productName = ProductName;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Chỉ màn hình ngang, tự xoay giữa 2 chiều ngang
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, BundleId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            // Prototype dùng UnityEngine.Input cũ (không cài Input System package)
            SetActiveInputHandler(0);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Debug.Log("[Cafe] Project settings: landscape, Android IL2CPP ARM64, Linear.");
        }

        /// 0 = Input Manager (cũ), 1 = Input System, 2 = cả hai.
        static void SetActiveInputHandler(int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == value) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
