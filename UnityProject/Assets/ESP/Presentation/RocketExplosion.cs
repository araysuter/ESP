using System;
using System.Collections.Generic;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Original cosmetic impact effect, driven only by a flight-core destruction edge.
    /// Fuel affects the art direction, not a chemical detonation or damage calculation.
    /// One bounded mesh / material; no physics objects, external textures, or particle emitters.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class RocketExplosion : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        // Serialized by BuildMission so the player build retains this shader.
        public Shader effectShader;
        public int ExplosionCount { get; private set; }
        public bool IsActive { get; private set; }
        public float EffectAgeSeconds { get; private set; }
        public const float DurationSeconds = 12f;

        private const int Count = 209; // 36 fire + 44 smoke + 56 dust + 72 fragments + flash.
        private struct Puff
        {
            public Vector3 position, velocity;
            public float delay, lifetime, size, growth, seed;
            public int kind;
        }
        private readonly Puff[] puffs = new Puff[Count];
        private readonly Vector3[] vertices = new Vector3[Count * 4];
        private readonly Vector2[] uv = new Vector2[Count * 4];
        private readonly Vector2[] lifeSeed = new Vector2[Count * 4];
        private readonly Color[] colors = new Color[Count * 4];
        private readonly int[] indices = new int[Count * 6];
        private readonly int[] order = new int[Count];
        private readonly float[] depths = new float[Count];
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<bool> previousVisibility = new List<bool>();
        private RocketSimulation observed;
        private bool previousDestroyed;
        private double previousMissionTime, padLatitude, padLongitude;
        private Vector3d impactEcef;
        private Quaternion surfaceRotation;
        private GameObject effectRoot;
        private Mesh mesh;
        private MeshRenderer effectRenderer;
        private Material material;
        private Comparison<int> depthComparison;
        private float fuelGlow;

        private void LateUpdate()
        {
            var simulation = driver != null ? driver.Simulation : null;
            if (!ReferenceEquals(simulation, observed))
            {
                Arm(simulation);
            }
            if (simulation == null) return;
            var state = simulation.State;
            // Also tolerate RocketSimulation.Reset() without a new simulation instance.
            if (state.MissionTime < previousMissionTime || (previousDestroyed && !simulation.IsDestroyed))
                Clear();
            bool destroyed = simulation.IsDestroyed;
            if (destroyed && !previousDestroyed) Begin(simulation);
            previousDestroyed = destroyed;
            previousMissionTime = state.MissionTime;
            if (destroyed)
                foreach (var renderer in hidden) if (renderer != null) renderer.enabled = false;
            if (!IsActive) return;
            if (director == null) { Clear(); return; }
            // The flight core freezes at impact. Keep using doubles if presentation origin changes.
            var origin = EarthFrames.InertialToEcef(state.Position,
                Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            effectRoot.transform.position = CameraRelativeCoordinates.FromEcef(impactEcef.X, impactEcef.Y, impactEcef.Z,
                origin.X, origin.Y, origin.Z, padLatitude, padLongitude);
            var camera = director.FlightRenderCamera;
            effectRenderer.enabled = camera != null && camera.isActiveAndEnabled;
            if (effectRenderer.enabled) Draw(camera);
            if (!driver.IsPaused && !MissionSession.BlocksInput)
                EffectAgeSeconds += Time.unscaledDeltaTime;
            if (EffectAgeSeconds >= DurationSeconds)
            {
                IsActive = false;
                effectRenderer.enabled = false;
            }
        }

        /// <summary>Call immediately after creating/configuring a simulation, before its first Step.
        /// Observes the initial non-destroyed state even when a whole failed flight fits in one frame.
        /// Binding an already-destroyed simulation never invents a destruction edge.</summary>
        public void Arm(RocketSimulation simulation)
        {
            Clear();
            observed = simulation;
            previousDestroyed = simulation != null && simulation.IsDestroyed;
            previousMissionTime = simulation != null ? simulation.State.MissionTime : 0;
        }

        /// <summary>Clear visuals and restore only renderer flags this component changed.
        /// Calling this on a destroyed simulation cannot replay its effect. Count is lifetime cumulative.</summary>
        public void Clear()
        {
            for (int i = 0; i < hidden.Count; ++i)
                if (hidden[i] != null) hidden[i].enabled = previousVisibility[i];
            hidden.Clear(); previousVisibility.Clear();
            IsActive = false; EffectAgeSeconds = 0;
            previousDestroyed = driver != null && driver.Simulation != null && driver.Simulation.IsDestroyed;
            if (effectRoot != null) Destroy(effectRoot);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            effectRoot = null; mesh = null; material = null; effectRenderer = null;
        }
        private void Begin(RocketSimulation simulation)
        {
            ExplosionCount++;
            if (director == null) return;
            if (director.RocketVisual != null)
            {
                foreach (var renderer in director.RocketVisual.GetComponentsInChildren<Renderer>(true))
                {
                    hidden.Add(renderer); previousVisibility.Add(renderer.enabled);
                    renderer.enabled = false;
                }
            }
            if (effectShader == null)
            {
                Debug.LogError("ESP impact shader is not assigned; wire RocketExplosion.effectShader in BuildMission.");
                return;
            }
            var state = simulation.State;
            impactEcef = EarthFrames.InertialToEcef(state.Position,
                Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            var site = LaunchSites.All[director.SelectedSiteIndex];
            padLatitude = site.Latitude * Math.PI / 180;
            padLongitude = site.Longitude * Math.PI / 180;
            var location = EarthFrames.EcefToGeodetic(impactEcef);
            var above = EarthFrames.GeodeticToEcef(location.LatitudeRadians, location.LongitudeRadians, location.AltitudeMeters + 1);
            var up = CameraRelativeCoordinates.FromEcef(above.X, above.Y, above.Z,
                impactEcef.X, impactEcef.Y, impactEcef.Z, padLatitude, padLongitude).normalized;
            surfaceRotation = Quaternion.FromToRotation(Vector3.up, up);
            // Include attached upper stages and SRBs, exclude discarded stages.
            // Compare equivalent fuel units on both sides, including fictional presets.
            double fuel = Math.Max(0, state.PropellantKg);
            if (simulation.BoosterAttached) fuel += Math.Max(0, simulation.BoosterPropellantKg);
            for (int i = state.StageIndex + 1; i < driver.StageCount; i++) fuel += driver.StagePropellantCapacityKg(i);
            double capacity = 0;
            for (int i = 0; i < driver.StageCount; i++) capacity += driver.StagePropellantCapacityKg(i);
            capacity += driver.BoosterPropellantCapacityKg ?? 0;
            fuelGlow = capacity > 0 ? Mathf.Clamp01((float)(fuel / capacity)) : 0;
            // The flash billboard is 3 * size across. At full propellant its
            // diameter is 3 * 124 m, the reference height of stacked Starship.
            // A small empty-vehicle impact remains visible. Cosmetic only.
            float size = Mathf.Lerp(8f, 124f, fuelGlow);
            float impulse = Mathf.Clamp((float)simulation.ImpactSpeedMetersPerSecond / 120f, .25f, 2f);
            Populate(size, impulse);
            effectRoot = new GameObject("Impact — cosmetic fire, dust and debris");
            effectRoot.layer = director.RocketVisual != null ? director.RocketVisual.gameObject.layer : gameObject.layer;
            effectRoot.AddComponent<MeshFilter>();
            effectRenderer = effectRoot.AddComponent<MeshRenderer>();
            effectRenderer.shadowCastingMode = ShadowCastingMode.Off;
            effectRenderer.receiveShadows = false;
            effectRenderer.lightProbeUsage = LightProbeUsage.Off;
            effectRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            material = new Material(effectShader) { name = "ESP impact (runtime)" };
            effectRenderer.sharedMaterial = material;
            mesh = new Mesh { name = "ESP bounded impact billboards" };
            mesh.MarkDynamic();
            for (int i = 0; i < Count; ++i)
            {
                int v = i * 4, t = i * 6;
                uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
                uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(0, 1);
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
                order[i] = i;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
            effectRoot.GetComponent<MeshFilter>().sharedMesh = mesh;
            depthComparison = (a, b) => depths[b].CompareTo(depths[a]);
            IsActive = true; EffectAgeSeconds = 0;
        }

        private void Populate(float size, float impulse)
        {
            // Local deterministic PRNG: never alter Unity's gameplay random state.
            var random = new System.Random(6173 + ExplosionCount * 31);
            float R() => (float)random.NextDouble();
            int n = 0;
            for (int kind = 0; kind < 5; ++kind)
            {
                int amount = kind == 0 ? 36 : kind == 1 ? 44 : kind == 2 ? 56 : kind == 3 ? 72 : 1;
                for (int i = 0; i < amount; ++i)
                {
                    float angle = R() * Mathf.PI * 2;
                    var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var p = new Puff { kind = kind, seed = R() * 89, position = radial * size * R() * .18f + Vector3.up * 2 };
                    if (kind == 0)
                    {
                        p.delay = R() * .24f; p.lifetime = Mathf.Lerp(.35f, 2.6f, fuelGlow) * (.7f + R() * .3f);
                        p.size = size * (.28f + R() * .22f); p.growth = size * (.65f + R() * .5f);
                        p.velocity = radial * size * (.4f + R() * .7f) + Vector3.up * size * (.3f + R() * .7f);
                    }
                    else if (kind == 1)
                    {
                        p.delay = .15f + R() * 1.25f; p.lifetime = 8f + R() * 2.5f;
                        p.size = size * (.3f + R() * .22f); p.growth = size * (.6f + R() * .9f);
                        p.velocity = radial * size * (.12f + R() * .3f) + Vector3.up * size * (.35f + R() * .6f);
                    }
                    else if (kind == 2)
                    {
                        p.delay = R() * .12f; p.lifetime = 3.5f + R() * 3.5f;
                        p.size = size * (.15f + R() * .18f); p.growth = size * (.35f + R() * .4f);
                        p.velocity = radial * size * (1 + R() * 1.8f) * Mathf.Sqrt(impulse) + Vector3.up * (2 + R() * 4);
                    }
                    else if (kind == 3)
                    {
                        p.delay = R() * .15f; p.lifetime = 1.2f + R() * 2.8f;
                        p.size = .3f + R() * .65f; p.growth = 0;
                        p.velocity = radial * (15 + R() * 48) * Mathf.Sqrt(impulse) + Vector3.up * (16 + R() * 48);
                    }
                    else
                    {
                        p.delay = 0; p.lifetime = .18f; p.size = size * 1.5f;
                        p.growth = size; p.velocity = Vector3.zero;
                    }
                    puffs[n++] = p;
                }
            }
        }

        private Vector3 Position(Puff p, float age)
        {
            float dragTime = p.kind == 3 ? age : (1 - Mathf.Exp(-age * .65f)) / .65f;
            Vector3 point = p.position + p.velocity * dragTime;
            if (p.kind == 1) point += new Vector3(age * 1.8f, age * 2.4f, age * .4f);
            if (p.kind == 3) point.y -= 4.9f * age * age;
            point.y = Mathf.Max(.3f, point.y);
            return surfaceRotation * point;
        }

        private void Draw(Camera camera)
        {
            Vector3 right = camera.transform.right, up = camera.transform.up, forward = camera.transform.forward;
            for (int i = 0; i < Count; ++i)
                depths[i] = Vector3.Dot(Position(puffs[i], Mathf.Max(0, EffectAgeSeconds - puffs[i].delay)), forward);
            Array.Sort(order, depthComparison);
            for (int slot = 0; slot < Count; ++slot)
            {
                Puff p = puffs[order[slot]];
                float age = EffectAgeSeconds - p.delay;
                float life = Mathf.Clamp01(age / p.lifetime);
                float visible = age >= 0 && age < p.lifetime ? 1 : 0;
                float fade = Mathf.Min(1, Mathf.Max(0, age) * (p.kind == 1 ? 3 : 20)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1f, life)));
                // Flash must be visible on the very first rendered destruction frame.
                if (p.kind == 4) fade = 1 - life;
                float size = p.size + p.growth * Mathf.Sqrt(life);
                Vector3 center = Position(p, Mathf.Max(0, age));
                Vector3 x = right * size, y = up * size;
                Color color = Color.white;
                if (p.kind == 0) color = new Color(1, 1, 1, .85f * fuelGlow);
                if (p.kind == 1) color = Color.Lerp(new Color(.075f, .065f, .058f, .8f), new Color(.31f, .32f, .33f, .42f), life);
                if (p.kind == 2) { color = new Color(.46f, .43f, .37f, .32f); y *= .55f; }
                if (p.kind == 3)
                {
                    Vector3 velocity = surfaceRotation * (p.velocity - Vector3.up * 9.8f * age);
                    Vector3 streak = velocity - forward * Vector3.Dot(velocity, forward);
                    y = streak.sqrMagnitude > .001f ? streak.normalized * Mathf.Max(size * 2, streak.magnitude * .035f) : up * size * 2;
                    x = Vector3.Cross(forward, y).normalized * size * .35f;
                    color = new Color(1, .48f, .12f, .9f);
                }
                if (p.kind == 4) color = new Color(1, .88f, .57f, .5f * Mathf.Max(.1f, fuelGlow));
                color.a *= visible * fade;
                int v = slot * 4;
                vertices[v] = center - x - y; vertices[v + 1] = center + x - y;
                vertices[v + 2] = center + x + y; vertices[v + 3] = center - x + y;
                for (int corner = 0; corner < 4; ++corner)
                {
                    colors[v + corner] = color;
                    lifeSeed[v + corner] = new Vector2(life, p.seed + p.kind * 100);
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv2 = lifeSeed;
            mesh.RecalculateBounds();
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
    }
}
