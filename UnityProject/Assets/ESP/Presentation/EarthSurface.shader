Shader "ESP/Earth Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Day imagery", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = .05
        _Metallic ("Metallic", Range(0,1)) = 0
        _SpecColor ("Specular", Color) = (.2,.2,.2,1)
        _BumpScale ("Normal scale", Float) = 1
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _OcclusionStrength ("Occlusion", Range(0,1)) = 1
        _OcclusionMap ("Occlusion map", 2D) = "white" {}
        _EmissionColor ("Legacy emission", Color) = (0,0,0,1)
        _EmissionMap ("Legacy emission map", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = .5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment EarthFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "EarthNightLights.hlsl"
            half4 EarthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                surfaceData.emission = EspNightEmission(input.uv);
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
