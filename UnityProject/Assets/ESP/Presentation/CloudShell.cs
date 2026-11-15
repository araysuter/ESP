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

        public bool Show(Texture density, Shader shader, float displayOpacity = .38f)
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
            readonly Vector2[] field;
            readonly int[] ages;
            readonly int width, height;

            public ObservationBlend(int width, int height)
            {
                this.width = width; this.height = height;
                field = new Vector2[width * height];
                ages = new int[field.Length];
                for (int i = 0; i < ages.Length; i++) ages[i] = int.MaxValue;
            }

            public int Add(Color32[] source, int daysOld, bool[] observed, out int unknown)
            {
                unknown = 0; int selected = 0;
                for (int i = 0; i < source.Length; i++)
                {
                    if (TryCloudFraction(source[i], out byte percent))
                    {
                        observed[i] = true;
                        // A valid zero is an observation and must prevent an older
                        // cloudy pixel from showing through. Equal-age ties retain
                        // the first observation; no implied sub-day ordering.
                        if (daysOld < ages[i])
                        {
                            float support = source[i].a / 255f;
                            field[i] = new Vector2(percent * .01f * support, support);
                            ages[i] = daysOld; selected++;
                        }
                    }
                    else if (source[i].a > 0 &&
                        !(source[i].r == 192 && source[i].g == 192 && source[i].b == 192)) unknown++;
                }
                return selected;
            }

            const int FeatherRadius = 8;

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
                var horizontal = new Vector2[field.Length];
                var smooth = new Vector2[field.Length];
                var boundary = new float[field.Length];
                const int r = FeatherRadius, span = 2 * r + 1;
                // Mark both sides of a date or missing-data boundary. The raw mask
                // is kept in the x component of the second temporary buffer.
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x, age = ages[i];
                    if (ages[Index(x - 1, y)] != age || ages[Index(x + 1, y)] != age ||
                        ages[Index(x, y - 1)] != age || ages[Index(x, y + 1)] != age)
                        smooth[i].x = 1;
                }
                // Horizontal running sums of premultiplied RG and boundary mask.
                for (int y = 0; y < height; y++)
                {
                    Vector2 sum = Vector2.zero; float mask = 0;
                    for (int x = -r; x <= r; x++) { int j = Index(x, y); sum += field[j]; mask += smooth[j].x; }
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x; horizontal[i] = sum / span; boundary[i] = mask / span;
                        int add = Index(x + r + 1, y), remove = Index(x - r, y);
                        sum += field[add] - field[remove]; mask += smooth[add].x - smooth[remove].x;
                    }
                }
                // Vertical running sums; reflection preserves continuity at poles.
                // Reuse smooth for the final filtered field and boundary influence.
                for (int x = 0; x < width; x++)
                {
                    Vector2 sum = Vector2.zero; float mask = 0;
                    for (int y = -r; y <= r; y++) { int j = Index(x, y); sum += horizontal[j]; mask += boundary[j]; }
                    for (int y = 0; y < height; y++)
                    {
                        int i = y * width + x;
                        Vector2 filtered = sum / span;
                        float influence = Mathf.Clamp01(mask / span * span * .5f);
                        smooth[i] = Vector2.Lerp(field[i], filtered, influence);
                        int add = Index(x, y + r + 1), remove = Index(x, y - r);
                        sum += horizontal[add] - horizontal[remove]; mask += boundary[add] - boundary[remove];
                    }
                }
                var pixels = new Color32[field.Length];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(smooth[i].x) * 255),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(smooth[i].y) * 255), 0, 255);
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
