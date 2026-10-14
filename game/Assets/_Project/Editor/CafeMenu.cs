using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Menu "Cafe/..." trong Editor và điểm vào cho batch mode:
    ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.CafeMenu.SetupAll
    public static class CafeMenu
    {
        [MenuItem("Cafe/1. Apply Project Settings")]
        public static void ApplySettings()
        {
            ProjectSetup.Apply();
            UrpSetupBridge.Ensure();
        }

        [MenuItem("Cafe/2. Import Art (art_source/export)")]
        public static void ImportArt()
        {
            ArtImporter.ImportAll();
            CharacterImporter.ImportAll();
            CharacterImporter.ImportOwners();
            CharacterImporter.ImportStaff();
        }

        [MenuItem("Cafe/3. Build Prototype Scene")]
        public static void BuildScene() => PrototypeSceneBuilder.Build();

        [MenuItem("Cafe/Setup All (1+2+3)")]
        public static void SetupAll()
        {
            ApplySettings();
            ImportArt();
            BrandingSetup.Apply();
            BuildScene();
            BootSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] Setup All xong.");
        }
    }
}
