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

        public bool Show(Texture density, Shader shader, float displayOpacity = .22f)
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
        /// Display-only newest-valid dated observation per pixel. RG stores
        /// fraction * support, support, so filtered missing pixels stay distinct
        /// from observed clear pixels. Callers retain the original coverage mask.
        /// </summary>
        public sealed class ObservationBlend
        {
            readonly Vector2[] field, fallback;
            readonly int[] ages, fallbackAges;
            readonly int width, height;

            public ObservationBlend(int width, int height)
            {
                this.width = width; this.height = height;
                field = new Vector2[width * height];
                ages = new int[field.Length];
                fallback = new Vector2[field.Length];
                fallbackAges = new int[field.Length];
                for (int i = 0; i < ages.Length; i++) ages[i] = fallbackAges[i] = int.MaxValue;
            }

            public int Add(Color32[] source, int daysOld, bool[] observed, out int unknown)
            {
                unknown = 0; int selected = 0;
                for (int i = 0; i < source.Length; i++)
                {
                    if (TryCloudFraction(source[i], out byte percent))
                    {
                        observed[i] = true;
                        // A valid zero is an observation and wins in the interior;
                        // boundary crossfades are display-only. Equal-age ties retain
                        // the first observation; no implied sub-day ordering.
                        float support = source[i].a / 255f;
                        var sample = new Vector2(percent * .01f * support, support);
                        if (daysOld < ages[i])
                        {
                            fallback[i] = field[i]; fallbackAges[i] = ages[i];
                            field[i] = sample; ages[i] = daysOld; selected++;
                        }
                        else if (daysOld > ages[i] && daysOld < fallbackAges[i])
                        {
                            fallback[i] = sample; fallbackAges[i] = daysOld;
                        }
                    }
                    else if (source[i].a > 0 &&
                        !(source[i].r == 192 && source[i].g == 192 && source[i].b == 192)) unknown++;
                }
                return selected;
            }

            const int FeatherRadius = 32;

            // Latitude reflection continues over the pole on the opposite meridian.
            int Index(int x, int y)
            {
                while (y < 0 || y >= height)
                {
                    if (y < 0) y = -y - 1;
                    else y = 2 * height - y - 1;
                    x += width / 2;
                }
                return y * width + (x % width + width) % width;
            }

            public Color32[] Finish()
            {
                // Seed the newer side only where its next-older overlapping sample
                // matches the date selected on the adjacent side. Expand within the
                // same date pair; no missing or unrelated date is synthesized.
                var distance = new byte[field.Length];
                var queue = new int[field.Length];
                for (int i = 0; i < distance.Length; i++) distance[i] = byte.MaxValue;
                int head = 0, tail = 0;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x, older = fallbackAges[i];
                    if (older == int.MaxValue) continue;
                    if (ages[Index(x - 1, y)] == older || ages[Index(x + 1, y)] == older ||
                        ages[Index(x, y - 1)] == older || ages[Index(x, y + 1)] == older)
                    {
                        distance[i] = 0; queue[tail++] = i;
                    }
                }
                while (head < tail)
                {
                    int i = queue[head++], d = distance[i];
                    if (d >= FeatherRadius) continue;
                    int x = i % width, y = i / width;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int j = Index(x + (direction == 0 ? -1 : direction == 1 ? 1 : 0),
                            y + (direction == 2 ? -1 : direction == 3 ? 1 : 0));
                        if (distance[j] != byte.MaxValue || ages[j] != ages[i] ||
                            fallbackAges[j] != fallbackAges[i]) continue;
                        distance[j] = (byte)(d + 1); queue[tail++] = j;
                    }
                }
                var pixels = new Color32[field.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    float t = distance[i] == byte.MaxValue ? 0 : 1f - distance[i] / (float)FeatherRadius;
                    t = t * t * (3f - 2f * t);
                    Vector2 value = Vector2.Lerp(field[i], fallback[i], t);
                    pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(value.x) * 255),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(value.y) * 255), 0, 255);
                }
                return pixels;
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
