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
                float4 _ForwardSky, _RightSky, _UpSky, _ViewParams, _SunSky, _SkyAdjust;
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
                float3 color = SAMPLE_TEXTURE2D_GRAD(_Sky, sampler_Sky, uv, dx, dy).rgb;
                float luminance = dot(color, float3(.2126, .7152, .0722));
                color = max(0, lerp(luminance.xxx, color, _SkyAdjust.y)) * _SkyAdjust.x;
                // Original procedural solar disc and optical flare, behind Earth.
                // The disc follows the same solar vector as the directional light.
                if (_SunSky.w > 0 && dot(ray, _SunSky.xyz) > 0)
                {
                    float radius = max(_SunSky.w, .00001);
                    float angular = length(cross(ray, _SunSky.xyz));
                    float disc = 1 - smoothstep(radius * .88, radius * 1.12, angular);
                    float halo = exp(-angular / max(radius * 2.5, .002));
                    float2 separation = float2(dot(ray - _SunSky.xyz, _RightSky.xyz), dot(ray - _SunSky.xyz, _UpSky.xyz));
                    float streak = exp(-abs(separation.x) / max(radius * .15, .0002) - abs(separation.y) / max(radius * 8, .012)) +
                        exp(-abs(separation.y) / max(radius * .15, .0002) - abs(separation.x) / max(radius * 8, .012));
                    color += float3(1.0, .86, .60) * (disc * 2.0 + halo * .24 + streak * .08);
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
