Shader "ESP/ObservedCloudShell"
{
    Properties
    {
        _CloudDensity ("Observed cloud fraction", 2D) = "black" {}
        _Opacity ("Display opacity", Range(0, 1)) = 0.22
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
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
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
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            float HashCell(float3 cell)
            {
                cell = frac(cell * float3(.1031, .1030, .0973));
                cell += dot(cell, cell.yzx + 19.19);
                return frac((cell.x + cell.y) * cell.z);
            }

            // Return the spatial gradient as well as the smooth noise value.
            // Sampling it toward the sun approximates lit and shadowed billows.
            float Billow(float3 p, out float3 gradient)
            {
                float3 cell = floor(p);
                float3 t = frac(p);
                float3 w = t * t * (3 - 2 * t);
                float3 slope = 6 * t * (1 - t);
                float4 lower = float4(HashCell(cell), HashCell(cell + float3(1, 0, 0)),
                    HashCell(cell + float3(0, 1, 0)), HashCell(cell + float3(1, 1, 0)));
                float4 upper = float4(HashCell(cell + float3(0, 0, 1)), HashCell(cell + float3(1, 0, 1)),
                    HashCell(cell + float3(0, 1, 1)), HashCell(cell + 1));
                float2 lo = lerp(lower.xz, lower.yw, w.x);
                float2 hi = lerp(upper.xz, upper.yw, w.x);
                float low = lerp(lo.x, lo.y, w.y);
                float high = lerp(hi.x, hi.y, w.y);
                gradient = slope * float3(
                    lerp(lerp(lower.y - lower.x, lower.w - lower.z, w.y),
                        lerp(upper.y - upper.x, upper.w - upper.z, w.y), w.z),
                    lerp(lo.y - lo.x, hi.y - hi.x, w.z), high - low);
                return lerp(low, high, w.z);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Render only the near face from orbit, only the inner face below
                // the 10 km display layer. Two-sided rendering must not double-blend
                // the far hemisphere through the near cloud surface.
                float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 normalizedCamera = cameraOS / float3(6.388137, 6.3667523, 6.388137);
                bool inside = dot(normalizedCamera, normalizedCamera) < 1;
                bool front = IS_FRONT_VFACE(face, true, false);
                if (front == inside) discard;
                // RG is fraction * support, support. Normalize AFTER filtering,
                // including mipmaps, so missing data is never averaged as clear sky.
                // Leave UVs unwrapped here: sampler Repeat maintains seam derivatives.
                half2 observation = SAMPLE_TEXTURE2D(_CloudDensity, sampler_CloudDensity, input.uv).rg;
                half density = saturate(observation.r / max(observation.g, 0.00392h));
                half support = smoothstep(0.03h, 0.50h, observation.g);
                Light sun = GetMainLight();
                half solarElevationSine = dot(normalize(input.normalWS), sun.direction);
                half night = 1 - smoothstep(-.1391731h, -.0174524h, solarElevationSine);
                half coverage = saturate(density * support);
                half clear = 1 - coverage;
                half nightOpacity = 1 - clear * clear * clear * clear;
                half3 nightColor = half3(.94h, .97h, 1) * (.006h + .70h * saturate(solarElevationSine));
                // Fade out fine structure as its screen footprint becomes subpixel.
                float3 noisePosition = input.positionOS * 320;
                float footprint = max(length(ddx(noisePosition)), length(ddy(noisePosition)));
                float detail = 1 - smoothstep(.25, .85, footprint);
                if (coverage <= 0) return 0;
                if (night >= .999h) return half4(nightColor, nightOpacity);

                float sunlight = .34 + .66 * saturate(solarElevationSine);
                if (detail <= 0)
                {
                    // Sum of the four vertical weights. Distant pixels can use
                    // the average column without evaluating the subpixel billows.
                    float distantOpacity = 1 - exp2(-9.2 * _Opacity * density * coverage);
                    half3 distantColor = half3(.99h, .995h, 1) * (.78 * sunlight);
                    return half4(lerp(distantColor, nightColor, night), lerp(distantOpacity, nightOpacity, night));
                }

                // Approximate a 5 km layer centered on the display shell using four
                // view-ray samples. The observed texture still sets the footprint.
                float3 towardCamera = normalize(cameraOS - input.positionOS);
                float facing = dot(towardCamera, normalize(input.normalOS));
                float3 stepRay = towardCamera * (.005 / max(abs(facing), .23));
                float3 sunOS = TransformWorldToObjectDir(sun.direction);
                float shapeThreshold = lerp(.77, .18, density);
                float transmission = 1;
                float brightness = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float offset = .375 - i * .25;
                    float height = .5 + offset * (facing >= 0 ? 1 : -1);
                    float3 gradient;
                    float noise = Billow(noisePosition + stepRay * (320 * offset), gradient);
                    float shape = smoothstep(shapeThreshold - .10, shapeThreshold + .16, noise);
                    float densityAtSample = lerp(density, shape, detail) * coverage;
                    float thickness = densityAtSample * (1 - .85 * abs(2 * height - 1));
                    float opacity = 1 - exp2(-4 * _Opacity * thickness);
                    float lightFacing = saturate(.5 - dot(gradient, sunOS) * .35 * detail);
                    float lit = sunlight * lerp(.52, 1.05, saturate(.65 * height + .35 * lightFacing));
                    brightness += transmission * opacity * lit;
                    transmission *= 1 - opacity;
                }

                float dayOpacity = 1 - transmission;
                float dayLight = brightness / max(dayOpacity, .0001);
                half3 dayColor = half3(.99h, .995h, 1) * dayLight;
                return half4(lerp(dayColor, nightColor, night), lerp(dayOpacity, nightOpacity, night));
            }
            ENDHLSL
        }
    }
}
