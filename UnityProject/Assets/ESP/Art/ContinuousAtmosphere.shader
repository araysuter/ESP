Shader "ESP/Continuous Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
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
                output.positionCS = float4(input.positionOS.xy, 0, 1);
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
                return float2((-b - root) / a, (-b + root) / a);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 origin = _CameraPlanet.xyz;
                float3 ray = normalize(_ForwardPlanet.xyz +
                    input.screen.x * _ViewParams.x * _ViewParams.y * _RightPlanet.xyz +
                    input.screen.y * _ViewParams.x * _UpPlanet.xyz);
                float2 outer = IntersectEllipsoid(origin, ray, EarthRadii + .1);
                float begin = max(0, outer.x), end = outer.y;
                if (end <= begin) return 0;
                float2 ground = IntersectEllipsoid(origin, ray, EarthRadii);
                bool hitsGround = ground.y >= 0 && ground.x >= 0;
                if (hitsGround) end = min(end, ground.x);
                // An underground camera must not integrate through Earth's interior.
                // Camera controls own collision; this only keeps the visual finite.
                if (dot(origin / EarthRadii, origin / EarthRadii) < .999999) return 0;
                float lengthThroughAir = max(0, end - begin);
                if (lengthThroughAir < .0000001) return 0;
                float3 sun = normalize(_SunPlanet.xyz);
                float opticalColumn = 0, illuminatedColumn = 0;
                // Fixed work: eight quadrature points, no nested light march, textures,
                // history buffer or per-frame allocation. Earth blocks solar rays.
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float t = begin + lengthThroughAir * ((i + .5) / 8.0);
                    float3 samplePosition = origin + t * ray;
                    float height = max(0, (length(samplePosition / EarthRadii) - 1) * 6.371);
                    float density = exp(-height / .008);
                    float2 sunGround = IntersectEllipsoid(samplePosition, sun, EarthRadii);
                    float lit = (sunGround.x > .000001 && sunGround.y > sunGround.x) ? 0 : 1;
                    opticalColumn += density;
                    illuminatedColumn += density * lit;
                }
                opticalColumn *= lengthThroughAir / 8;
                illuminatedColumn *= lengthThroughAir / 8;
                // RGB wavelength dependence is an appearance approximation, not
                // measured local aerosol/weather. Density scale height is 8 km.
                float3 opticalDepth = opticalColumn * float3(5.8, 13.5, 33.1);
                float litFraction = illuminatedColumn / max(opticalColumn, .0000001);
                float mu = dot(ray, sun);
                float phase = .65 + .35 * mu * mu;
                float scatter = (1 - exp(-opticalDepth.b * 2.5)) * litFraction * phase;
                float3 color = float3(.26, .48, .90) * scatter;
                float alpha = 1 - exp(-dot(opticalDepth, float3(.21, .72, .07)));
                if (!hitsGround)
                {
                    float height = max(0, (length(origin / EarthRadii) - 1) * 6.371);
                    float3 up = normalize(origin / (EarthRadii * EarthRadii));
                    float day = smoothstep(-.10, .15, dot(up, sun));
                    // Display exposure suppresses stars through a bright lower sky;
                    // it fades continuously with air column and camera altitude.
                    float skyExposure = day * exp(-height / .025) * (1 - exp(-opticalColumn * 700));
                    alpha = max(alpha, .995 * skyExposure);
                }
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
