using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Cosmetic pad gas and dust. Local offsets are artwork, never flight state.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class LaunchGroundEffects : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        public Shader smokeShader;
        public const int Capacity = 384;
        public int ActiveParticleCount { get; private set; }
        private struct Puff { public bool alive, dust; public double time; public float seed; public Vector3 radial; }
        private readonly Puff[] puffs = new Puff[Capacity];
        private readonly SmokeEmissionSchedule schedule = new SmokeEmissionSchedule();
        private readonly Vector3[] vertices = new Vector3[Capacity * 4], centers = new Vector3[Capacity];
        private readonly Vector2[] uv = new Vector2[Capacity * 4], lifeSeed = new Vector2[Capacity * 4];
        private readonly Color[] colors = new Color[Capacity * 4];
        private readonly int[] triangles = new int[Capacity * 6], order = new int[Capacity];
        private readonly float[] depth = new float[Capacity];
        private Comparison<int> compareDepth;
        private RocketSimulation observed;
        private Vector3d pad;
        private double latitude, longitude, previousTime, clock, ignition = -1, lastPowered = -1;
        private int next;
        private long emitted;
        private GameObject root;
        private Mesh mesh;
        private Material material;
        private MeshRenderer cloudRenderer;

        private void ResetCloud(RocketSimulation simulation)
        {
            Array.Clear(puffs, 0, puffs.Length); schedule.Clear();
            next = 0; emitted = 0; ActiveParticleCount = 0; ignition = lastPowered = -1;
            observed = simulation; clock = previousTime = simulation != null ? simulation.State.MissionTime : 0;
            if (cloudRenderer != null) cloudRenderer.enabled = false;
            if (director == null || simulation == null) return;
            var site = LaunchSites.All[director.SelectedSiteIndex];
            latitude = site.Latitude * Math.PI / 180; longitude = site.Longitude * Math.PI / 180;
            pad = EarthFrames.GeodeticToEcef(latitude, longitude, site.ElevationMeters);
        }

        private void LateUpdate()
        {
            var simulation = driver != null ? driver.Simulation : null;
            if (!ReferenceEquals(simulation, observed) || (simulation != null && simulation.State.MissionTime < previousTime)) ResetCloud(simulation);
            if (simulation == null || director == null) return;
            var state = simulation.State;
            double dt = Math.Max(0, state.MissionTime - previousTime);
            bool paused = driver.IsPaused || MissionSession.BlocksInput;
            var origin = EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            float height = (float)(state.AltitudeMeters - LaunchSites.All[director.SelectedSiteIndex].ElevationMeters);
            // Only the first-stage plume near this pad can drive ground impingement.
            bool powered = state.IsLaunched && !state.IsLanded && state.StageIndex == 0 &&
                simulation.Physics.ThrustNewtons > 0 && height < 220 && (origin - pad).Magnitude() < 300 &&
                simulation.ActualPitchDegrees > 45;
            if (!paused)
            {
                clock += dt > 0 ? dt : state.IsLanded ? Time.unscaledDeltaTime : 0;
                if (powered && dt > 0)
                {
                    if (ignition < 0) ignition = Math.Max(0, clock - dt);
                    lastPowered = clock;
                    float strength = Mathf.Clamp01(1 - Mathf.Max(0, height - 35) / 185);
                    var batch = schedule.Advance(dt, 24 * strength, 32);
                    for (int i = 0; i < batch.Count; i++)
                    {
                        double birth = clock - batch.AgeSeconds(i);
                        if (clock - birth >= 32) continue;
                        float seed = (float)((emitted++ * .61803398875) % 1);
                        float angle = seed * Mathf.PI * 2;
                        puffs[next] = new Puff { alive = true, dust = emitted % 3 == 0, time = birth, seed = seed,
                            radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) };
                        next = (next + 1) % Capacity;
                    }
                }
                else schedule.Clear();
            }
            previousTime = state.MissionTime;
            ActiveParticleCount = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (puffs[i].alive && clock - puffs[i].time >= (puffs[i].dust ? 18 : 32)) puffs[i].alive = false;
                if (puffs[i].alive) ActiveParticleCount++;
            }
            var camera = director.FlightRenderCamera;
            bool draw = ActiveParticleCount > 0 && camera != null && camera.isActiveAndEnabled;
            if (cloudRenderer != null) cloudRenderer.enabled = draw;
            if (draw && EnsureRenderer()) Draw(origin, camera);
        }

        // A train of outward-moving pressure fronts, with a decaying oscillatory wake.
        // 85 m/s is an artistic pacing choice, not a physical acoustic solver.
        public static Vector2 ShockMotion(float radius, double time, double start, double stop, float seed)
        {
            if (start < 0 || stop < start) return Vector2.zero;
            double arrival = time - start - Math.Max(0, radius) / 85;
            if (arrival < 0) return Vector2.zero;
            double pulse = Math.Floor(arrival / 1.15);
            double lastPulse = Math.Floor((stop - start) / 1.15);
            pulse = Math.Min(pulse, lastPulse);
            double wake = arrival - pulse * 1.15;
            float envelope = (float)Math.Exp(-wake * 3.5) * Mathf.Exp(-radius / 240);
            return new Vector2(envelope * (2.5f + 1.4f * Mathf.Sin((float)wake * 23 + seed * 6)),
                envelope * Mathf.Sin((float)wake * 31 + seed * 9) * 1.6f);
        }

        private bool EnsureRenderer()
        {
            if (cloudRenderer != null) return true;
            if (smokeShader == null) return false;
            root = new GameObject("Launch gas and shock-stirred dust (cosmetic)");
            root.layer = director.RocketVisual != null ? director.RocketVisual.gameObject.layer : gameObject.layer;
            mesh = new Mesh { name = "Bounded launch cloud" }; mesh.MarkDynamic();
            material = new Material(smokeShader);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            cloudRenderer = root.AddComponent<MeshRenderer>(); cloudRenderer.sharedMaterial = material;
            cloudRenderer.shadowCastingMode = ShadowCastingMode.Off; cloudRenderer.receiveShadows = false;
            cloudRenderer.lightProbeUsage = LightProbeUsage.Off; cloudRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            compareDepth = (a, b) => depth[b].CompareTo(depth[a]);
            return true;
        }

        private void Draw(Vector3d origin, Camera camera)
        {
            var sunlight = GetComponent<MissionSunlight>();
            float daylight = sunlight != null ? sunlight.InEarthShadow ? 0 :
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 10, (float)sunlight.SolarElevationDegrees)) : 1;
            material.SetFloat("_Daylight", daylight);
            Vector3 padLocal = CameraRelativeCoordinates.FromEcef(pad.X, pad.Y, pad.Z, origin.X, origin.Y, origin.Z, latitude, longitude);
            for (int i = 0; i < Capacity; i++)
            {
                var p = puffs[i]; float age = (float)Math.Max(0, clock - p.time);
                float radius = 8 + (p.dust ? 23 : 16) * 4 * (1 - Mathf.Exp(-age / 4)) + age * 1.5f;
                Vector2 shock = p.dust ? ShockMotion(radius, clock, ignition, lastPowered, p.seed) : Vector2.zero;
                // Gases roll upward; dust hugs the pad and shakes as each front arrives.
                float y = p.dust ? 2 + age * .3f + shock.y : 7 + age * (2.8f + p.seed * 1.8f);
                centers[i] = padLocal + p.radial * (radius + shock.x) + Vector3.up * Mathf.Max(1, y);
                depth[i] = p.alive ? Vector3.Dot(centers[i] - camera.transform.position, camera.transform.forward) : float.NegativeInfinity;
                order[i] = i;
            }
            Array.Sort(order, compareDepth);
            for (int slot = 0; slot < Capacity; slot++)
            {
                int i = order[slot], v = slot * 4; var p = puffs[i];
                float age = (float)Math.Max(0, clock - p.time), life = age / (p.dust ? 18 : 32);
                float size = (p.dust ? 7 : 9) + (p.dust ? 4 : 6) * Mathf.Sqrt(age);
                Vector3 x = camera.transform.right * size, y = camera.transform.up * size * (p.dust ? .55f : 1);
                Vector3 center = centers[i];
                vertices[v] = center - x - y; vertices[v + 1] = center + x - y;
                vertices[v + 2] = center + x + y; vertices[v + 3] = center - x + y;
                float alpha = p.alive ? .62f * Mathf.Clamp01(age * 4) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1, life))) : 0;
                Color color = p.dust ? new Color(.68f, .51f, .31f, alpha) : new Color(.94f, .95f, .96f, alpha);
                for (int c = 0; c < 4; c++) { colors[v + c] = color; lifeSeed[v + c] = new Vector2(life, p.seed * 79); }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv2 = lifeSeed; mesh.RecalculateBounds();
        }

        private void OnDisable() => ResetCloud(null);
        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
