// Original ESP visual effect; no third-party shader code or textures.
Shader "ESP/ReentryPlasma"
{
 Properties { _Intensity("Intensity", Range(0,1)) = 0 _Flow("Incoming direction", Vector) = (0,-1,0,0) _Wake("Wake", Float) = 0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend One One
   Cull Off
   ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Intensity, _Wake;
   float4 _Flow;
   CBUFFER_END
   struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
   struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 positionWS : TEXCOORD2; };
   V Vert(A i) { V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.uv=i.uv; return o; }
   float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float Noise(float2 p) { float2 a=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y); }
   half4 Frag(V i) : SV_Target
   {
    float2 p=float2(i.uv.x*8, i.uv.y*6-_Time.y*2.1);
    float n=.6*Noise(p)+.3*Noise(p*2.03)+.1*Noise(p*4.07);
    float streak=pow(saturate(n),1.5);
    float wind=pow(saturate(dot(normalize(i.normalWS), normalize(_Flow.xyz))),1.3);
    float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
    float edge=pow(1-saturate(abs(dot(normalize(i.normalWS),view))),1.5);
    float shell=wind*(.12+.95*edge)*(.55+.9*streak);
    float wake=pow(saturate(1-i.uv.y),1.35)*(.18+.9*streak);
    float strength=lerp(shell,wake,saturate(_Wake))*_Intensity;
    float3 hot=lerp(float3(.9,.08,.015),float3(1,.67,.28),saturate(streak*.9+wind*.35));
    return half4(hot*strength*1.25,0);
   }
   ENDHLSL
  }
 }
}
