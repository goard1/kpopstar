// 곱하기 블렌드 셰이더.
// _Radial = 1: 가짜 그림자 (UV 중심에서 멀어질수록 흐려지는 원형)
// _Radial = 0: 유리 (면 전체를 살짝 물들임)
Shader "Munbanggu/BlobShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.55,0.45,0.42,1)
        _Radial ("Radial Falloff", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" }

        Pass
        {
            Name "Multiply"
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Radial;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half d = saturate(dot(i.uv, i.uv));
                half radial = 1.0h - smoothstep(0.30h, 1.0h, d);
                half a = lerp(1.0h, radial, _Radial);
                return half4(lerp(half3(1,1,1), _ShadowColor.rgb, a), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
