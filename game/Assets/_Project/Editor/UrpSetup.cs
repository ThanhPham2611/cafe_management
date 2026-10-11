using System.IO;
using UnityEditor;
using UnityEngine;
#if CAFE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Cafe.EditorTools
{
    /// Gọi được cả khi URP chưa cài (CAFE_URP chưa định nghĩa) để CafeMenu không lỗi biên dịch.
    public static class UrpSetupBridge
    {
        public static void Ensure()
        {
#if CAFE_URP
            UrpSetup.Ensure();
#else
            Debug.LogWarning("[Cafe] Chưa có package com.unity.render-pipelines.universal, bỏ qua bước URP.");
#endif
        }
    }

#if CAFE_URP
    /// Tạo URP asset cho mobile theo art_bible §6: 1 cascade, shadow 18 m / 1024, không HDR/MSAA,
    /// không depth/opaque texture, SRP Batcher bật.
    public static class UrpSetup
    {
        const string Dir = "Assets/_Project/Settings";
        const string AssetPath = Dir + "/URP_Mobile.asset";
        const string RendererPath = Dir + "/URP_Mobile_Renderer.asset";

        public static void Ensure()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (rp == null)
            {
                Directory.CreateDirectory(Dir);
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                rp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(rp, AssetPath);
            }

            // Set qua SerializedObject để không phụ thuộc setter public của từng phiên bản URP
            var so = new SerializedObject(rp);
            SetFloat(so, "m_ShadowDistance", 18f);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetInt(so, "m_MainLightShadowmapResolution", 1024);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_MSAA", 1);
            SetBool(so, "m_SupportsHDR", false);
            SetFloat(so, "m_RenderScale", 1f);
            SetBool(so, "m_RequireDepthTexture", false);
            SetBool(so, "m_RequireOpaqueTexture", false);
            SetBool(so, "m_UseSRPBatcher", true);
            SetInt(so, "m_AdditionalLightsRenderingMode", 0); // tắt đèn phụ (fake bằng emission)
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = rp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = rp;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] URP_Mobile đã gán cho Graphics + mọi Quality level.");
        }

        static void SetFloat(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = v;
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = v;
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v;
        }
    }
#endif
}
