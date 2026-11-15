Shader "ESP/Offline Ground Lights"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-11" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "EarthNightLights.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 geographicUv : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 geographicUv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.geographicUv = input.geographicUv;
                return output;
            }
            // Keep the serialized fallback material compatible, but do not paint
            // the coarse orbital light atlas onto the authored local ground.
            half4 Frag(Varyings input) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }
    }
}
