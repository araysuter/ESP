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
                float _Throttle, _Vacuum, _MachStrength, _Density, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };
            float Envelope(float along)
            {
                // Exit matches the nozzle radius. Atmospheric cells pinch slightly;
                // vacuum produces a broad fan. The tail remains compact and finite.
                float radius = .96 + lerp(.30, 3.3, _Vacuum) * along;
                float cells = sin(along * 37.6991) * .10 * _MachStrength;
                radius *= 1.0 - cells * (1.0 - along) * (1.0 - _Vacuum);
                return radius * (1.0 - .92 * smoothstep(.58, 1.0, along));
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                float along = input.uv.y;
                float width = Envelope(along);
                float3 radial = float3(input.positionOS.x, 0, input.positionOS.z);
                float3 p = input.positionOS.xyz;
                p.xz *= width;
                // Slope makes the rounded tail readable even end-on. Unity's normal
                // transform accounts for the nozzle radius/length scale ratio.
                float slope = (Envelope(min(1.0, along + .001)) - Envelope(max(0.0, along - .001))) / .002;
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
                float softEdge = smoothstep(.03, .68, facing);
                float projectedRadius = sqrt(saturate(1.0 - facing * facing));
                float tailFade = pow(saturate(1.0 - along), .85) * (1.0 - smoothstep(.72, 1.0, along));
                float core = pow(facing, 7.0) * (1.0 - smoothstep(.12, .83, along));
                // Stationary shock cells with diamond cross-sections. Only fine
                // turbulence advects downstream; the diamonds do not scroll.
                float cell = abs(frac(along * 6.0 + .12) * 2.0 - 1.0);
                float diamond = pow(saturate(1.0 - cell - projectedRadius * .85), 2.0);
                diamond *= _MachStrength * (1.0 - .85 * _Vacuum) * (1.0 - along);
                float phase = along * 115.0 - _Time.y * 24.0 + _Seed;
                float turbulence = .88 + .12 * sin(phase + sin(input.uv.x * 25.1327 + _Time.y * 3.0))
                    * sin(along * 61.0 - _Time.y * 17.0 + _Seed);
                float throat = exp(-along * 28.0) * pow(facing, 3.0);
                float3 sheath = lerp(_SheathColor.rgb, _TailColor.rgb, smoothstep(.28, .9, along));
                // Front and back add to form the volume. No opaque flame surface.
                float3 color = sheath * (.36 * softEdge) + _CoreColor.rgb * (core * .68 + diamond * 1.9 + throat * .55);
                color *= tailFade * turbulence * _Throttle * _Density * .5;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
