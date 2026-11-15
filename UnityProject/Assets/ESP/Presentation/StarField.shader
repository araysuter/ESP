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
                float4 _ForwardSky, _RightSky, _UpSky, _ViewParams, _SunSky, _SkyAdjust, _SolarOptics, _SkyDaylight;
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
                // Suppress faint stars under illuminated lower air BEFORE adding
                // solar radiance. This artistic exposure term must never become
                // atmosphere opacity: otherwise it would erase the Sun as well.
                float stellarAirMass = lerp(4, 1, saturate(dot(ray, _SkyDaylight.xyz)));
                color *= exp(-_SkyDaylight.w * stellarAirMass);
                // True solar angular size, with display glare outside the disc.
                // Glare represents camera optics, not extra solar geometry.
                if (_SunSky.w > 0 && dot(ray, _SunSky.xyz) > 0)
                {
                    float radius = max(_SunSky.w, .00001);
                    float angular = length(cross(ray, _SunSky.xyz));
                    float pixel = max(fwidth(angular), .000001);
                    float disc = 1 - smoothstep(radius - pixel, radius + pixel, angular);
                    float limb = sqrt(saturate(1 - angular * angular / (radius * radius)));
                    // Nearly white overexposed photosphere; mild limb darkening
                    // remains visible when exposure is reduced. Not radiometry.
                    color += float3(1.0, .975, .92) * disc * (16 + 16 * limb);
                    float2 separation = float2(dot(ray - _SunSky.xyz, _RightSky.xyz), dot(ray - _SunSky.xyz, _UpSky.xyz));
                    float halo = exp(-angular / max(radius * 3, .001));
                    float coreBloom = exp(-angular * angular / max(radius * radius * 5, .000001));
                    float streak = exp(-abs(separation.y) / max(radius * .10, .0001)) *
                        exp(-abs(separation.x) / max(radius * 10, .004));
                    // Earth-source visibility gates all optical artifacts: a sun
                    // behind the planet cannot paint a halo around its far limb.
                    // Diffraction spikes and a wider HDR shoulder are camera
                    // effects; the photosphere itself retains its true angular radius.
                    float theta = atan2(separation.y, separation.x);
                    float spikes = pow(abs(cos(theta * 4)), 40) * exp(-angular / max(radius * 22, .008));
                    float shoulder = exp(-angular / max(radius * 9, .003));
                    color += float3(1, .93, .80) * _SolarOptics.x *
                        (halo * 2.2 + coreBloom * 4.0 + shoulder * .26 + streak * .32 + spikes * 1.8);
                }
                // Lens ghosts lie on the line through the optical center and
                // the light source. Keep them in this background layer so opaque
                // geometry still occludes them, and suppress them below the horizon.
                float sunForward = dot(_SunSky.xyz, _ForwardSky.xyz);
                if (_SunSky.w > 0 && _SolarOptics.x > 0 && sunForward > .001)
                {
                    float2 sunScreen = float2(dot(_SunSky.xyz, _RightSky.xyz) / (sunForward * _ViewParams.x),
                        dot(_SunSky.xyz, _UpSky.xyz) / (sunForward * _ViewParams.x) * _ProjectionParams.x);
                    float2 screen = float2(input.screen.x * _ViewParams.y, input.screen.y);
                    float edge = 1 - smoothstep(.82, 1.22, max(abs(sunScreen.x / _ViewParams.y), abs(sunScreen.y)));
                    float2 g1 = screen + sunScreen * .32;
                    float2 g2 = screen + sunScreen * .68;
                    float2 g3 = screen + sunScreen * 1.08;
                    float r1=length(g1), r2=length(g2), r3=length(g3);
                    float ring=exp(-pow((r2-.12)/.015,2));
                    float3 ghosts = float3(.20,.55,.40) * exp(-r1*r1/.003)
                        + float3(.55,.24,.12) * exp(-r2*r2/.008)
                        + float3(.12,.28,.60) * exp(-r3*r3/.012)
                        + float3(.22,.12,.30) * ring * .35;
                    color += ghosts * edge * _SolarOptics.x;
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
