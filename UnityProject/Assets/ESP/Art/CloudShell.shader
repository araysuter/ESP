Shader "ESP/ObservedCloudShell"
{
    Properties
    {
        _CloudDensity ("Observed cloud fraction", 2D) = "black" {}
        _Opacity ("Display opacity", Range(0, 1)) = 0.88
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ObservedClouds"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_CloudDensity);
            SAMPLER(sampler_CloudDensity);
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudDensity_ST;
                half _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Scalar data texture is linear, interpolated only after exact palette
                // decoding. Missing pixels are zero: no invented cloud cover or motion.
                half density = SAMPLE_TEXTURE2D(_CloudDensity, sampler_CloudDensity, input.uv).r;
                Light sun = GetMainLight();
                half illumination = 0.30h + 0.70h * saturate(dot(normalize(input.normalWS), sun.direction));
                half3 white = half3(0.94h, 0.97h, 1.0h) * illumination;
                return half4(white, saturate(density * _Opacity));
            }
            ENDHLSL
        }
    }
}
