using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Original cosmetic SRB smoke. ECEF births remain doubles; no force or
    /// terrain/atmosphere state is fed back into the authoritative flight core.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class RocketSmokeTrail : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        public Shader smokeShader;
        public MissionSunlight sunlight;
        public const int Capacity = 256;
        public const double LifetimeSeconds = 26;
        public int ActiveParticleCount { get; private set; }
        public long TotalEmittedCount { get; private set; }
        public double VisualTimeSeconds => visualTime;
        private struct Puff
        {
            public bool alive;
            public Vector3d birth, exhaust, up, drift;
            public double time, height;
            public float size, seed;
        }
        private readonly Puff[] puffs = new Puff[Capacity];
        private readonly SmokeEmissionSchedule schedule = new SmokeEmissionSchedule();
        private readonly Transform[] nozzles = new Transform[2];
        private readonly Vector3d[] previousNozzle = new Vector3d[2];
        private readonly Vector3d[] currentNozzle = new Vector3d[2];
        private readonly Vector3[] centers = new Vector3[Capacity];
        private readonly Vector3[] vertices = new Vector3[Capacity * 4];
        private readonly Vector2[] uv = new Vector2[Capacity * 4];
        private readonly Vector2[] lifeSeed = new Vector2[Capacity * 4];
        private readonly Color[] colors = new Color[Capacity * 4];
        private readonly int[] triangles = new int[Capacity * 6];
        private readonly int[] order = new int[Capacity];
        private readonly float[] depth = new float[Capacity];
        private RocketSimulation observed;
        private int nozzleCount, next;
        private double previousMissionTime, visualTime, latitude, longitude;
        private bool hasPrevious, previousLaunched, missingShaderReported;
        private GameObject root;
        private Mesh mesh;
        private MeshRenderer smokeRenderer;
        private Material material;
        private Comparison<int> depthComparison;

        /// <summary>Call after simulation construction/configuration and before stepping.
        /// Binding samples current pad/nozzle positions; no particles are emitted here.</summary>
        public void Arm(RocketSimulation simulation)
        {
            Clear(); observed = simulation;
            if (simulation == null || director == null) return;
            previousMissionTime = visualTime = simulation.State.MissionTime;
            previousLaunched = simulation.State.IsLaunched;
            var site = LaunchSites.All[director.SelectedSiteIndex];
            latitude = site.Latitude * Math.PI / 180; longitude = site.Longitude * Math.PI / 180;
            if (director.RocketVisual != null)
                foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true))
                {
                    if (!plume.solidBooster || plume.nozzles == null) continue;
                    foreach (var nozzle in plume.nozzles)
                        if (nozzle != null && nozzleCount < nozzles.Length) nozzles[nozzleCount++] = nozzle;
                }
            SampleNozzles(Ecef(simulation));
            for (int i = 0; i < nozzleCount; ++i) previousNozzle[i] = currentNozzle[i];
            hasPrevious = nozzleCount > 0;
        }

        public void Clear()
        {
            Array.Clear(puffs, 0, puffs.Length); Array.Clear(nozzles, 0, nozzles.Length);
            ActiveParticleCount = 0; next = nozzleCount = 0; hasPrevious = false;
            previousMissionTime = visualTime = 0; previousLaunched = false;
            observed = null; schedule.Clear();
            if (smokeRenderer != null) smokeRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            var simulation = driver != null ? driver.Simulation : null;
            if (!ReferenceEquals(simulation, observed)) Arm(simulation);
            if (simulation == null || director == null) return;
            var state = simulation.State;
            if (state.MissionTime < previousMissionTime || (previousLaunched && !state.IsLaunched)) Arm(simulation);
            double dt = Math.Max(0, state.MissionTime - previousMissionTime);
            Vector3d origin = Ecef(simulation);
            SampleNozzles(origin);
            bool paused = driver.IsPaused || MissionSession.BlocksInput;
            if (!paused)
            {
                // In flight, visual age follows true simulated elapsed time (including time rate).
                // After terminal impact the core clock stops: let existing cosmetic smoke finish.
                visualTime += dt > 0 ? dt : state.IsLanded ? Time.unscaledDeltaTime : 0;
                bool burning = director.VehicleIndex == 2 && simulation.BoosterBurning && !state.IsLanded;
                if (burning && dt > 0 && nozzleCount > 0 && hasPrevious)
                {
                    // Atmospheric art response only. No smoke emission in near-vacuum.
                    float density = (float)simulation.Physics.DensityKgPerCubicMeter;
                    double rate = 4 * Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0, density) / 1.225f));
                    var batch = schedule.Advance(dt, rate, 32); // At most 64 puffs per rendered frame.
                    for (int emission = 0; emission < batch.Count; ++emission)
                    {
                        double age = batch.AgeSeconds(emission);
                        if (age >= LifetimeSeconds) continue;
                        double fraction = Math.Max(0, Math.Min(1, 1 - age / dt));
                        for (int nozzle = 0; nozzle < nozzleCount; ++nozzle)
                        {
                            Vector3d birth = previousNozzle[nozzle] + (currentNozzle[nozzle] - previousNozzle[nozzle]) * fraction;
                            Emit(birth, LocalToEcef(-nozzles[nozzle].up * 13), visualTime - age);
                        }
                    }
                }
                else if (!burning) schedule.Clear();
            }
            previousMissionTime = state.MissionTime; previousLaunched = state.IsLaunched;
            for (int i = 0; i < nozzleCount; ++i) previousNozzle[i] = currentNozzle[i];
            hasPrevious = nozzleCount > 0;
            // Preview scheduling gates drawing only: emission, aging and capacity stay identical.
            ActiveParticleCount = 0;
            for (int i = 0; i < Capacity; ++i)
            {
                if (!puffs[i].alive) continue;
                if (visualTime - puffs[i].time >= LifetimeSeconds) { puffs[i].alive = false; continue; }
                ActiveParticleCount++;
            }
            var camera = director.FlightRenderCamera;
            bool draw = ActiveParticleCount > 0 && camera != null && camera.isActiveAndEnabled;
            if (smokeRenderer != null) smokeRenderer.enabled = draw;
            if (draw && EnsureRenderer()) Draw(origin, camera);
        }

        private static Vector3d Ecef(RocketSimulation simulation)
        {
            var state = simulation.State;
            return EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
        }
        private Vector3d LocalToEcef(Vector3 local)
        {
            double sl = Math.Sin(longitude), cl = Math.Cos(longitude), sp = Math.Sin(latitude), cp = Math.Cos(latitude);
            return new Vector3d(-sl * local.x - sp * cl * local.z + cp * cl * local.y,
                cl * local.x - sp * sl * local.z + cp * sl * local.y, cp * local.z + sp * local.y);
        }
        private void SampleNozzles(Vector3d origin)
        {
            // Driver places the vehicle-relative origin at world zero and keeps pad ENU axes.
            // Imported nozzle transforms are metre-scale offsets, not authoritative Earth positions.
            for (int i = 0; i < nozzleCount; ++i)
                if (nozzles[i] != null) currentNozzle[i] = origin + LocalToEcef(nozzles[i].position);
        }
        private void Emit(Vector3d birth, Vector3d exhaust, double time)
        {
            var geo = EarthFrames.EcefToGeodetic(birth);
            var up = new Vector3d(Math.Cos(geo.LatitudeRadians) * Math.Cos(geo.LongitudeRadians),
                Math.Cos(geo.LatitudeRadians) * Math.Sin(geo.LongitudeRadians), Math.Sin(geo.LatitudeRadians));
            float seed = (float)((TotalEmittedCount * .61803398875) % 1);
            double padHeight = LaunchSites.All[director.SelectedSiteIndex].ElevationMeters;
            puffs[next] = new Puff { alive = true, birth = birth, exhaust = exhaust, up = up,
                drift = LocalToEcef(new Vector3(.8f, .45f, .2f)), time = time,
                height = Math.Max(.3, geo.AltitudeMeters - padHeight), size = 2.6f + seed * 1.7f, seed = seed * 79 };
            next = (next + 1) % Capacity; TotalEmittedCount++;
        }
        private bool EnsureRenderer()
        {
            if (smokeRenderer != null) { smokeRenderer.enabled = true; return true; }
            if (smokeShader == null)
            {
                if (!missingShaderReported) Debug.LogError("Assign RocketSmokeTrail.smokeShader in BuildMission.");
                missingShaderReported = true; return false;
            }
            root = new GameObject("SRB smoke — cosmetic ECEF trail");
            root.layer = director.RocketVisual != null ? director.RocketVisual.gameObject.layer : gameObject.layer;
            mesh = new Mesh { name = "ESP bounded SRB smoke" }; mesh.MarkDynamic();
            material = new Material(smokeShader) { name = "ESP original SRB smoke (runtime)" };
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            smokeRenderer = root.AddComponent<MeshRenderer>(); smokeRenderer.sharedMaterial = material;
            smokeRenderer.shadowCastingMode = ShadowCastingMode.Off; smokeRenderer.receiveShadows = false;
            smokeRenderer.lightProbeUsage = LightProbeUsage.Off; smokeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int i = 0; i < Capacity; ++i)
            {
                int v = i * 4, t = i * 6; order[i] = i;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            depthComparison = (a, b) => depth[b].CompareTo(depth[a]);
            return true;
        }
        private void Draw(Vector3d origin, Camera camera)
        {
            if (sunlight == null) sunlight = GetComponent<MissionSunlight>();
            float daylight;
            if (sunlight != null)
                daylight = sunlight.InEarthShadow ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 10, (float)sunlight.SolarElevationDegrees));
            else
                daylight = RenderSettings.sun != null && RenderSettings.sun.enabled ? Mathf.Clamp01(RenderSettings.sun.intensity / 1.5f) : 0;
            material.SetFloat("_Daylight", daylight);
            Vector3 right = camera.transform.right, up = camera.transform.up, forward = camera.transform.forward;
            for (int i = 0; i < Capacity; ++i)
            {
                Puff p = puffs[i];
                double age = Math.Max(0, visualTime - p.time);
                Vector3d offset = p.exhaust * ((1 - Math.Exp(-age * .7)) / .7) + p.drift * age;
                double height = p.height + offset.X * p.up.X + offset.Y * p.up.Y + offset.Z * p.up.Z;
                if (height < .3) offset += p.up * (.3 - height);
                var point = p.birth + offset;
                centers[i] = p.alive ? CameraRelativeCoordinates.FromEcef(point.X, point.Y, point.Z,
                    origin.X, origin.Y, origin.Z, latitude, longitude) : Vector3.zero;
                depth[i] = p.alive ? Vector3.Dot(centers[i], forward) : float.NegativeInfinity;
            }
            Array.Sort(order, depthComparison);
            for (int slot = 0; slot < Capacity; ++slot)
            {
                int index = order[slot]; Puff p = puffs[index];
                float age = (float)Math.Max(0, visualTime - p.time), life = age / (float)LifetimeSeconds;
                // Original bounded growth/opacity art response; no external textures or copied shader.
                float radius = p.size + 5.5f * Mathf.Log(1 + age * .8f);
                Vector3 x = right * radius, y = up * radius;
                int v = slot * 4; Vector3 center = centers[index];
                vertices[v] = center - x - y; vertices[v + 1] = center + x - y;
                vertices[v + 2] = center + x + y; vertices[v + 3] = center - x + y;
                float alpha = p.alive ? .48f * Mathf.Clamp01(age * 5) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, life))) : 0;
                for (int corner = 0; corner < 4; ++corner)
                {
                    colors[v + corner] = new Color(.78f, .76f, .70f, alpha);
                    lifeSeed[v + corner] = new Vector2(life, p.seed);
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv2 = lifeSeed; mesh.RecalculateBounds();
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            Clear();
            if (root != null) Destroy(root); if (mesh != null) Destroy(mesh); if (material != null) Destroy(material);
        }
    }
}
