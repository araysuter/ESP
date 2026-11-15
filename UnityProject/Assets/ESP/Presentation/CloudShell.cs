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

        public bool Show(Texture density, Shader shader, float displayOpacity = .68f)
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
            material.SetFloat("_Opacity", Mathf.Clamp01(displayOpacity));
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

        /// <summary>
        /// Bounded, display-only reconstruction of dated observations. RG stores
        /// fraction * support, support; even mipmaps must keep missing != clear.
        /// The original observation mask is counted separately by the caller.
        /// </summary>
        public sealed class ObservationBlend
        {
            readonly int width, height;
            readonly Vector2[] accumulated, samples, scratch;
            readonly float[] support;

            public ObservationBlend(int width, int height)
            {
                this.width = width; this.height = height;
                accumulated = new Vector2[width * height];
                samples = new Vector2[accumulated.Length];
                scratch = new Vector2[accumulated.Length];
                support = new float[accumulated.Length];
            }

            public int Add(Color32[] source, int daysOld, bool[] observed, out int unknown)
            {
                unknown = 0; int valid = 0;
                for (int i = 0; i < source.Length; i++)
                {
                    samples[i] = Vector2.zero;
                    if (TryCloudFraction(source[i], out byte percent))
                    {
                        float weight = source[i].a / 255f;
                        samples[i] = new Vector2(percent * .01f * weight, weight);
                        observed[i] = true; valid++;
                    }
                    else if (source[i].a > 0 &&
                        !(source[i].r == 192 && source[i].g == 192 && source[i].b == 192)) unknown++;
                }
                if (valid == 0) return 0;
                // Two small box filters approximate a tent kernel. Each source day
                // feathers to zero support at its swath edges, rather than abruptly
                // replacing a different day's weather. No large missing region is filled.
                for (int pass = 0; pass < 2; pass++)
                {
                    SmoothLongitude(samples, scratch);
                    SmoothLatitude(scratch, samples);
                }
                float recency = Mathf.Pow(.65f, daysOld);
                for (int i = 0; i < samples.Length; i++)
                {
                    accumulated[i] += samples[i] * recency;
                    support[i] = Mathf.Max(support[i], samples[i].y);
                }
                return valid;
            }

            public Color32[] Finish()
            {
                var field = new Vector2[accumulated.Length];
                for (int i = 0; i < field.Length; i++)
                {
                    float fraction = accumulated[i].y > 0 ? accumulated[i].x / accumulated[i].y : 0;
                    field[i] = new Vector2(fraction * support[i], support[i]);
                }
                // All longitudes meet at each pole. A single validity-weighted ring
                // value prevents the UV fan's wedges from meeting with different colors.
                CollapsePolarRing(field, 0);
                CollapsePolarRing(field, height - 1);
                var pixels = new Color32[field.Length];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(field[i].x) * 255),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(field[i].y) * 255), 0, 255);
                return pixels;
            }

            void SmoothLongitude(Vector2[] input, Vector2[] output)
            {
                for (int y = 0; y < height; y++)
                {
                    float latitude = ((y + .5f) / height - .5f) * Mathf.PI;
                    // Constant angular footprint on the sphere, not stretched polar
                    // scan lines. Running sums keep polar filtering O(pixel count).
                    int radius = Mathf.Min(width / 2 - 1,
                        Mathf.CeilToInt(2 / Mathf.Max(.001f, Mathf.Cos(latitude))));
                    Vector2 sum = Vector2.zero;
                    for (int dx = -radius; dx <= radius; dx++) sum += input[y * width + Wrap(dx)];
                    for (int x = 0; x < width; x++)
                    {
                        output[y * width + x] = sum / (2 * radius + 1);
                        sum += input[y * width + Wrap(x + radius + 1)] - input[y * width + Wrap(x - radius)];
                    }
                }
            }

            void SmoothLatitude(Vector2[] input, Vector2[] output)
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 sum = Vector2.zero;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int sy = y + dy, sx = x;
                        // Crossing a pole continues on the opposite meridian.
                        if (sy < 0) { sy = -sy - 1; sx = Wrap(x + width / 2); }
                        else if (sy >= height) { sy = 2 * height - sy - 1; sx = Wrap(x + width / 2); }
                        sum += input[sy * width + sx];
                    }
                    output[y * width + x] = sum / 5;
                }
            }

            int Wrap(int x) => (x % width + width) % width;
            void CollapsePolarRing(Vector2[] field, int y)
            {
                Vector2 mean = Vector2.zero;
                for (int x = 0; x < width; x++) mean += field[y * width + x];
                mean /= width;
                for (int x = 0; x < width; x++) field[y * width + x] = mean;
            }
        }
        private void OnDestroy()
        {
            if (shell) Destroy(shell);
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
