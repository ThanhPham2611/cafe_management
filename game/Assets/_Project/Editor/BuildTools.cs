using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Build bản test. Batch mode:
    ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.BuildTools.BuildWindowsTest
    public static class BuildTools
    {
        const string WinPath = "Builds/WinTest/QuanCaPhe.exe";
        const string ApkPath = "Builds/Android/QuanCaPhe.apk";

        /// APK dev (ký bằng debug keystore của Unity) để cài thử lên điện thoại:
        ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.BuildTools.BuildAndroidApk
        [MenuItem("Cafe/Build/Android APK (dev)")]
        public static void BuildAndroidApk()
        {
            ProjectSetup.Apply();
            PlayerSettings.Android.useCustomKeystore = false; // debug keystore
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            var options = new BuildPlayerOptions
            {
                scenes = BootSceneBuilder.BuildScenes(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Cafe] Android build {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, " +
                      $"{s.totalErrors} lỗi, {s.totalWarnings} cảnh báo)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// Xcode project cho iOS Simulator (chạy thử trên máy ảo, không cần ký):
        ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.BuildTools.BuildIosSimulator
        [MenuItem("Cafe/Build/iOS Simulator (Xcode project)")]
        public static void BuildIosSimulator()
        {
            ProjectSetup.Apply();
            var oldSdk = PlayerSettings.iOS.sdkVersion;
            // API công khai không có simulatorArchitecture -> ghi thẳng vào ProjectSettings (1 = ARM64)
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var archProp = ps.FindProperty("iOSSimulatorArchitecture");
            var oldArch = archProp.intValue;
            archProp.intValue = 1;
            ps.ApplyModifiedProperties();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Prototype_Tier1.unity" },
                locationPathName = "Builds/iOSSim",
                target = BuildTarget.iOS,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            PlayerSettings.iOS.sdkVersion = oldSdk;
            archProp.intValue = oldArch;
            ps.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            var s = report.summary;
            Debug.Log($"[Cafe] iOS sim build {s.result}: {s.outputPath} ({s.totalErrors} lỗi, {s.totalWarnings} cảnh báo)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// Bản Development cho macOS (chạy bot autoplay / chụp màn hình trên máy Mac):
        ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.BuildTools.BuildMacTest
        [MenuItem("Cafe/Build/macOS Test")]
        public static void BuildMacTest()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // test tự động không bị dừng khi mất focus

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Prototype_Tier1.unity" },
                locationPathName = ArgValue("-buildOut") ?? "Builds/MacTest/QuanCaPhe.app",   // -buildOut <đường dẫn>: dựng ra chỗ khác để không đè bản đang chạy
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Cafe] Build {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, " +
                      $"{s.totalErrors} lỗi, {s.totalWarnings} cảnh báo)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string ArgValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [MenuItem("Cafe/Build/Windows Test")]
        public static void BuildWindowsTest()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // test tự động không bị dừng khi mất focus

            var options = new BuildPlayerOptions
            {
                scenes = BootSceneBuilder.BuildScenes(),
                locationPathName = WinPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Cafe] Build {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, " +
                      $"{s.totalErrors} lỗi, {s.totalWarnings} cảnh báo)");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
