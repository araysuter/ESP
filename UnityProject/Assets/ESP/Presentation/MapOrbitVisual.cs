using System;
using System.Collections.Generic;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Smooth coast orbit; parent UI owns marker labels. Flight history is recorded without a visible overlay.</summary>
    public sealed class MapOrbitVisual : MonoBehaviour
    {
        [Tooltip("Serialized URP/Unlit material; keeps the depth-tested shader in packaged builds.")]
        public Material orbitMaterial;
        public OrbitProjectionResult Projection { get; private set; }
        private MissionDirector director;
        private LineRenderer line;
        private readonly List<LineRenderer> patchLines = new List<LineRenderer>();
        private readonly List<Material> patchMaterials = new List<Material>();
        private readonly List<(Vector3 position, CelestialPrimary primary)> transitions = new List<(Vector3, CelestialPrimary)>();
        private FlightTrailRecorder recorder;
        public FlightTrail FlownTrail => recorder != null ? recorder.History : null;
        private Material runtimeMaterial;
        private float nextRefresh;
        private UIDocument missionDocument;
        private readonly Vector3[] vertices = new Vector3[OrbitProjection.MaximumSamples];

        public void Initialize(MissionDirector owner)
        {
            director = owner;
            if (owner != null)
            {
                recorder = owner.GetComponent<FlightTrailRecorder>();
                if (recorder == null) recorder = owner.gameObject.AddComponent<FlightTrailRecorder>();
                recorder.Initialize(owner);
            }
            nextRefresh = 0;
        }

        private bool HasActiveFlight()
        {
            if (director == null || !director.IsMapView || director.mapCamera == null || !director.mapCamera.isActiveAndEnabled) return false;
            var driver = director.FlightDriver as RocketFlightDriver;
            return driver != null && driver.Simulation != null && driver.Simulation.State.IsLaunched && !driver.Simulation.State.IsLanded;
        }

        private void LateUpdate()
        {
            var driver = director != null ? director.FlightDriver as RocketFlightDriver : null;
            var simulation = driver != null ? driver.Simulation : null;
            // Also sample immediately before drawing to keep the last endpoint authoritative.
            recorder?.RecordSimulation(simulation);
            var mapControls = director != null && director.mapCamera != null ? director.mapCamera.GetComponent<GlobeCameraControls>() : null;
            if (director == null || director.MapRenderCamera == null || simulation == null || orbitMaterial == null)
            { Clear(); return; }
            if (line == null)
                line = CreateLine("Instantaneous coast orbit", new Color(.18f, .86f, .96f, 1), out runtimeMaterial);
            var state = simulation.State;
            double earthAngle = Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime;
            if (!state.IsLaunched || state.IsLanded)
            {
                line.enabled = false;
                HidePatches();
                Projection = null;
                nextRefresh = 0;
            }
            else if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .25f;
                bool distant = simulation.Celestial != null && (simulation.Primary != CelestialPrimary.Earth ||
                    OrbitDiagnostics.Compute(state.Position, state.Velocity).ApogeeRadiusMeters > 250000000);
                if (distant)
                {
                    Projection = null; line.enabled = false;
                    DrawPatches(simulation, earthAngle);
                }
                else
                {
                HidePatches();
                Projection = OrbitProjection.Build(state.Position, state.Velocity, earthAngle,
                    OrbitProjection.MaximumSamples, Gravity.EarthGravitationalParameter * simulation.GravityMultiplier);
                line.enabled = Projection.IsValid && Projection.EcefPoints.Count >= 2;
                if (line.enabled)
                {
                    int count = Projection.EcefPoints.Count;
                    for (int i = 0; i < count; i++) vertices[i] = ToMap(Projection.EcefPoints[i]);
                    line.positionCount = count;
                    line.SetPositions(vertices);
                }
                }
            }
            float width = LogicalPixelWidth(ToMap(EarthFrames.InertialToEcef(state.Position, earthAngle)));
            line.widthMultiplier = width * 1.7f;
            foreach (var patch in patchLines) patch.widthMultiplier = width * 1.7f;
        }

        private void DrawPatches(RocketSimulation simulation, double earthAngle)
        {
            var state = simulation.State;
            var points = PatchedConicProjection.Build(simulation.Celestial, state.Position, state.Velocity,
                state.MissionTime, simulation.GravityMultiplier);
            transitions.Clear();
            int segment = -1; CelestialPrimary last = (CelestialPrimary)(-1);
            var verticesForSegment = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.Primary != last)
                {
                    if (segment >= 0)
                    {
                        var transitionPosition=ToPatchMap(point, last, simulation.Celestial, state.MissionTime, earthAngle);
                        transitions.Add((transitionPosition, point.Primary));
                        verticesForSegment.Add(transitionPosition);
                        patchLines[segment].positionCount = verticesForSegment.Count;
                        patchLines[segment].SetPositions(verticesForSegment.ToArray());
                        verticesForSegment.Clear();
                    }
                    segment++;
                    while (patchLines.Count <= segment)
                    {
                        var renderer = CreateLine("Patched trajectory", Color.white, out var mat);
                        patchLines.Add(renderer); patchMaterials.Add(mat);
                    }
                    var color = point.Primary == CelestialPrimary.Moon ? new Color(.95f,.69f,.25f) :
                        point.Primary == CelestialPrimary.Sun ? new Color(.72f,.48f,.98f) : new Color(.18f,.86f,.96f);
                    var material = patchMaterials[segment];
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
                    if (material.HasProperty("_Color")) material.SetColor("_Color",color);
                    patchLines[segment].enabled = true; last = point.Primary;
                }
                verticesForSegment.Add(ToPatchMap(point, point.Primary, simulation.Celestial, state.MissionTime, earthAngle));
            }
            if (segment >= 0)
            {
                patchLines[segment].positionCount = verticesForSegment.Count;
                patchLines[segment].SetPositions(verticesForSegment.ToArray());
            }
            for (int i=segment+1; i<patchLines.Count; i++) patchLines[i].enabled=false;
        }
        private static Vector3 ToPatchMap(PatchedPoint point, CelestialPrimary primary, LunarEphemeris ephemeris, double now, double earthAngle)
        {
            // Display each patch around its primary's current position, as in a
            // conventional patched-conic map. Do not mix future body positions
            // into the displayed osculating orbit of that body.
            var position=point.Position;
            if(primary==CelestialPrimary.Moon) position+=ephemeris.MoonPosition(now)-ephemeris.MoonPosition(point.Time);
            else if(primary==CelestialPrimary.Sun) position+=ephemeris.SunPosition(now)-ephemeris.SunPosition(point.Time);
            return ToMap(EarthFrames.InertialToEcef(position,earthAngle));
        }
        private void HidePatches() { foreach (var patch in patchLines) patch.enabled=false; transitions.Clear(); }
        public bool TryGetTransitionScreenPosition(int index, out Vector2 screenPosition, out string label)
        {
            screenPosition=default; label="";
            if (!HasActiveFlight() || index<0 || index>=transitions.Count) return false;
            var transition=transitions[index]; var camera=director.mapCamera;
            if(IsOccluded(transition.position,camera.transform.position)) return false;
            Vector3 screen=camera.WorldToScreenPoint(transition.position);
            if(screen.z<=0 || !camera.pixelRect.Contains(new Vector2(screen.x,screen.y))) return false;
            screenPosition=new Vector2(screen.x,screen.y); label="→ " + transition.primary + " SOI"; return true;
        }

        private float LogicalPixelWidth(Vector3 anchor)
        {
            var camera = director.MapRenderCamera;
            float distance = Mathf.Max(.1f, Vector3.Distance(camera.transform.position, anchor));
            float height = camera.orthographic ? 2 * camera.orthographicSize : 2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            if (missionDocument == null)
            {
                var ui = FindObjectOfType<MissionUI>();
                if (ui != null) missionDocument = ui.GetComponent<UIDocument>();
            }
            float logicalHeight = missionDocument != null ? missionDocument.rootVisualElement.resolvedStyle.height : 0;
            if (float.IsNaN(logicalHeight) || logicalHeight <= 0) logicalHeight = 800;
            if (!director.IsMapView) logicalHeight = 216;
            return height / logicalHeight;
        }

        private LineRenderer CreateLine(string label, Color color, out Material material)
        {
            var child = new GameObject(label);
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.loop = false;
            renderer.alignment = LineAlignment.View;
            renderer.numCornerVertices = 0;
            renderer.numCapVertices = 2;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            material = new Material(orbitMaterial);
            material.mainTexture = null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            // The serialized URP/Unlit material supplies normal depth-tested globe occlusion.
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            renderer.sharedMaterial = material;
            renderer.startColor = renderer.endColor = Color.white;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>Returns bottom-left-origin screen pixels. Rejects offscreen and globe-occluded markers.</summary>
        public bool TryGetMarkerScreenPosition(OrbitMarkerKind kind, out Vector2 screenPosition, out double wgs84AltitudeMeters)
        {
            screenPosition = default; wgs84AltitudeMeters = 0;
            if (!HasActiveFlight() || Projection == null || !Projection.IsValid) return false;
            var marker = Projection.Marker(kind);
            if (!marker.IsAvailable) return false;
            Vector3 point = ToMap(marker.EcefPosition);
            var camera = director.mapCamera;
            if (IsOccluded(point, camera.transform.position)) return false;
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0 || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) return false;
            screenPosition = new Vector2(screen.x, screen.y);
            wgs84AltitudeMeters = marker.Wgs84AltitudeMeters;
            return true;
        }

        private static Vector3 ToMap(Vector3d ecef) => new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
        private static bool IsOccluded(Vector3 point, Vector3 camera)
        {
            double dx = point.x - camera.x, dy = point.y - camera.y, dz = point.z - camera.z;
            const double a = 6.378137, b = a * (1 - EarthFrames.Flattening);
            double aa = (dx * dx + dz * dz) / (a * a) + dy * dy / (b * b);
            double bb = 2 * ((camera.x * dx + camera.z * dz) / (a * a) + camera.y * dy / (b * b));
            double cc = (camera.x * camera.x + camera.z * camera.z) / (a * a) + camera.y * camera.y / (b * b) - 1;
            double discriminant = bb * bb - 4 * aa * cc;
            if (aa <= 0 || discriminant < 0) return false;
            double hit = (-bb - Math.Sqrt(discriminant)) / (2 * aa);
            return hit >= 0 && hit < .999999;
        }
        private void Clear()
        {
            if (line != null) line.enabled = false;
            HidePatches();
            Projection = null;
            nextRefresh = 0;
        }
        private void OnDisable() { Clear(); }
        private void OnDestroy() { if (runtimeMaterial != null) Destroy(runtimeMaterial); foreach (var material in patchMaterials) if (material != null) Destroy(material); }
    }
}
