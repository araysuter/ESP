using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Artistic northern auroral curtains in the scaled Earth frame; never flight state.</summary>
    public sealed class PolarAurora : MonoBehaviour
    {
        public const double BottomAltitudeMeters = 100000;
        public const double TopAltitudeMeters = 350000;
        private const int Segments = 512, HeightSteps = 12, Curtains = 3;
        private Mesh mesh;
        private Material material;
        private static readonly AuroraClock Clock = new AuroraClock();
        private static int updatedFrame = -1;

        public static PolarAurora Create(Transform earth)
        {
            if (!earth) return null;
            var shader = Resources.Load<Shader>("PolarAurora");
            if (!shader || !shader.isSupported)
            {
                Debug.LogWarning("ESP aurora shader unavailable; Earth remains visible.");
                return null;
            }
            var root = new GameObject("Northern aurora — visual approximation");
            root.layer = earth.gameObject.layer;
            root.transform.SetParent(earth, false);
            var effect = root.AddComponent<PolarAurora>();
            effect.mesh = BuildCurtains();
            effect.material = new Material(shader) { name = "Northern aurora (runtime)" };
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = effect.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return effect;
        }

        // WGS84 geodetic heights in the same million-metre units and ECEF X,Z,Y
        // axes as EarthEllipsoid. Duplicate the seam so the texture coordinates wrap.
        public static Mesh BuildCurtains()
        {
            int columns = Segments + 1, rows = HeightSteps + 1;
            var vertices = new Vector3[Curtains * columns * rows];
            var uv = new Vector2[vertices.Length];
            var indices = new int[Curtains * Segments * HeightSteps * 6];
            int cursor = 0;
            for (int c = 0; c < Curtains; c++)
            for (int x = 0; x <= Segments; x++)
            {
                double longitude = 2 * Math.PI * x / Segments;
                // Irregular, nested oval curtains rather than a flat circular band.
                double latitude = (65.5 + c * 1.65 + 1.35 * Math.Sin(3 * longitude + c)
                    + .55 * Math.Sin(9 * longitude + c * 1.7)) * Math.PI / 180;
                for (int y = 0; y <= HeightSteps; y++)
                {
                    float height = (float)y / HeightSteps;
                    var point = EarthFrames.GeodeticToEcef(latitude, longitude,
                        BottomAltitudeMeters + (TopAltitudeMeters - BottomAltitudeMeters) * height);
                    int i = (c * columns + x) * rows + y;
                    vertices[i] = new Vector3((float)(point.X / 1000000), (float)(point.Z / 1000000), (float)(point.Y / 1000000));
                    uv[i] = new Vector2((float)x / Segments, height);
                    if (x == Segments || y == HeightSteps) continue;
                    int next = i + rows;
                    indices[cursor++] = i; indices[cursor++] = i + 1; indices[cursor++] = next;
                    indices[cursor++] = next; indices[cursor++] = i + 1; indices[cursor++] = next + 1;
                }
            }
            var result = new Mesh { name = "WGS84 northern auroral curtains" };
            result.vertices = vertices; result.uv = uv; result.triangles = indices;
            // Vertex animation shifts the curtains several degrees toward the equator.
            result.bounds = new Bounds(Vector3.zero, Vector3.one * 14f);
            return result;
        }

        private void LateUpdate()
        {
            if (updatedFrame != Time.frameCount)
            {
                updatedFrame = Time.frameCount;
                var driver = FindFirstObjectByType<RocketFlightDriver>();
                var simulation = driver != null ? driver.Simulation : null;
                bool idlePaused = MissionSession.Instance != null && MissionSession.Instance.HasSession && MissionSession.BlocksInput;
                if (driver != null && driver.IsPaused && (MissionSession.Instance == null || MissionSession.Instance.HasSession)) idlePaused = true;
                Clock.Advance(simulation, simulation != null ? simulation.State.MissionTime : 0, Time.unscaledDeltaTime, idlePaused);
                Shader.SetGlobalFloat("_EspAuroraPhase", (float)(Clock.Seconds * 2 * Math.PI / 180));
                var session = MissionSession.Instance;
                Shader.SetGlobalFloat("_EspAuroraBrightness", session != null ? session.AuroraBrightness : 1);
                Shader.SetGlobalFloat("_EspAuroraColor", session != null ? session.AuroraColorIntensity : 1);
                Shader.SetGlobalFloat("_EspAuroraStorm", session != null ? session.AuroraStormStrength : 1);
            }
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }

    /// <summary>Continuous visual clock; flight uses actual simulated time, including warp and pause.</summary>
    public sealed class AuroraClock
    {
        private object previousSimulation;
        private double previousMissionTime;
        public double Seconds { get; private set; }
        public void Advance(object simulation, double missionTime, double realDelta, bool idlePaused)
        {
            if (simulation != null)
            {
                if (ReferenceEquals(simulation, previousSimulation) && missionTime >= previousMissionTime)
                    Seconds += missionTime - previousMissionTime;
                previousMissionTime = missionTime;
            }
            else if (!idlePaused && realDelta > 0) Seconds += realDelta;
            previousSimulation = simulation;
            Seconds %= 180.0; // All shader phase multipliers are integral, so the wrap is seamless.
        }
    }
}
