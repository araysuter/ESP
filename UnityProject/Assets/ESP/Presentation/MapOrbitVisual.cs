using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Map-only coast conic. Owns no flight state; parent UI owns marker labels.</summary>
    public sealed class MapOrbitVisual : MonoBehaviour
    {
        [Tooltip("Serialized URP/Unlit material; keeps the depth-tested shader in packaged builds.")]
        public Material orbitMaterial;
        public OrbitProjectionResult Projection { get; private set; }
        private MissionDirector director;
        private LineRenderer line;
        private Material runtimeMaterial;
        private float nextRefresh;
        private UIDocument missionDocument;
        private readonly Vector3[] vertices = new Vector3[OrbitProjection.MaximumSamples];

        public void Initialize(MissionDirector owner)
        {
            director = owner;
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
            if (!HasActiveFlight()) { Clear(); return; }
            // No runtime Shader.Find: a material with its shader must be serialized by the scene builder.
            if (orbitMaterial == null) { Clear(); return; }
            if (line == null) CreateLine();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .25f;
                var state = ((RocketFlightDriver)director.FlightDriver).Simulation.State;
                Projection = OrbitProjection.Build(state.Position, state.Velocity,
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime, OrbitProjection.MaximumSamples,
                    Gravity.EarthGravitationalParameter * ((RocketFlightDriver)director.FlightDriver).Simulation.GravityMultiplier);
                line.enabled = Projection.IsValid && Projection.EcefPoints.Count >= 2;
                if (line.enabled)
                {
                    int count = Projection.EcefPoints.Count;
                    for (int i = 0; i < count; i++) vertices[i] = ToMap(Projection.EcefPoints[i]);
                    line.positionCount = count;
                    line.SetPositions(vertices);
                }
            }
            if (line.enabled)
            {
                var camera = director.mapCamera;
                float distance = Mathf.Max(.1f, Vector3.Distance(camera.transform.position, vertices[0]));
                float height = camera.orthographic ? 2 * camera.orthographicSize : 2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                if (missionDocument == null)
                {
                    var ui = FindObjectOfType<MissionUI>();
                    if (ui != null) missionDocument = ui.GetComponent<UIDocument>();
                }
                float logicalHeight = missionDocument != null ? missionDocument.rootVisualElement.resolvedStyle.height : 0;
                if (float.IsNaN(logicalHeight) || logicalHeight <= 0) logicalHeight = 800;
                line.widthMultiplier = height * 1.7f / logicalHeight;
            }
        }

        private void CreateLine()
        {
            var child = new GameObject("Instantaneous coast orbit");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false; // Closed conic includes an exact duplicate endpoint.
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 0;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            runtimeMaterial = new Material(orbitMaterial);
            runtimeMaterial.mainTexture = null;
            var cyan = new Color(.18f, .86f, .96f, 1);
            if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", cyan);
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", cyan);
            // URP/Unlit defaults to ZTest LEqual; never use an overlay/Always shader.
            if (runtimeMaterial.HasProperty("_ZTest")) runtimeMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            line.sharedMaterial = runtimeMaterial;
            line.startColor = line.endColor = Color.white;
            line.enabled = false;
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
            Projection = null;
            nextRefresh = 0;
        }
        private void OnDisable() { Clear(); }
        private void OnDestroy() { if (runtimeMaterial != null) Destroy(runtimeMaterial); }
    }
}
