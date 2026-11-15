Shader "ESP/Procedural Impact"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 lifeSeed : TEXCOORD1; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 lifeSeed : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.lifeSeed = input.lifeSeed; output.color = input.color;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float n = .55 * Noise(p);
                p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6);
                n += .28 * Noise(p + 13.7);
                return n + .17 * Noise(p * 2.03 + 29.1);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                float age = input.lifeSeed.x;
                float kind = floor(input.lifeSeed.y * .01);
                float seed = input.lifeSeed.y - kind * 100;
                float noise = Fbm(p * 3.8 + float2(seed, seed * .73 - age * 2));
                float edge = 1 - smoothstep(.54 + noise * .22, .78 + noise * .21, radius);
                float density = saturate(edge * (.5 + noise * .8));
                float alpha = density * input.color.a;
                float3 rgb = input.color.rgb;
                if (kind < .5)
                {
                    // Rolling bright interior, amber outer filaments, charred pockets as it cools.
                    float heat = saturate((1 - age) * .86 + noise * .55 - radius * .3);
                    rgb = lerp(float3(.09, .025, .012), float3(1.9, .23, .018), smoothstep(.2, .6, heat));
                    rgb = lerp(rgb, float3(3.3, 1.45, .23), smoothstep(.58, .83, heat));
                    rgb = lerp(rgb, float3(4.3, 3.5, 1.8), smoothstep(.86, 1, heat));
                }
                else if (kind < 2.5)
                {
                    // Soft pseudo-volume shading, with irregular lobes instead of flat discs.
                    float sphere = sqrt(saturate(1 - dot(p, p)));
                    float light = saturate(.53 + .35 * sphere + .18 * p.y - .12 * p.x);
                    rgb *= (.7 + noise * .6) * light;
                    if (kind < 1.5) rgb += float3(.20, .055, .008) * pow(saturate(1 - age * 4), 2) * noise;
                }
                else if (kind < 3.5)
                {
                    alpha = (1 - smoothstep(.08, .8, radius)) * input.color.a;
                    rgb *= 2.7 * (1 - age * .7);
                }
                else
                {
                    alpha = pow(saturate(1 - radius), 2.5) * input.color.a;
                    rgb *= 4;
                }
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
