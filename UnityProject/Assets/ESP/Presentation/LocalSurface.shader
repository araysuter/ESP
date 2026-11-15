Shader "ESP/Local Surface Imagery"
{
    Properties
    {
        _BaseMap ("Geographic imagery", 2D) = "white" {}
        _Opacity ("Coverage opacity", Range(0,1)) = 1
        _PatchUv ("Tile in patch", Vector) = (0,0,1,0)
        _SunDirection ("Direction to sun", Vector) = (0,1,0,0)
        _SunColor ("Sunlight", Color) = (1,1,1,1)
        _CameraAltitude ("Camera altitude meters", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _PatchUv, _SunDirection, _SunColor;
                float _Opacity, _CameraAltitude;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 imagery = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Empty satellite cells must reveal the bundled globe, not black squares.
                clip(max(imagery.r, max(imagery.g, imagery.b)) - .0003);
                float2 patchUv = (input.uv + _PatchUv.xy) * _PatchUv.z;
                float edge = min(min(patchUv.x, patchUv.y), min(1 - patchUv.x, 1 - patchUv.y));
                half alpha = imagery.a * _Opacity * smoothstep(0, .08, edge);
                clip(alpha - .002);
                float3 normal = normalize(input.normalWS);
                float sun = dot(normal, normalize(_SunDirection.xyz));
                half daylight = smoothstep(-.08, .15, sun);
                half3 color = imagery.rgb * (.015 + _SunColor.rgb * max(0, sun));
                // Presentation-only aerial perspective, not sampled visibility/weather.
                // The overlay runs after Base-camera atmosphere, so its distant
                // ground must receive its own dense, low-angle air path. Keep it
                // opaque: fading alpha would expose the same aliased globe below.
                float3 towardCamera = _WorldSpaceCameraPos - input.positionWS;
                float distanceMeters = length(towardCamera);
                float airDensity = exp(-max(_CameraAltitude, 0) / 18000);
                float baseHaze = (1 - exp(-distanceMeters / 60000)) * airDensity;
                float viewElevation = saturate(dot(normal, towardCamera / max(distanceMeters, .001)));
                float grazing = 1 - smoothstep(.015, .12, viewElevation);
                // No added horizon haze inside 400 m. It becomes strong only
                // beyond the detailed pad footprint and near the visual horizon.
                float horizonHaze = grazing * smoothstep(400, 3500, distanceMeters) * airDensity;
                half haze = daylight * min(.985, 1 - (1 - baseHaze) * (1 - horizonHaze));
                color = lerp(color, half3(.26, .48, .90), haze);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
