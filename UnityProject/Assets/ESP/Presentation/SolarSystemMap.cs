using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Visual-only Sun/Earth context in the map's million-meter ECEF units.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class SolarSystemMap : MonoBehaviour
    {
        public const float AstronomicalUnit = 149597.8707f;
        public const float SunRadius = 695.7f;
        private const int Segments = 256;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private MissionDirector director;
        private MissionSunlight sunlight;
        private LineRenderer orbit;
        private Transform sunObject;
        private Material material, sunMaterial;

        public void Initialize(MissionDirector owner)
        {
            director = owner;
            sunlight = owner.GetComponent<MissionSunlight>();
            if (owner.mapRoot == null || owner.orbitMaterial == null) return;
            var lineObject = new GameObject("Approximate Earth orbit around Sun");
            lineObject.layer = MissionInsetView.MapLayer;
            lineObject.transform.SetParent(owner.mapRoot.transform, false);
            orbit = lineObject.AddComponent<LineRenderer>();
            orbit.useWorldSpace = true;
            orbit.positionCount = points.Length;
            orbit.alignment = LineAlignment.View;
            orbit.shadowCastingMode = ShadowCastingMode.Off;
            orbit.receiveShadows = false;
            material = new Material(owner.orbitMaterial);
            material.mainTexture = null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.32f, .55f, .66f, 1));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.32f, .55f, .66f, 1));
            orbit.sharedMaterial = material;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sun (visual only)";
            sphere.layer = MissionInsetView.MapLayer;
            sphere.transform.SetParent(owner.mapRoot.transform, false);
            sphere.transform.localScale = Vector3.one * (2 * SunRadius);
            var collider = sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sunMaterial = new Material(owner.orbitMaterial);
            sunMaterial.mainTexture = null;
            if (sunMaterial.HasProperty("_BaseColor")) sunMaterial.SetColor("_BaseColor", new Color(1, .93f, .76f));
            if (sunMaterial.HasProperty("_Color")) sunMaterial.SetColor("_Color", new Color(1, .93f, .76f));
            renderer.sharedMaterial = sunMaterial;
            sunObject = sphere.transform;
        }

        public static Vector3 EclipticPole(DateTime utc)
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double obliquity = (23.439 - .00000036 * (utc - epoch).TotalDays) * Math.PI / 180;
            var pole = EarthFrames.InertialToEcef(new Vector3d(0, -Math.Sin(obliquity), Math.Cos(obliquity)),
                CelestialFrames.GreenwichSiderealAngle(utc));
            return new Vector3((float)pole.X, (float)pole.Z, (float)pole.Y).normalized;
        }

        public static bool EarthOccludes(Vector3 point, Vector3 camera)
        {
            // Promote before subtracting/multiplying: float quadratic terms lose
            // enough precision to report false occlusion near a surface target.
            double x = camera.x, y = camera.y, z = camera.z;
            double dx = (double)point.x - x, dy = (double)point.y - y, dz = (double)point.z - z;
            const double a = 6.378137, b = 6.356752314245;
            double aa = (dx * dx + dz * dz) / (a * a) + dy * dy / (b * b);
            double bb = 2 * ((x * dx + z * dz) / (a * a) + y * dy / (b * b));
            double cc = (x * x + z * z) / (a * a) + y * y / (b * b) - 1;
            if (cc < 0) return true;
            double discriminant = bb * bb - 4 * aa * cc;
            if (aa <= 0 || discriminant < 0) return false;
            double t = (-bb - Math.Sqrt(discriminant)) / (2 * aa);
            return t >= 0 && t < .99999;
        }

        private void LateUpdate()
        {
            if (orbit == null || director == null || director.mapCamera == null || sunlight == null) return;
            var controls = director.mapCamera.GetComponent<GlobeCameraControls>();
            Vector3 towardSun = new Vector3((float)sunlight.SunEcef.X, (float)sunlight.SunEcef.Z, (float)sunlight.SunEcef.Y).normalized;
            Vector3 sunPosition = towardSun * AstronomicalUnit;
            if (sunObject != null) sunObject.position = sunPosition;
            orbit.enabled = director.IsMapView && controls != null && controls.Distance >= 1000 && sunlight.CurrentUtc != default;
            if (!orbit.enabled) return;
            Vector3 radial = -towardSun;
            Vector3 tangent = Vector3.Cross(EclipticPole(sunlight.CurrentUtc), radial).normalized;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = 2 * Mathf.PI * i / Segments;
                points[i] = sunPosition + AstronomicalUnit * (radial * Mathf.Cos(angle) + tangent * Mathf.Sin(angle));
            }
            orbit.SetPositions(points);
            orbit.widthMultiplier = Mathf.Max(.1f, controls.Distance * .0014f);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sunMaterial != null) Destroy(sunMaterial);
        }
    }
}
