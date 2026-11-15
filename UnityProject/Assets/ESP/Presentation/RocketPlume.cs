using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Cosmetic exhaust only. Call SetFlow with simulated engine output and local
    /// ambient pressure. Anchors point along local -Y; the fallback sits at the
    /// vehicle base. No force, fuel, or weather is calculated here.
    /// </summary>
    public sealed class RocketPlume : MonoBehaviour
    {
        public enum PlumeStyle { Methane, Hydrolox, Solid }
        public PlumeStyle style = PlumeStyle.Methane;
        public int stageIndex;
        public bool solidBooster;
        public Transform[] nozzles;
        public Shader plumeShader;
        [Tooltip("Optional serialized overrides. Bundled Waterfall Resources textures are used when empty.")]
        public Texture2D plumeNoise1, plumeNoise2;
        [Range(0, 1)] public float textureNoiseStrength = .35f;
        public bool UsesWaterfallTextures { get; private set; }
        private static bool warnedMissingNoise;
        private static bool loggedNoiseBinding;
        [Min(.01f)] public float nozzleRadius = 1f;
        [Min(.01f)] public float plumeLength = 12f;
        private readonly List<Transform> visuals = new List<Transform>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<float> seeds = new List<float>();
        private Mesh mesh;
        private Material material;
        private MaterialPropertyBlock properties;
        private float throttle, pressure = 101325f;
        private float appliedThrottle = -1f, appliedVacuum = -1f;

        public static bool IsExhaustRenderer(Renderer renderer) => renderer != null && renderer.sharedMaterial != null &&
            renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.name == "ESP/ProceduralPlume";

        private void Awake()
        {
            if (plumeShader == null) plumeShader = Shader.Find("ESP/ProceduralPlume");
            if (plumeShader == null) { Debug.LogWarning("ESP plume shader unavailable"); enabled = false; return; }
            material = new Material(plumeShader) { name = "ESP procedural plume (runtime)" };
            // Resource assets are explicitly included by Unity's player build;
            // existing prefab builders need no new scene/material assignments.
            // These images retain their own CC BY-NC-SA 4.0 license/notice.
            if (plumeNoise1 == null) plumeNoise1 = Resources.Load<Texture2D>("Waterfall/fx-noise-1");
            if (plumeNoise2 == null) plumeNoise2 = Resources.Load<Texture2D>("Waterfall/fx-noise-2");
            UsesWaterfallTextures = plumeNoise1 != null && plumeNoise2 != null;
            if (UsesWaterfallTextures)
            {
                material.SetTexture("_NoiseTex1", plumeNoise1);
                material.SetTexture("_NoiseTex2", plumeNoise2);
                if (!loggedNoiseBinding)
                {
                    Debug.Log("ESP_WATERFALL_TEXTURES_BOUND " + plumeNoise1.name + "=" + plumeNoise1.width + "x" + plumeNoise1.height
                        + " " + plumeNoise2.name + "=" + plumeNoise2.width + "x" + plumeNoise2.height + " shader=" + plumeShader.name);
                    loggedNoiseBinding = true;
                }
            }
            else if (!warnedMissingNoise)
            {
                Debug.LogWarning("ESP Waterfall plume textures missing; using procedural exhaust fallback");
                warnedMissingNoise = true;
            }
            material.SetFloat("_TextureNoiseStrength", UsesWaterfallTextures ? Mathf.Clamp01(textureNoiseStrength) : 0f);
            properties = new MaterialPropertyBlock();
            mesh = BuildMesh();
            // Starship's importer creates one component per nozzle; other presets
            // group their nozzles. Count the stage across both layouts so broad
            // overlapping fans do not become an opaque-looking white cylinder.
            var vehicle = GetComponentInParent<RocketPresentation>();
            Transform groupRoot = vehicle != null ? vehicle.transform : transform.root;
            int nozzleCount = 0;
            foreach (var plume in groupRoot.GetComponentsInChildren<RocketPlume>(true))
            {
                if (plume.stageIndex != stageIndex || plume.style != style || plume.solidBooster != solidBooster) continue;
                if (plume.nozzles == null || plume.nozzles.Length == 0) nozzleCount++;
                else foreach (var anchor in plume.nozzles) if (anchor != null) nozzleCount++;
            }
            properties.SetFloat("_ClusterGain", 1f / Mathf.Sqrt(Mathf.Max(1, nozzleCount)));
            if (nozzles == null || nozzles.Length == 0)
            {
                var fallback = new GameObject("Fallback nozzle").transform;
                fallback.SetParent(transform, false);
                nozzles = new[] { fallback };
            }
            foreach (Transform nozzle in nozzles)
            {
                if (nozzle == null) continue;
                var child = new GameObject("Procedural plume");
                child.transform.SetParent(nozzle, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                visuals.Add(child.transform); renderers.Add(renderer);
                Vector3 anchorPosition = nozzle.localPosition;
                seeds.Add(anchorPosition.x * 1.73f + anchorPosition.y * .79f + anchorPosition.z * 2.39f + seeds.Count * 2.399963f);
            }
            Apply();
        }

        /// <param name="fraction">Actual engine flow fraction, zero when engine is off or fuel is exhausted.</param>
        /// <param name="ambientPressurePascals">Ambient static pressure in pascals.</param>
        public void SetFlow(float fraction, float ambientPressurePascals)
        {
            throttle = float.IsNaN(fraction) || float.IsInfinity(fraction) ? 0 : Mathf.Clamp01(fraction);
            pressure = float.IsNaN(ambientPressurePascals) || float.IsInfinity(ambientPressurePascals) ? 101325 : Mathf.Max(0, ambientPressurePascals);
            if (material != null) Apply();
        }
        private void Apply()
        {
            // Art response to pressure, not a nozzle-flow solver. The fractional
            // power preserves a gradual change through the upper atmosphere.
            float vacuum = 1f - Mathf.Pow(Mathf.Clamp01(pressure / 101325f), .35f);
            if (!(throttle == 0 && appliedThrottle > 0) && Mathf.Abs(appliedThrottle - throttle) < .0005f &&
                Mathf.Abs(appliedVacuum - vacuum) < .0005f) return;
            appliedThrottle = throttle; appliedVacuum = vacuum;
            Color core = style == PlumeStyle.Hydrolox ? new Color(.76f, .88f, 1f) : style == PlumeStyle.Solid ? new Color(1f, .94f, .78f) : new Color(.85f, .94f, 1f);
            Color sheath = style == PlumeStyle.Hydrolox ? new Color(.12f, .36f, 1f) : style == PlumeStyle.Solid ? new Color(1f, .27f, .025f) : new Color(.26f, .32f, 1f);
            Color tail = style == PlumeStyle.Solid ? new Color(1f, .12f, .012f) : style == PlumeStyle.Hydrolox ? new Color(.16f, .31f, .8f) : new Color(.75f, .22f, .36f);
            properties.SetFloat("_Throttle", throttle);
            properties.SetFloat("_Vacuum", vacuum);
            properties.SetColor("_CoreColor", core);
            properties.SetColor("_SheathColor", sheath);
            properties.SetColor("_TailColor", tail);
            properties.SetFloat("_MachStrength", style == PlumeStyle.Solid ? .08f : .8f);
            properties.SetFloat("_Density", style == PlumeStyle.Solid ? 1.1f : .65f);
            float length = Mathf.Max(.01f, plumeLength) * (.28f + .72f * Mathf.Sqrt(throttle)) * Mathf.Lerp(1f, 2.2f, vacuum);
            // Longer coherent launch flame, tapering the extra length toward vacuum.
            // Existing pressure expansion and mesh bounds use this final length.
            length *= Mathf.Lerp(2.3f, 1.2f, vacuum);
            float radius = Mathf.Max(.01f, nozzleRadius);
            // Approximately 26 degrees of far-field half-angle at full vacuum.
            // This is an art parameter, not an inferred nozzle exit pressure.
            // Express the spread in meters before normalizing by the exit radius,
            // so a small bell can still produce a broad vacuum exhaust fan.
            float expansion = length * .48f / radius;
            properties.SetFloat("_Expansion", expansion);
            // Shader displaces X/Z only. Atmospheric cells fit within 1.5 radii;
            // the vacuum fan is bounded by 1 + expansion, plus <=3% ripple.
            float boundRadius = Mathf.Max(1.5f, 1f + expansion) * 1.04f;
            mesh.bounds = new Bounds(new Vector3(0, -.5f, 0), new Vector3(boundRadius * 2f, 1.02f, boundRadius * 2f));
            for (int i = 0; i < visuals.Count; i++)
            {
                visuals[i].localScale = new Vector3(radius, length, radius);
                properties.SetFloat("_Seed", seeds[i]);
                renderers[i].SetPropertyBlock(properties);
                renderers[i].enabled = throttle > .001f;
            }
        }
        private static Mesh BuildMesh()
        {
            // Rounded envelope avoids intersecting luminous cards. The shader
            // supplies the soft core and sheath; no particles or lights per engine.
            // 1600 triangles per nozzle; one shared mesh and material per group.
            const int sides = 20, segments = 40;
            var vertices = new Vector3[(sides + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * segments * 6];
            int t = 0;
            for (int row = 0; row <= segments; row++)
            {
                float v = (float)row / segments;
                for (int side = 0; side <= sides; side++)
                {
                    float u = (float)side / sides, angle = u * Mathf.PI * 2f;
                    int i = row * (sides + 1) + side;
                    vertices[i] = new Vector3(Mathf.Cos(angle), -v, Mathf.Sin(angle));
                    uv[i] = new Vector2(u, v);
                    if (row == segments || side == sides) continue;
                    int next = i + sides + 1;
                    triangles[t++] = i; triangles[t++] = next; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = next; triangles[t++] = next + 1;
                }
            }
            var result = new Mesh { name = "Original exhaust envelope" };
            result.vertices = vertices; result.uv = uv; result.triangles = triangles;
            // Apply supplies conservative bounds for the current length/radius.
            result.bounds = new Bounds(new Vector3(0, -.5f, 0), new Vector3(3.12f, 1.02f, 3.12f));
            return result;
        }
        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
