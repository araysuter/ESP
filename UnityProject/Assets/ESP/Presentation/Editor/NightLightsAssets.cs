using System;
using UnityEditor;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    /// <summary>Explicit serialized bindings preserve offline imagery and player shaders.</summary>
    public static class NightLightsAssets
    {
        public const string TexturePath = "Assets/ESP/Art/Earth/BlackMarble2016Lights.png";
        public static void Configure(Material earth, MissionSunlight sunlight, LocalSurfaceTiles surface)
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new Exception("Missing bundled NASA 2016 night lights");
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 4096;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            // Single-channel BC4: ~5.3 MiB including mipmaps, no RGB block tinting.
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.overridden = true;
            platform.maxTextureSize = 4096;
            platform.format = TextureImporterFormat.BC4;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            sunlight.nightLights = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/EarthSurface.shader");
            surface.offlineLightsShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/OfflineGroundLights.shader");
            if (sunlight.nightLights == null || sunlight.nightLights.width != 4096 || sunlight.nightLights.height != 2048 ||
                shader == null || surface.offlineLightsShader == null)
                throw new Exception("Night-light asset/shader qualification failed");
            earth.shader = shader;
            EditorUtility.SetDirty(earth);
            Debug.Log("ESP_NIGHT_LIGHTS_BOUND NASA-2016 4096x2048 BC4 linear geographic-UV");
        }
    }
}
