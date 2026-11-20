// 추억의 문방구 — 모바일용 플랫/툰 셰이더 (URP)
// - 메인 라이트 2단 램프 + 하늘/바닥 앰비언트 (그림자 없음)
// - 버텍스 컬러 × _BaseColor (MeshKit 메시는 버텍스 컬러, 기본 도형은 _BaseColor)
// - 외곽선: 같은 셰이더의 두 번째 패스(SRPDefaultUnlit, 앞면 컬링 inverted hull).
//   외곽선 방향은 UV2(TEXCOORD1)의 부드러운 노멀, 없으면 노멀 사용.
//   material.SetShaderPassEnabled("SRPDefaultUnlit", false) 로 끌 수 있음.
Shader "Munbanggu/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 0
        _ShadeColor ("Shade Tint", Color) = (0.72,0.62,0.66,1)
        _SkyColor ("Ambient Sky", Color) = (0.42,0.40,0.38,1)
        _GroundColor ("Ambient Ground", Color) = (0.30,0.24,0.22,1)
        _RampCenter ("Ramp Center", Range(0,1)) = 0.52
        _RampSoftness ("Ramp Softness", Range(0.001,0.5)) = 0.06
        _OutlineColor ("Outline Color", Color) = (0.24,0.15,0.12,1)
        _OutlineWidth ("Outline Width", Float) = 0.018
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _UseVertexColor;
            half4 _ShadeColor;
            half4 _SkyColor;
            half4 _GroundColor;
            half _RampCenter;
            half _RampSoftness;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = lerp(half4(1,1,1,1), v.color, _UseVertexColor);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight();
                half ndl = dot(n, mainLight.direction) * 0.5h + 0.5h;
                half ramp = smoothstep(_RampCenter - _RampSoftness, _RampCenter + _RampSoftness, ndl);
                half3 lit = lerp(_ShadeColor.rgb, half3(1,1,1), ramp) * mainLight.color;
                half3 ambient = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * 0.5h + 0.5h);
                half3 albedo = _BaseColor.rgb * i.color.rgb;
                return half4(albedo * (lit + ambient), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                // MeshKit 메시: UV2가 (0,0,0)이면 외곽선 없음 → 삼각형을 한 점으로 접어 버림
                // UV2가 아예 없는 메시(기본 도형)는 노멀 방향 사용
                float len2 = dot(v.smoothNormal, v.smoothNormal);
                float3 dirOS = _UseVertexColor > 0.5 ? v.smoothNormal : (len2 > 0.01 ? v.smoothNormal : v.normalOS);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 dirWS = TransformObjectToWorldNormal(dirOS);
                posWS += dirWS * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                if (_UseVertexColor > 0.5 && len2 < 0.01) o.positionCS = float4(0, 0, -2, 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
