// URP 전용 툰 셰이더 — Built-In 파이프라인에서는 분홍으로 보임
// 프로젝트가 URP(Universal Render Pipeline)임을 확인하고 사용할 것
Shader "PersonalityBox/ToonLit"
{
    Properties
    {
        _BaseMap        ("Albedo (RGB)",    2D)    = "white" {}
        _BaseColor      ("Base Color",      Color) = (1,1,1,1)
        _EmissionMap    ("Emission (RGB)",  2D)    = "black" {}
        [HDR]
        _EmissionColor  ("Emission Color",  Color) = (0,0,0,0)

        [Header(Toon Shading)]
        _RampSteps      ("Shadow Steps",     Range(1,8))     = 3
        _RampSmooth     ("Step Softness",    Range(0.001,0.3)) = 0.05
        _ShadowColor    ("Shadow Color",     Color) = (0.2,0.22,0.3,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1))     = 0.5

        [Header(Outline)]
        _OutlineWidth   ("Outline Width",    Range(0,0.08))  = 0.025
        _OutlineColor   ("Outline Color",    Color) = (0.05,0.05,0.05,1)

        [Header(Rim Light)]
        [HDR]
        _RimColor       ("Rim Color",        Color) = (0.4,0.6,1,1)
        _RimPower       ("Rim Power",        Range(0.5,8))   = 3
        _RimStrength    ("Rim Strength",     Range(0,1))     = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Pass 1 : 아웃라인 (Inverted Hull) ────────────────────────────────
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull   Front
            ZWrite On
            ZTest  LEqual
            Offset 1, 1

            HLSLPROGRAM
            #pragma vertex   VertOutline
            #pragma fragment FragOutline
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                half4  _ShadowColor;
                half4  _OutlineColor;
                half4  _RimColor;
                float  _RampSteps;
                float  _RampSmooth;
                float  _ShadowThreshold;
                float  _OutlineWidth;
                float  _RimPower;
                float  _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 posHCS : SV_POSITION; };

            Varyings VertOutline(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float4 posCS = TransformObjectToHClip(IN.posOS.xyz);
                // 뷰 공간 노멀 기준 바깥쪽으로 확장
                float3 normVS = mul((float3x3)UNITY_MATRIX_IT_MV, IN.normOS);
                float2 offset = normalize(normVS.xy) * posCS.z * _OutlineWidth;
                posCS.xy += offset;
                OUT.posHCS = posCS;
                return OUT;
            }

            half4 FragOutline(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ── Pass 2 : ForwardLit (툰 조명) ────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                half4  _ShadowColor;
                half4  _OutlineColor;
                half4  _RimColor;
                float  _RampSteps;
                float  _RampSmooth;
                float  _ShadowThreshold;
                float  _OutlineWidth;
                float  _RimPower;
                float  _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posHCS  : SV_POSITION;
                float2 uv      : TEXCOORD0;
                float3 normWS  : TEXCOORD1;
                float3 posWS   : TEXCOORD2;
                float  fogFact : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos  = GetVertexPositionInputs(IN.posOS.xyz);
                VertexNormalInputs   norm = GetVertexNormalInputs(IN.normOS);
                OUT.posHCS  = pos.positionCS;
                OUT.posWS   = pos.positionWS;
                OUT.normWS  = norm.normalWS;
                OUT.uv      = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFact = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // NdotL을 _RampSteps 계단으로 양자화 (셀 쉐이딩 핵심)
            half ToonRamp(half NdotL)
            {
                half t    = saturate((NdotL - _ShadowThreshold + 0.5h) / (_RampSmooth + 0.001h));
                half s    = floor(t * _RampSteps) / _RampSteps;
                return saturate(smoothstep(s, s + _RampSmooth, t));
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap,     sampler_BaseMap,     IN.uv) * _BaseColor;
                half4 emiss  = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv) * _EmissionColor;

                float3 N = normalize(IN.normWS);
                float3 V = normalize(GetCameraPositionWS() - IN.posWS);

                // 메인 라이트 + 그림자
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.posWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                Light main = GetMainLight(shadowCoord);

                half NdotL = dot(N, main.direction) * 0.5h + 0.5h;
                half toon  = ToonRamp(NdotL) * main.shadowAttenuation;
                half4 lit  = lerp(_ShadowColor, half4(1,1,1,1), toon);

                // 림 라이트 (외곽선 빛 발광)
                half rim = pow(1.0h - saturate(dot(V, N)), _RimPower) * _RimStrength;

                half4 col;
                col.rgb  = albedo.rgb * lit.rgb * half3(main.color);
                col.rgb += _RimColor.rgb * rim;
                col.rgb += emiss.rgb;
                col.a    = albedo.a;

                col.rgb = MixFog(col.rgb, IN.fogFact);
                return col;
            }
            ENDHLSL
        }

        // ShadowCaster / DepthOnly 는 URP Lit 폴백이 처리
    }

    FallBack "Universal Render Pipeline/Lit"
}
