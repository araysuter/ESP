Shader "ESP/Polar Aurora"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Name "AuroralCurtains"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _EspAuroraPhase, _EspAuroraBrightness, _EspAuroraColor, _EspAuroraStorm;
            float4 _EspSunDirectionEcef;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float angle = input.uv.x * 2 * PI;
                // The auroral oval gently undulates; an exaggerated storm expands it equatorward.
                float shift = .018 * sin(5 * angle + _EspAuroraPhase) + .012 * sin(11 * angle - 2 * _EspAuroraPhase)
                    - max(0, _EspAuroraStorm - 1) * .16;
                float3 north = normalize(float3(-p.x * p.y, p.x * p.x + p.z * p.z, -p.z * p.y));
                p += north * shift;
                output.positionCS = TransformObjectToHClip(p);
                output.positionOS = p;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float angle = input.uv.x * 2 * PI;
                float h = input.uv.y;
                float phase = _EspAuroraPhase;
                float ray = .5 + .5 * sin(89 * angle + 2 * sin(7 * angle + phase) - 2 * phase);
                float folds = .5 + .5 * sin(23 * angle + 2.2 * sin(4 * angle - phase) + phase);
                float shimmer = .65 + .35 * sin(37 * angle + 3 * phase) * sin(11 * angle - 2 * phase);
                float envelope = smoothstep(0, .13, h) * (1 - smoothstep(.65, 1, h));
                float density = envelope * (pow(ray, 5) * .65 + pow(folds, 3) * .24 + .08) * shimmer;
                float variation = .5 + .5 * sin(3 * angle + phase + 1.3 * sin(7 * angle - 2 * phase));
                float3 green = float3(.08, 1.0, .40);
                float3 violet = float3(.65, .12, 1.0);
                float3 red = float3(1.0, .12, .20);
                float3 color = lerp(green, violet, saturate((1 - h) * .7 * variation));
                color = lerp(color, red, smoothstep(.38, .95, h) * (.35 + .5 * variation));
                float gray = dot(color, float3(.2126, .7152, .0722));
                color = max(0, lerp(gray.xxx, color, _EspAuroraColor));
                // Daylight washes out the display; no observed activity is implied.
                float daylight = dot(normalize(input.positionOS), normalize(_EspSunDirectionEcef.xyz));
                float night = 1 - smoothstep(-.12, .12, daylight);
                float strength = max(0, _EspAuroraStorm);
                return half4(color * density * night * _EspAuroraBrightness * strength * .7, 0);
            }
            ENDHLSL
        }
    }
}
