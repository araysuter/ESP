using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Enlarged, display-only copy of the selected vehicle, positioned from ECEF state.</summary>
    public sealed class MapVehicleVisual : MonoBehaviour
    {
        private MissionDirector director;
        private Transform icon, model;
        private float modelHeight;

        public void Initialize(MissionDirector owner) { director = owner; }

        public void SetVehicle(GameObject prefab)
        {
            if (icon != null) Destroy(icon.gameObject);
            icon = null; model = null;
            if (prefab == null) return;
            icon = new GameObject("Vehicle map icon (enlarged)").transform;
            icon.SetParent(transform, false);
            model = CopyMeshes(prefab.transform, icon);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            bool found = false; Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            modelHeight = found ? Mathf.Max(.01f, bounds.size.y) : 1f;
        }

        // Copy only mesh references and transforms, not simulation, colliders, scripts or exhaust.
        private static Transform CopyMeshes(Transform source, Transform parent)
        {
            var target = new GameObject(source.name).transform;
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && !RocketPlume.IsExhaustRenderer(renderer))
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = target.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
            }
            // A plume component may live on an entire stage, so traverse every child.
            // Exhaust renderers are filtered above; no plume component is copied.
            foreach (Transform child in source) CopyMeshes(child, target);
            return target;
        }

        private void LateUpdate()
        {
            if (director == null || director.mapCamera == null || icon == null) return;
            var driver = director.FlightDriver as RocketFlightDriver;
            var simulation = driver != null ? driver.Simulation : null;
            Vector3 point, up;
            if (simulation != null && simulation.State.IsLaunched)
            {
                var state = simulation.State;
                var ecef = EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
                point = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
                var geo = EarthFrames.EcefToGeodetic(ecef);
                double lat = geo.LatitudeRadians, lon = geo.LongitudeRadians;
                double pitch = simulation.ActualPitchDegrees * Math.PI / 180, heading = simulation.ActualHeadingDegrees * Math.PI / 180;
                double east = Math.Cos(pitch) * Math.Sin(heading), north = Math.Cos(pitch) * Math.Cos(heading), vertical = Math.Sin(pitch);
                up = new Vector3((float)(-Math.Sin(lon) * east - Math.Sin(lat) * Math.Cos(lon) * north + Math.Cos(lat) * Math.Cos(lon) * vertical),
                    (float)(Math.Cos(lat) * north + Math.Sin(lat) * vertical),
                    (float)(Math.Cos(lon) * east - Math.Sin(lat) * Math.Sin(lon) * north + Math.Cos(lat) * Math.Sin(lon) * vertical)).normalized;
            }
            else
            {
                var site = LaunchSites.All[director.SelectedSiteIndex];
                double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
                var ecef = EarthFrames.GeodeticToEcef(lat, lon, site.ElevationMeters);
                point = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
                up = new Vector3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
            }
            var camera = director.mapCamera;
            // Test the true anchor against the ellipsoid, so enlargement never reveals a rear-side vehicle.
            bool visible = IsVisible(point, camera.transform.position);
            icon.gameObject.SetActive(visible);
            if (!visible) return;
            var booster = model.Find("BoosterAssembly");
            if (booster != null) booster.gameObject.SetActive(simulation == null || simulation.State.StageIndex == 0);
            foreach (string name in new[] { "LeftSRB", "RightSRB" })
            {
                var solid = model.Find(name);
                if (solid != null) solid.gameObject.SetActive(simulation == null || simulation.BoosterAttached);
            }
            float distance = Vector3.Distance(camera.transform.position, point);
            float height = camera.orthographic ? 2 * camera.orthographicSize : 2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            // Fraction of viewport height keeps the icon legible on Retina displays;
            // physical pixel sizing otherwise disappears under a launch-site label.
            float iconHeight = height * .12f;
            icon.position = point + point.normalized * .01f;
            icon.rotation = Quaternion.FromToRotation(Vector3.up, up);
            icon.localScale = Vector3.one * (iconHeight / modelHeight);
        }

        private static bool IsVisible(Vector3 point, Vector3 camera)
        {
            Vector3 delta = point - camera;
            const double a = 6.378137, b = 6.356752314245;
            double qa = (delta.x * delta.x + delta.z * delta.z) / (a * a) + delta.y * delta.y / (b * b);
            double qb = 2 * ((camera.x * delta.x + camera.z * delta.z) / (a * a) + camera.y * delta.y / (b * b));
            double qc = (camera.x * camera.x + camera.z * camera.z) / (a * a) + camera.y * camera.y / (b * b) - 1;
            double discriminant = qb * qb - 4 * qa * qc;
            if (qa <= 0 || discriminant < 0) return true;
            double t = (-qb - Math.Sqrt(discriminant)) / (2 * qa);
            return t < 0 || t >= .99999;
        }
    }
}
