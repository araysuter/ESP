using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Original, procedural emission sheets in Earth-fixed magnetic ovals.
    /// Display units are megameters. This is an artistic Kp simulation, not a forecast.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class AuroraCurtains : MonoBehaviour
    {
        public const float DefaultKp = 5, DefaultColor = 150, DefaultIntensity = 300;
        public const int Segments = 1536, HeightSteps = 12, Bands = 3;
        private Mesh mesh;
        private Material material;
        private RocketFlightDriver flight;
        private static double idleSeconds;
        private static int clockFrame = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClock() { idleSeconds = 0; clockFrame = -1; }
        internal double? ReviewSeconds;
        internal Vector3? ReviewSun;
        internal Vector3? ReviewSettings;
        public Material CurtainMaterial => material;

        public static AuroraCurtains Create(Transform earth)
        {
            var existing = earth.GetComponentInChildren<AuroraCurtains>(true);
            if (existing) return existing;
            var shader = Resources.Load<Shader>("AuroraCurtains");
            if (!shader || !shader.isSupported)
            { Debug.LogWarning("ESP aurora shader unavailable on this device"); return null; }
            var root = new GameObject("Aurora · simulated magnetic curtains");
            root.layer = earth.gameObject.layer;
            root.transform.SetParent(earth, false);
            var effect = root.AddComponent<AuroraCurtains>();
            effect.flight = FindFirstObjectByType<RocketFlightDriver>();
            effect.mesh = BuildMesh();
            effect.material = new Material(shader) { name = "Aurora emission sheets" };
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = effect.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            return effect;
        }

        public static Mesh BuildMesh()
        {
            int rows = HeightSteps + 1, columns = Segments + 1;
            var vertices = new Vector3[2 * Bands * columns * rows];
            var uv = new Vector4[vertices.Length];
            var triangles = new int[2 * Bands * Segments * HeightSteps * 6];
            int index = 0;
            for (int sheet = 0; sheet < 2 * Bands; sheet++)
            {
                int offset = sheet * rows * columns;
                for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    int v = offset + x * rows + y;
                    // Shader displaces into a WGS84-sized shell; explicit bounds below.
                    uv[v] = new Vector4((float)x / Segments, (float)y / HeightSteps,
                        sheet % Bands, sheet < Bands ? 1 : -1);
                    if (x == Segments || y == HeightSteps) continue;
                    triangles[index++] = v; triangles[index++] = v + rows; triangles[index++] = v + 1;
                    triangles[index++] = v + 1; triangles[index++] = v + rows; triangles[index++] = v + rows + 1;
                }
            }
            var result = new Mesh { name = "Aurora folded sheets", indexFormat = IndexFormat.UInt32 };
            result.vertices = vertices; result.SetUVs(0, uv); result.triangles = triangles;
            result.bounds = new Bounds(Vector3.zero, Vector3.one * 14f);
            result.UploadMeshData(true);
            return result;
        }

        private void LateUpdate()
        {
            if (!material) return;
            if (clockFrame != Time.frameCount)
            {
                clockFrame = Time.frameCount;
                if (!MissionSession.IsSimulationPaused) idleSeconds += Time.unscaledDeltaTime;
            }
            // Actual integrated mission seconds preserve pause, warp, and rate limiting.
            double seconds = ReviewSeconds ?? (flight && flight.HasPosition ? flight.MissionTimeSeconds : idleSeconds);
            var session = MissionSession.Instance;
            Vector3 settings = ReviewSettings ?? new Vector3(session ? session.AuroraKp : DefaultKp,
                session ? session.AuroraColorBoost : DefaultColor, session ? session.AuroraIntensity : DefaultIntensity);
            material.SetVector("_Aurora", new Vector4(settings.x, settings.y / 100, settings.z / 100, (float)(seconds % 86400)));
            material.SetVector("_AuroraSun", ReviewSun ?? (Vector3)Shader.GetGlobalVector("_EspSunDirectionEcef"));
        }
        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
