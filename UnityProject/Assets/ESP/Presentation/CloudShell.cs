using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Display-only cloud fraction on a thin WGS84 shell; never flight weather.</summary>
    public sealed class CloudShell : MonoBehaviour
    {
        // Source globe geometry uses one Unity unit per 1,000,000 metres.
        public const float DisplayAltitudeMeters = 10000f;
        private GameObject shell;
        private Mesh mesh;
        private Material material;
        private Renderer shellRenderer;

        public bool Show(Texture density, Shader shader)
        {
            if (!density) { if (shell) shell.SetActive(false); return true; }
            if (!shell)
            {
                var source = GetComponent<MeshFilter>();
                if (!source || !source.sharedMesh || !shader || !shader.isSupported) return false;
                // Reuse geodetic UVs, winding and normals. A normal offset gives constant
                // geodetic height, rather than scaling the Earth into a larger sphere.
                mesh = Instantiate(source.sharedMesh);
                mesh.name = "WGS84 clouds — display altitude 10 km";
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                if (normals.Length != vertices.Length) { Destroy(mesh); mesh = null; return false; }
                for (int i = 0; i < vertices.Length; i++) vertices[i] += normals[i] * (DisplayAltitudeMeters / 1000000f);
                mesh.vertices = vertices; mesh.RecalculateBounds();
                shell = new GameObject("Observed NASA cloud fraction shell");
                shell.layer = gameObject.layer;
                shell.transform.SetParent(transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                material = new Material(shader) { name = "Observed cloud fraction (runtime)" };
                shellRenderer.sharedMaterial = material;
            }
            material.SetTexture("_CloudDensity", density);
            shell.SetActive(true);
            return true;
        }

        public void Hide() { if (shell) shell.SetActive(false); }

        // NASA's official MODIS_Cloud_Fraction v1.3 colormap, inspected 2026-11-14:
        // https://gibs.earthdata.nasa.gov/colormaps/v1.3/MODIS_Cloud_Fraction.xml
        // Exact palette inversion precedes interpolation. Unknown RGB, alpha=0 and
        // the transparent fill code (192,192,192, sourceValue 127) remain missing.
        private static readonly Dictionary<int, byte> Fractions = BuildPalette();
        private static Dictionary<int, byte> BuildPalette()
        {
            var result = new Dictionary<int, byte>(101);
            AddRange(result, 0, 5, 102, 0, 119, 1);
            AddRange(result, 6, 11, 183, 15, 141, 1);
            AddRange(result, 12, 18, 0, 0, 100, 1);
            AddRange(result, 19, 24, 0, 0, 170, 1);
            AddRange(result, 25, 30, 0, 0, 255, 1);
            AddRange(result, 31, 37, 0, 136, 238, 0);
            AddRange(result, 38, 43, 0, 80, 0, 0);
            AddRange(result, 44, 49, 0, 136, 0, 0);
            AddRange(result, 50, 56, 0, 220, 0, 0);
            AddRange(result, 57, 62, 255, 255, 0, 2);
            AddRange(result, 63, 68, 240, 190, 64, 2);
            AddRange(result, 69, 75, 187, 136, 0, 2);
            AddRange(result, 76, 81, 122, 90, 3, 2);
            AddRange(result, 82, 87, 110, 0, 0, 2);
            AddRange(result, 88, 94, 170, 0, 0, 2);
            AddRange(result, 95, 100, 255, 0, 0, 2);
            return result;
        }
        private static void AddRange(Dictionary<int, byte> result, int first, int last, int r, int g, int b, int channel)
        {
            for (int value = first; value <= last; value++)
            {
                int offset = value - first;
                int red = r + (channel == 0 ? offset : 0);
                int green = g + (channel == 1 ? offset : 0);
                int blue = b + (channel == 2 ? offset : 0);
                result.Add((red << 16) | (green << 8) | blue, (byte)value);
            }
        }
        public static bool TryCloudFraction(Color32 pixel, out byte percent)
        {
            percent = 0;
            return pixel.a > 0 && Fractions.TryGetValue((pixel.r << 16) | (pixel.g << 8) | pixel.b, out percent);
        }
        private void OnDestroy()
        {
            if (shell) Destroy(shell);
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
