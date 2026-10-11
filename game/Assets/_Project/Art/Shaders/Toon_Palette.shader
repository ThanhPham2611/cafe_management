// M_Toon_Palette (art_bible §4): toon 3 bậc + shadow tint ấm + rim nhẹ, màu lấy từ T_Palette_256.
// _SwatchIndex >= 0: bỏ qua UV, tô cả mesh bằng 1 ô palette (đổi màu ghế, nhân vật placeholder)
// -> đặt qua MaterialPropertyBlock, không clone material.
Shader "Cafe/Toon_Palette"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Palette (T_Palette_256)", 2D) = "white" {}
        _SwatchIndex ("Swatch Index (-1 = dùng UV)", Float) = -1
        _ShadowTint ("Shadow Tint", Color) = (0.416, 0.29, 0.353, 1)
        _MidTone ("Mid Tone", Range(0, 1)) = 0.82
        _Band1 ("Band Shadow", Range(0, 1)) = 0.35
        _Band2 ("Band Light", Range(0, 1)) = 0.7
        _BandSoft ("Band Softness", Range(0.001, 0.2)) = 0.04
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _RimColor ("Rim Color", Color) = (1, 0.851, 0.627, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _SwatchIndex;
            half4 _ShadowTint;
            half _MidTone;
            half _Band1;
            half _Band2;
            half _BandSoft;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            float2 SwatchUV(float idx)
            {
                float row = floor(idx / 8.0);
                float col = idx - row * 8.0;
                return float2((col + 0.5) / 8.0, 1.0 - (row + 0.5) / 8.0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = _SwatchIndex >= 0 ? SwatchUV(_SwatchIndex) : i.uv;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;

                half3 n = normalize(i.normalWS);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                half ndl = saturate(dot(n, light.direction));
                half lit = ndl * light.shadowAttenuation;

                // 3 bậc: tối (shadow tint) -> giữa -> sáng
                half t1 = smoothstep(_Band1 - _BandSoft, _Band1 + _BandSoft, lit);
                half t2 = smoothstep(_Band2 - _BandSoft, _Band2 + _BandSoft, lit);
                half3 tone = lerp(_ShadowTint.rgb, _MidTone.xxx, t1);
                tone = lerp(tone, half3(1, 1, 1), t2);

                half3 col = albedo * tone * light.color;
                col += albedo * SampleSH(n) * _AmbientStrength;

                // Rim ấm chỉ ở phía ngược sáng
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * (1.0 - t2);
                col += _RimColor.rgb * rim * _RimIntensity;

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(ShadowAttributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 pos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return pos;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DepthAttributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
