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
    /// Tạo 3 URP asset cho mobile theo art_bible §6 — mỗi mức đồ họa một asset (runtime chọn bằng QualitySettings, xem GraphicsQuality):
    ///   Thấp  URP_Mobile_Low    : không bóng, độ nét 70%, không MSAA, tắt đèn phụ
    ///   Vừa   URP_Mobile_Medium : bóng cứng 512 (18 -> 14 m), độ nét 85%, đèn phụ per-pixel (4 đèn / vật)
    ///   Cao   URP_Mobile        : bóng mềm 1024, độ nét 100%, MSAA 2x, đèn phụ per-pixel (8 đèn / vật)
    /// Đèn phụ = đèn thật của đồ đèn về đêm (LampLights, số đèn lấy từ GraphicsPresets.LampLights), không bóng đổ ở mọi mức.
    /// Chung: 1 cascade, không HDR, không depth/opaque texture, SRP Batcher bật. Mức chất lượng i của Unity = GraphicsLevel i
    /// (ProjectSettings/QualitySettings có đúng 3 mức Low / Medium / High).
    public static class UrpSetup
    {
        const string Dir = "Assets/_Project/Settings";
        const string RendererPath = Dir + "/URP_Mobile_Renderer.asset";

        struct Preset
        {
            public string path;
            public bool shadows, soft;
            public int shadowRes, msaa;
            public GraphicsLevel level;
            public float shadowDistance, renderScale;
        }

        static readonly Preset[] Presets =
        {
            new Preset { path = Dir + "/URP_Mobile_Low.asset", shadows = false, soft = false, shadowRes = 512, msaa = 1, shadowDistance = 14f, renderScale = 0.7f, level = GraphicsLevel.Low },
            new Preset { path = Dir + "/URP_Mobile_Medium.asset", shadows = true, soft = false, shadowRes = 512, msaa = 1, shadowDistance = 14f, renderScale = 0.85f, level = GraphicsLevel.Medium },
            new Preset { path = Dir + "/URP_Mobile.asset", shadows = true, soft = true, shadowRes = 1024, msaa = 2, shadowDistance = 18f, renderScale = 1f, level = GraphicsLevel.High },
        };

        public static void Ensure()
        {
            var assets = new UniversalRenderPipelineAsset[Presets.Length];
            for (int i = 0; i < Presets.Length; i++)
            {
                assets[i] = LoadOrCreate(Presets[i].path);
                Configure(assets[i], Presets[i]);
            }

            GraphicsSettings.defaultRenderPipeline = assets[assets.Length - 1]; // Cao
            int current = QualitySettings.GetQualityLevel();
            int levels = Mathf.Min(QualitySettings.names.Length, assets.Length);
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = assets[i];
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(current, 0, levels - 1), false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cafe] URP Thấp / Vừa / Cao đã gán cho 3 mức chất lượng (Low / Medium / High).");
        }

        static UniversalRenderPipelineAsset LoadOrCreate(string path)
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (rp != null) return rp;
            Directory.CreateDirectory(Dir);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }
            rp = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(rp, path);
            return rp;
        }

        static void Configure(UniversalRenderPipelineAsset rp, Preset p)
        {
            // Set qua SerializedObject để không phụ thuộc setter public của từng phiên bản URP
            var so = new SerializedObject(rp);
            SetFloat(so, "m_ShadowDistance", p.shadowDistance);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetInt(so, "m_MainLightShadowmapResolution", p.shadowRes);
            SetBool(so, "m_MainLightShadowsSupported", p.shadows);
            SetBool(so, "m_AnyShadowsSupported", p.shadows);
            SetBool(so, "m_SoftShadowsSupported", p.soft);
            SetInt(so, "m_MSAA", p.msaa);
            SetBool(so, "m_SupportsHDR", false);
            SetFloat(so, "m_RenderScale", p.renderScale);
            SetBool(so, "m_RequireDepthTexture", false);
            SetBool(so, "m_RequireOpaqueTexture", false);
            SetBool(so, "m_UseSRPBatcher", true);
            int lamps = GraphicsPresets.LampLights(p.level);
            SetInt(so, "m_AdditionalLightsRenderingMode", lamps > 0 ? 1 : 0); // 1 = PerPixel, 0 = tắt (mức Thấp: đèn chỉ phát sáng trên mô hình)
            SetInt(so, "m_AdditionalLightsPerObjectLimit", Mathf.Max(4, lamps));
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            so.ApplyModifiedPropertiesWithoutUndo();
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
