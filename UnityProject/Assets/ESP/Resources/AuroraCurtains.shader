Shader "ESP/Aurora Curtains"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+80" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Name "AuroraEmission"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Aurora; // simulated Kp, saturation/range, brightness, mission seconds
                float4 _AuroraSun;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 radial : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };
            // Integer angular frequencies close the oval seamlessly. Smooth waves
            // fold the ENTIRE curtain; no point sprites, billboards or particle dots.
            float Fold(float a, float t)
            {
                return .027*sin(5*a+t*.09) + .018*sin(11*a-t*.06)
                    + .009*sin(23*a + 1.8*sin(3*a+t*.04)) + .004*sin(47*a-t*.12);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float t = _Aurora.w, band = v.uv.z, h = v.uv.y;
                float a = v.uv.x * TWO_PI;
                float phase = a + band*.37 + (v.uv.w < 0 ? 1.2 : 0);
                // Kp expands the oval equatorward (~72 to 51 magnetic degrees).
                float latitude = radians(72 - _Aurora.x*2.3 + band*2.2) + Fold(phase,t);
                latitude += h*.008*sin(9*phase + t*.03);
                float3 magnetic = float3(cos(latitude)*cos(a), sin(latitude)*v.uv.w, cos(latitude)*sin(a));
                // Fixed tilted dipole, north ~80.6N, 72.7W. Artistic centered oval.
                const float3 pole = float3(.0486,.9866,-.1559);
                const float3 east = float3(.9549,0,.2969);
                float3 across = cross(pole,east);
                float3 radial = magnetic.x*east + magnetic.y*pole + magnetic.z*across;
                float baseHeight = .102 + .008*sin(7*phase+t*.05) + band*.007;
                float height = baseHeight + h*(.19 + .045*sin(4*phase-t*.03) + .035*_Aurora.x/9);
                float3 position = radial * (float3(6.378137,6.3567523,6.378137) + height);
                o.positionCS = TransformObjectToHClip(position);
                o.positionWS = TransformObjectToWorld(position);
                o.uv = float4(phase,h,band,a);
                o.radial = radial;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float a=i.uv.x, h=i.uv.y, t=_Aurora.w, band=i.uv.z;
                float activity = .12 + .88*pow(.5+.5*sin(3*a+t*.035 + sin(7*a-t*.02)),1.6);
                // Fine field-aligned rays retain their identity along height, with
                // a little shear. Derivative filtering suppresses orbital shimmer.
                float rayPhase = a*480 + 9*sin(19*a-t*.12) + t*.7 + h*.7*sin(13*a);
                float fine = .5+.5*sin(rayPhase);
                fine = lerp(fine,.5,saturate(fwidth(rayPhase)*.25));
                float rays = .48 + .22*sin(83*a + 3*sin(13*a)+t*.14) + .13*sin(137*a-t*.09) + .17*fine;
                rays = max(.12,rays);
                float lower = smoothstep(0,.16,h);
                float edge = exp(-pow((h-.12)/.11,2));
                float rayHeight = .72 + .28*(.5+.5*sin(37*a + sin(17*a)+t*.06));
                float veil = exp(-h*2.2)*(1-smoothstep(.5*rayHeight,rayHeight,h));
                float density = lower*(edge*.40 + veil*.95)*rays*activity;
                float greenToRed = smoothstep(.12,.66,h);
                float variety = .5+.5*sin(4*a-t*.035 + 1.4*sin(9*a+t*.02));
                float boost = _Aurora.y;
                float3 green = float3(.17,1,.32);
                float3 red = lerp(float3(1,.065,.16),float3(.68,.10,1),variety*saturate(boost*.62));
                float3 color = lerp(green,red,greenToRed*saturate(.42+boost*.36));
                // Violet fringes are sparse and continuous, not colored speckles.
                color = lerp(color,float3(.42,.12,1),pow(variety,6)*exp(-h*12)*saturate(boost-.7)*.45);
                float luminance = dot(color,float3(.2126,.7152,.0722));
                color = max(0,lerp(luminance.xxx,color,boost));
                color /= max(1,max(color.r,max(color.g,color.b)));
                float night = 1-smoothstep(-.16,.06,dot(normalize(i.radial),normalize(_AuroraSun.xyz+float3(0,1e-6,0))));
                float strength = (.22+.78*_Aurora.x/9) * (1-band*.2);
                // A thin sheet otherwise reveals a hard silhouette where a fold
                // turns edge-on. Soften that silhouette like a finite-width veil.
                float3 sheetNormal = normalize(cross(ddx(i.positionWS),ddy(i.positionWS)));
                float facing = abs(dot(sheetNormal,GetWorldSpaceNormalizeViewDir(i.positionWS)));
                float softFold = smoothstep(0,.22,facing);
                // Bounded emission preserves color at high intensity without a
                // full-screen bloom pass. Atmosphere composites after these sheets.
                return half4(color*density*strength*night*softFold*_Aurora.z*.90,0);
            }
            ENDHLSL
        }
    }
}
