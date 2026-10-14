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
