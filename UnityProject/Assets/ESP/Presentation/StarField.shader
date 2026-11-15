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
            // Defocused aperture: a broad colored wash, a soft interior and
            // a faint incomplete rim. Avoid crisp polygon outlines.
            float3 OpticalGhost(float2 offset, float size, float3 tint)
            {
                float2 q = offset / size;
                float r2 = dot(q, q);
                float r = sqrt(r2);
                float rimDistance = (r - .82) / .22;
                float rim = exp(-rimDistance * rimDistance);
                float crescent = saturate(.55 + q.x * .35 - q.y * .2);
                return tint * (exp(-r2 * 2.6) * .58
                    + exp(-r2 * .6) * .16 + rim * crescent * .09);
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
                    float halo = exp(-angular / max(radius * 6, .002));
                    float coreBloom = exp(-angular * angular / max(radius * radius * 5, .000001));
                    float streak = exp(-abs(separation.y) / max(radius * .10, .0001)) *
                        exp(-abs(separation.x) / max(radius * 5, .002));
                    // Earth-source visibility gates all optical artifacts: a sun
                    // behind the planet cannot paint a halo around its far limb.
                    // Diffraction spikes and a wider HDR shoulder are camera
                    // effects; the photosphere itself retains its true angular radius.
                    float theta = atan2(separation.y, separation.x);
                    // Keep the eight rays inside the soft glare, with no long tails.
                    float spikeEnvelope = 1 - smoothstep(radius * 16, radius * 30, angular);
                    float spikes = pow(abs(cos(theta * 4)), 40) * exp(-angular / max(radius * 7, .003)) * spikeEnvelope;
                    float shoulder = exp(-angular / max(radius * 16, .005));
                    color += float3(1, .93, .80) * _SolarOptics.x *
                        (halo * 2.2 + coreBloom * 4.0 + shoulder * .40 + streak * .32 + spikes * 1.8);
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
                    float2 axis = sunScreen / max(length(sunScreen), .0001);
                    float2 normal = float2(-axis.y, axis.x);
                    float2 fromSun = screen - sunScreen;
                    float along = dot(fromSun, axis);
                    float across = dot(fromSun, normal);
                    // Reference-inspired layered warm-white veiling glare. Broad
                    // low-opacity gradients preserve the underlying star detail.
                    float3 ghosts = float3(.11,.08,.058) *
                        exp(-dot(fromSun, fromSun) / .48);
                    ghosts += float3(.045,.032,.025) *
                        exp(-(along * along / .85 + across * across / .18));
                    // The dominant near-source ghost has separated orange, yellow
                    // and green/blue shoulders rather than a flat colored disc.
                    float2 warm = screen - sunScreen * .67;
                    ghosts += OpticalGhost(warm, .11, float3(.85,.18,.015));
                    ghosts += OpticalGhost(warm + axis * .018, .062, float3(.58,.58,.025));
                    ghosts += OpticalGhost(warm - normal * .055 - axis * .025,
                        .052, float3(.025,.16,.12));
                    ghosts += OpticalGhost(screen - sunScreen * .43 + normal * .006,
                        .043, float3(.18,.065,.018));
                    ghosts += OpticalGhost(screen - sunScreen * .26,
                        .032, float3(.16,.035,.025));
                    ghosts += OpticalGhost(screen - sunScreen * .07 - normal * .008,
                        .065, float3(.10,.012,.045));
                    ghosts += OpticalGhost(screen + sunScreen * .31,
                        .052, float3(.07,.012,.065));
                    ghosts += OpticalGhost(screen + sunScreen * .71 + normal * .012,
                        .12, float3(.075,.008,.04));
                    ghosts += OpticalGhost(screen + sunScreen * 1.03,
                        .19, float3(.06,.004,.025));
                    // Fine, irregular pinpoints and halos between the larger ghosts.
                    [unroll] for (int g = 0; g < 7; g++)
                    {
                        float t = .12 + g * .137 + (g % 2) * .027;
                        float2 p = screen - sunScreen * (1 - t * 1.8)
                            + normal * ((g % 3) - 1) * .008;
                        float size = .003 + (g % 3) * .0018;
                        float d = dot(p, p) / (size * size);
                        float3 tint = lerp(float3(.34,.12,.025),
                            float3(.20,.08,.32), t);
                        ghosts += tint * (exp(-d * 2) * .65 + exp(-d * .12) * .10);
                    }
                    // Several blue-violet threads fan from the source. Each has
                    // a narrow core and a much softer surrounding gradient.
                    float window = exp(-along * along / 2.5);
                    float thread1 = across + along * .19;
                    float thread2 = across - along * .23;
                    float thread3 = across + along * .15;
                    ghosts += float3(.055,.075,.24) * window *
                        (exp(-abs(thread1) / .003) * .55 + exp(-abs(thread1) / .018) * .20);
                    ghosts += float3(.085,.035,.18) * window *
                        (exp(-abs(thread2) / .0025) * .5 + exp(-abs(thread2) / .014) * .18);
                    ghosts += float3(.04,.065,.16) * window *
                        exp(-abs(thread3) / .002) * .22;
                    color += ghosts * edge * _SolarOptics.x;
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
