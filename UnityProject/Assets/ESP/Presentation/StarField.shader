Shader "ESP/Star Field"
{
    Properties { _Sky("NASA J2000 celestial sky (linear HDR)", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Sky); SAMPLER(sampler_Sky);
            CBUFFER_START(UnityPerMaterial)
                float4 _ForwardSky, _RightSky, _UpSky, _ViewParams;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 screen : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy, .5, 1);
                output.screen = input.positionOS.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(_ForwardSky.xyz + input.screen.x * _ViewParams.x * _ViewParams.y * _RightSky.xyz +
                    input.screen.y * _ProjectionParams.x * _ViewParams.x * _UpSky.xyz);
                // NASA plate carree: RA zero at center, increasing left; north at top.
                float2 uv = float2(.5 - atan2(ray.y, ray.x) / (2 * PI), .5 + asin(clamp(ray.z, -1, 1)) / PI);
                // Explicit gradients avoid a false coarse mip where RA wraps at the seam.
                float2 dx = ddx(uv), dy = ddy(uv);
                dx.x -= round(dx.x); dy.x -= round(dy.x);
                return half4(SAMPLE_TEXTURE2D_GRAD(_Sky, sampler_Sky, uv, dx, dy).rgb, 1);
            }
            ENDHLSL
        }
    }
}
