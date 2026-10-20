using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cafe.EditorTools
{
    /// Tạo ShaderVariantCollection cho Toon_Palette (ForwardLit) và đưa vào Graphics Settings > Preloaded shaders: Unity warm-up
    /// các biến thể này lúc khởi động (màn Boot) thay vì biên dịch pipeline lần đầu lúc trời tối (đèn phụ _ADDITIONAL_LIGHTS).
    /// Các tổ hợp = 3 mức đồ họa x {ban ngày không đèn, ban đêm có đèn}. Chạy lại nếu đổi multi_compile của shader.
    ///   Unity -batchmode -quit -projectPath game -executeMethod Cafe.EditorTools.ShaderWarmupSetup.Apply
    public static class ShaderWarmupSetup
    {
        const string CollectionPath = "Assets/_Project/Settings/SVC_ToonPalette.shadervariants";

        static readonly string[][] Variants =
        {
            new string[0],                                                                       // Thấp
            new[] { "_ADDITIONAL_LIGHTS" },
            new[] { "_MAIN_LIGHT_SHADOWS" },                                                     // Vừa
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT" },                                    // Cao
            new[] { "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_ADDITIONAL_LIGHTS" },
        };

        [MenuItem("Cafe/Graphics/Build Shader Warm-up Collection")]
        public static void Apply()
        {
            Shader shader = Shader.Find("Cafe/Toon_Palette");
            if (shader == null) { Debug.LogError("[ShaderWarmup] Không tìm thấy shader Cafe/Toon_Palette."); return; }

            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionPath);
            bool isNew = svc == null;
            if (isNew) svc = new ShaderVariantCollection();
            svc.Clear();
            foreach (string[] keywords in Variants)
                svc.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline, keywords));
            if (isNew) AssetDatabase.CreateAsset(svc, CollectionPath);
            else EditorUtility.SetDirty(svc);

            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = gs.FindProperty("m_PreloadedShaders");
            bool listed = false;
            for (int i = 0; i < list.arraySize; i++) listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == svc;
            if (!listed)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = svc;
                gs.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ShaderWarmup] " + Variants.Length + " biến thể Toon_Palette -> " + CollectionPath + " (Preloaded shaders).");
        }
    }
}
