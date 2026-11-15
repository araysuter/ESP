Shader "ESP/ObservedCloudShell"
{
    Properties
    {
        _CloudDensity ("Observed cloud fraction", 2D) = "black" {}
        _Opacity ("Display opacity", Range(0, 1)) = 0.38
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
                // Preserve daytime appearance. At night there is only a small
                // readability fill; clouds must not glow white over dark oceans.
                half illumination = lerp(.30h, .006h, night) + .70h * saturate(solarElevationSine);
                half3 white = half3(0.94h, 0.97h, 1.0h) * illumination;
                // Cloud fraction is not optical thickness: use a bounded display
                // approximation. Full night coverage is opaque so sharp ground
                // lights never bleed through an overcast layer. Missing observed
                // support remains transparent; daytime opacity stays unchanged.
                half coverage = saturate(density * support);
                half clear = 1 - coverage;
                half nightOpacity = 1 - clear * clear * clear * clear;
                half opacity = lerp(coverage * _Opacity, nightOpacity, night);
                return half4(white, opacity);
            }
            ENDHLSL
        }
    }
}
