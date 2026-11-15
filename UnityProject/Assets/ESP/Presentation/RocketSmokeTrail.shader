// Original ESP procedural smoke artwork; contains no KSP textures or shader code.
Shader "ESP/Procedural SRB Smoke"
{
    Properties { _Daylight ("Daylight", Range(0, 1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Daylight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 lifeSeed : TEXCOORD1; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 lifeSeed : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv; o.lifeSeed = input.lifeSeed; o.color = input.color; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), f.x), lerp(Hash(c + float2(0, 1)), Hash(c + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float2 q = p * 3.2 + float2(input.lifeSeed.y, input.lifeSeed.y * .7 - input.lifeSeed.x * 1.8);
                float n = .56 * Noise(q) + .29 * Noise(q * 2.07 + 8) + .15 * Noise(q * 4.11 - 3);
                float a = (1 - smoothstep(.54 + .23 * n, .79 + .2 * n, length(p))) * (.55 + .65 * n) * input.color.a;
                float roundness = sqrt(saturate(1 - dot(p, p)));
                Light sun = GetMainLight();
                float3 normalWS = normalize(UNITY_MATRIX_V[0].xyz * p.x + UNITY_MATRIX_V[1].xyz * p.y - UNITY_MATRIX_V[2].xyz * roundness);
                float lighting = .24 + .76 * saturate(dot(normalWS, sun.direction) * .65 + .35);
                float3 illumination = .04 + _Daylight * (float3(.08, .09, .10) + sun.color * lighting);
                float3 rgb = input.color.rgb * (.68 + .5 * n) * illumination;
                return half4(rgb * a, a);
            }
            ENDHLSL
        }
    }
}
