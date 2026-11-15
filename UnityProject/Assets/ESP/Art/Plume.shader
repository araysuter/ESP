// Original ESP URP shader. Waterfall noise images are separately licensed
// CC BY-NC-SA 4.0; see StreamingAssets/Waterfall-NOTICE.txt. No Waterfall
// shader source, KSP includes, assemblies or compiled shaders are used here.
Shader "ESP/ProceduralPlume"
{
    Properties
    {
        _CoreColor ("Core", Color) = (0.85,0.94,1,1)
        _SheathColor ("Sheath", Color) = (0.26,0.32,1,1)
        _TailColor ("Tail", Color) = (0.75,0.22,0.36,1)
        _Throttle ("Flow", Range(0,1)) = 0
        _Vacuum ("Vacuum expansion", Range(0,1)) = 0
        _MachStrength ("Shock diamonds", Range(0,1)) = 0.8
        _Density ("Optical density", Range(0,2)) = 0.65
        _Seed ("Nozzle variation", Float) = 0
        _Expansion ("Vacuum radial spread in nozzle radii", Float) = 12
        _ClusterGain ("Overlapping nozzle fan gain", Range(0,1)) = 1
        _NoiseTex1 ("Waterfall fine breakup", 2D) = "gray" {}
        _NoiseTex2 ("Waterfall broad breakup", 2D) = "gray" {}
        _TextureNoiseStrength ("Texture breakup", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Exhaust"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor, _SheathColor, _TailColor;
                float _Throttle, _Vacuum, _MachStrength, _Density, _Seed, _Expansion, _ClusterGain;
                float _TextureNoiseStrength;
            CBUFFER_END
            TEXTURE2D(_NoiseTex1); SAMPLER(sampler_NoiseTex1);
            TEXTURE2D(_NoiseTex2); SAMPLER(sampler_NoiseTex2);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };
            float Envelope(float along, float around)
            {
                // Exit stays at the actual bell radius. Air confines a compact
                // shock-cell jet; vacuum continues expanding downstream. Fade
                // radiance instead of pinching the vacuum fan back into a tube.
                float cells = sin(along * 37.6991) * .08 * _MachStrength;
                float confined = (1.0 + .28 * along) * (1.0 - cells * (1.0 - along));
                confined *= 1.0 - .94 * smoothstep(.58, 1.0, along);
                float expanded = 1.0 + _Expansion * pow(along, 1.05);
                float ripple = 1.0 + .03 * along * sin(around * 18.84956 + along * 14.0 - _Time.y * 4.0 + _Seed);
                return lerp(confined, expanded, _Vacuum) * ripple;
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                float along = input.uv.y;
                float width = Envelope(along, input.uv.x);
                float3 radial = float3(input.positionOS.x, 0, input.positionOS.z);
                float3 p = input.positionOS.xyz;
                p.xz *= width;
                // Slope makes the rounded tail readable even end-on. Unity's normal
                // transform accounts for the nozzle radius/length scale ratio.
                float lo = max(0.0, along - .001), hi = min(1.0, along + .001);
                float slope = (Envelope(hi, input.uv.x) - Envelope(lo, input.uv.x)) / (hi - lo);
                output.normalWS = TransformObjectToWorldNormal(float3(radial.x, slope, radial.z));
                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float along = input.uv.y;
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing = abs(dot(normalize(input.normalWS), view));
                // Suppress the mesh silhouette for a soft luminous envelope.
                float softEdge = pow(smoothstep(.02, .76, facing), 1.5);
                float projectedRadius = sqrt(saturate(1.0 - facing * facing));
                float tailFade = pow(saturate(1.0 - along), lerp(.85, .48, _Vacuum))
                    * (1.0 - smoothstep(.82, 1.0, along));
                // The near-nozzle hot core remains short as the outer fan widens.
                float core = pow(facing, 7.0) * (1.0 - smoothstep(.025, lerp(.83, .26, _Vacuum), along));
                // Stationary shock cells with diamond cross-sections. Only fine
                // turbulence advects downstream; the diamonds do not scroll.
                float cell = abs(frac(along * 6.0 + .12) * 2.0 - 1.0);
                float diamond = pow(saturate(1.0 - cell - projectedRadius * .85), 2.0);
                diamond *= _MachStrength * (1.0 - _Vacuum) * (1.0 - along);
                float phase = along * 115.0 - _Time.y * 24.0 + _Seed;
                float turbulence = .88 + .12 * sin(phase + sin(input.uv.x * 25.1327 + _Time.y * 3.0))
                    * sin(along * 61.0 - _Time.y * 17.0 + _Seed);
                float throat = exp(-along * 28.0) * pow(facing, 3.0);
                float filaments = .82 + .18 * sin(input.uv.x * 18.84956 + along * 29.0 - _Time.y * 7.0 + _Seed);
                // Two small upstream noise images replace regular-looking bands
                // with subtle downstream breakup. Integer angular tiling keeps
                // the envelope seam continuous. The hot core stays stable.
                float2 noiseUV1 = float2(input.uv.x * 2.0 + _Seed * .11 + _Time.y * .04,
                    along * 2.8 - _Time.y * 1.15);
                float2 noiseUV2 = float2(input.uv.x * 3.0 + _Seed * .17 - _Time.y * .03,
                    along * 6.0 - _Time.y * .61);
                float noise1 = SAMPLE_TEXTURE2D(_NoiseTex1, sampler_NoiseTex1, noiseUV1).r;
                float noise2 = SAMPLE_TEXTURE2D(_NoiseTex2, sampler_NoiseTex2, noiseUV2).r;
                float textureNoise = noise1 * .65 + noise2 * .35;
                float breakup = lerp(1.0, .55 + .9 * textureNoise,
                    _TextureNoiseStrength * smoothstep(.02, .25, along));
                float tailMix = smoothstep(.28, .9, along) * (1.0 - .28 * _Vacuum);
                float3 sheath = lerp(_SheathColor.rgb, _TailColor.rgb, tailMix);
                // Front and back add to form the volume. No opaque flame surface.
                // Only the wide overlapping outer fans receive cluster attenuation;
                // individual nozzle cores remain legible at ignition and staging.
                float diffuseGain = lerp(1.0, _ClusterGain, _Vacuum);
                float dilution = rsqrt(1.0 + _Vacuum * _Expansion * along * .06);
                float3 color = sheath * (.44 * softEdge * filaments * diffuseGain * dilution * breakup)
                    + _CoreColor.rgb * (core * lerp(.68, 1.25, _Vacuum) + diamond * 1.9 + throat * .55);
                color *= tailFade * turbulence * _Throttle * _Density * .5;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
