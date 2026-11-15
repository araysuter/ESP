Shader "ESP/Continuous Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Name "ContinuousAir"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _CameraPlanet, _ForwardPlanet, _RightPlanet, _UpPlanet, _SunPlanet, _ViewParams;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 screen : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Interior clip depth works with reversed-Z Metal as well as GL;
                // never put this always-tested quad exactly on a clipping plane.
                output.positionCS = float4(input.positionOS.xy, .5, 1);
                output.screen = input.positionOS.xy;
                return output;
            }
            // Units are millions of meters; axes match source WGS84 mesh X,Z,Y.
            static const float3 EarthRadii = float3(6.378137, 6.356752314245, 6.378137);
            float2 IntersectEllipsoid(float3 origin, float3 direction, float3 radii)
            {
                float3 o = origin / radii, d = direction / radii;
                float a = dot(d, d), b = dot(o, d), c = dot(o, o) - 1;
                float discriminant = b * b - a * c;
                if (discriminant < 0) return float2(1, -1);
                float root = sqrt(max(0, discriminant));
                // Stable near-surface root avoids subtracting nearly equal numbers.
                float q = -b - (b >= 0 ? root : -root);
                if (abs(q) < 1e-12) return float2(-b / a, -b / a);
                float t0 = q / a, t1 = c / q;
                return float2(min(t0, t1), max(t0, t1));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 origin = _CameraPlanet.xyz;
                float3 ray = normalize(_ForwardPlanet.xyz +
                    input.screen.x * _ViewParams.x * _ViewParams.y * _RightPlanet.xyz +
                    // Match the GPU projection flip when URP renders to a Metal target.
                    input.screen.y * _ProjectionParams.x * _ViewParams.x * _UpPlanet.xyz);
                float2 outer = IntersectEllipsoid(origin, ray, EarthRadii + .1);
                float begin = max(0, outer.x), end = outer.y;
                if (end <= begin) return 0;
                float2 ground = IntersectEllipsoid(origin, ray, EarthRadii);
                bool hitsGround = ground.y >= 0 && ground.x >= 0;
                if (hitsGround) end = min(end, ground.x);
                // CPU clamps only the visual integration origin above the ellipsoid.
                // No all-sky rejection for a sub-meter float rounding discrepancy.
                float lengthThroughAir = max(0, end - begin);
                if (lengthThroughAir < .0000001) return 0;
                float3 sun = normalize(_SunPlanet.xyz);
                // Coefficients are in inverse million metres, for representative
                // RGB wavelengths (680, 550, 440 nm). These are an RGB clear-air
                // approximation, not a spectral or multiple-scattering solution.
                const float3 betaRayleigh = float3(5.8, 13.5, 33.1);
                const float betaMieScattering = 3.0;
                const float betaMieExtinction = 4.4;
                const float invFourPi = .07957747155;
                float mu = clamp(dot(ray, sun), -1.0, 1.0);
                float rayleighPhase = 3.0 * invFourPi / 4.0 * (1.0 + mu * mu);
                // Henyey-Greenstein: mu=1 looks toward the Sun.
                const float g = .76;
                float miePhase = invFourPi * (1.0 - g * g) /
                    pow(max(1.0 + g * g - 2.0 * g * mu, .001), 1.5);
                float2 viewColumn = 0;
                float3 rayleighIntegral = 0, mieIntegral = 0;
                // Concentrate the bounded eight-point view quadrature toward
                // closest approach in ellipsoid-normalized space, where density
                // is generally highest. This approximates geodetic altitude.
                float3 normalizedOrigin = origin / EarthRadii;
                float3 normalizedRay = ray / EarthRadii;
                float closest = clamp(-dot(normalizedOrigin, normalizedRay) /
                    dot(normalizedRay, normalizedRay), begin, end);
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float t0, t1;
                    if (closest <= begin + .0000001)
                    {
                        float u0 = i / 8.0, u1 = (i + 1.0) / 8.0;
                        t0 = begin + lengthThroughAir * u0 * u0;
                        t1 = begin + lengthThroughAir * u1 * u1;
                    }
                    else if (closest >= end - .0000001)
                    {
                        float u0 = 1.0 - i / 8.0, u1 = 1.0 - (i + 1.0) / 8.0;
                        t0 = end - lengthThroughAir * u0 * u0;
                        t1 = end - lengthThroughAir * u1 * u1;
                    }
                    else if (i < 4)
                    {
                        float u0 = 1.0 - i / 4.0, u1 = 1.0 - (i + 1.0) / 4.0;
                        t0 = closest - (closest - begin) * u0 * u0;
                        t1 = closest - (closest - begin) * u1 * u1;
                    }
                    else
                    {
                        float u0 = (i - 4.0) / 4.0, u1 = (i - 3.0) / 4.0;
                        t0 = closest + (end - closest) * u0 * u0;
                        t1 = closest + (end - closest) * u1 * u1;
                    }
                    float stepLength = t1 - t0;
                    float t = .5 * (t0 + t1);
                    float3 samplePosition = origin + t * ray;
                    float height = max(0, (length(samplePosition / EarthRadii) - 1) * 6.371);
                    float2 density = exp(-height / float2(.008, .0012));
                    // Midpoint view optical depth from camera to this sample.
                    float2 sampleViewColumn = viewColumn + density * (stepLength * .5);
                    viewColumn += density * stepLength;
                    float2 sunGround = IntersectEllipsoid(samplePosition, sun, EarthRadii);
                    if (sunGround.x > .000001 && sunGround.y > sunGround.x) continue;
                    float sunDistance = max(0, IntersectEllipsoid(samplePosition, sun, EarthRadii + .1).y);
                    float2 sunColumn = 0;
                    // Quadratically spaced intervals resolve dense air near the
                    // sample; this also bounds work at low solar elevations.
                    [unroll] for (int j = 0; j < 6; j++)
                    {
                        float u0 = j / 6.0, u1 = (j + 1.0) / 6.0;
                        float s0 = sunDistance * u0 * u0, s1 = sunDistance * u1 * u1;
                        float3 lightPosition = samplePosition + sun * (.5 * (s0 + s1));
                        float lightHeight = max(0, (length(lightPosition / EarthRadii) - 1) * 6.371);
                        sunColumn += exp(-lightHeight / float2(.008, .0012)) * (s1 - s0);
                    }
                    float2 totalColumn = sampleViewColumn + sunColumn;
                    float3 transmittance = exp(-betaRayleigh * totalColumn.x - betaMieExtinction * totalColumn.y);
                    rayleighIntegral += density.x * transmittance * stepLength;
                    mieIntegral += density.y * transmittance * stepLength;
                }
                // Fixed display exposure; no claim of absolute radiometric units.
                float3 color = 20.0 * (betaRayleigh * rayleighPhase * rayleighIntegral +
                    betaMieScattering * miePhase * mieIntegral);
                float3 viewTransmission = exp(-betaRayleigh * viewColumn.x - betaMieExtinction * viewColumn.y);
                // Blend state has scalar alpha, so this is luminance extinction,
                // not wavelength-dependent attenuation of the background.
                float alpha = 1.0 - dot(viewTransmission, float3(.2126, .7152, .0722));
                // Daylight star visibility belongs to the celestial background;
                // boosting alpha here also incorrectly hides the Sun and glare.
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
