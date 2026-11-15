using System;
using FlightCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Camera-relative orbit around the visible flight vehicle; never changes flight state.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FlightCameraControls : MonoBehaviour
    {
        public MissionDirector director;
        // Preview framing follows the live vehicle but never consumes pointer input.
        public bool passiveView;
        private Transform trackedVehicle;
        private Vector3 center;
        private float yaw, pitch, distance;
        private bool dragging;
        private Transform onboardHull;
        private Vector3 mountPosition;
        private Vector3 chasePosition;
        private Quaternion chaseRotation;
        private float chaseFieldOfView;
        private float cinematicStart;
        private static readonly float[] CinematicAngles = { 35f, 135f, 225f, 315f };
        private static readonly float[] CinematicElevations = { 18f, 5f, 28f, -8f };
        private static readonly float[] CinematicRanges = { 2.8f, 4.2f, 2.2f, 3.3f };
        public bool IsCinematicView { get; private set; }

        public void ToggleCinematicView()
        {
            if (passiveView || director == null || director.IsMapView || director.RocketVisual == null) return;
            if (IsOnboardView) RestoreChaseView();
            IsCinematicView = !IsCinematicView;
            cinematicStart = Time.unscaledTime;
            dragging = false;
            if (!IsCinematicView) trackedVehicle = null;
        }

        public bool IsOnboardView => onboardHull != null;

        public void ToggleOnboardView()
        {
            if (IsCinematicView) { IsCinematicView = false; trackedVehicle = null; }
            if (IsOnboardView)
            {
                RestoreChaseView();
                return;
            }
            if (passiveView || director == null || director.IsMapView || director.RocketVisual == null) return;

            var vehicle = director.RocketVisual;
            var hull = vehicle.Find("UpperAssembly");
            if (hull == null) hull = vehicle;
            if (!TryHullBounds(hull, vehicle.GetComponent<RocketPresentation>(), out Bounds bounds)) return;

            var camera = GetComponent<Camera>();
            chasePosition = transform.position;
            chaseRotation = transform.rotation;
            chaseFieldOfView = camera.fieldOfView;
            camera.fieldOfView = 62f;
            onboardHull = hull;
            // Aim past the nose, with the mount just outside the aft hull.
            mountPosition = new Vector3(bounds.max.x + Mathf.Max(.5f, bounds.size.x * .09f),
                bounds.min.y + bounds.size.y * .12f, bounds.center.z);
            dragging = false;
            ApplyOnboardPose();
        }

        public void ResetFraming()
        {
            IsCinematicView = false;
            if (IsOnboardView) RestoreChaseView();
            trackedVehicle = null;
            center = Vector3.zero;
            yaw = pitch = distance = 0;
            dragging = false;
        }

        private void LateUpdate()
        {
            if (director == null || (director.IsMapView && !passiveView) || director.RocketVisual == null) { dragging = false; return; }
            if (IsOnboardView)
            {
                ApplyOnboardPose();
                return;
            }
            Transform vehicle = director.RocketVisual;
            if (!TryVisibleBounds(vehicle, out Bounds bounds)) { dragging = false; return; }
            float radius = Mathf.Max(1f, bounds.extents.magnitude);
            if (IsCinematicView)
            {
                float elapsed = Time.unscaledTime - cinematicStart;
                int shot = Mathf.FloorToInt(elapsed / 8f) % 4;
                float angle = (CinematicAngles[shot] + (elapsed % 8f) * 1.5f) * Mathf.Deg2Rad;
                float elevation = CinematicElevations[shot] * Mathf.Deg2Rad;
                Vector3 shotDirection = new Vector3(Mathf.Cos(elevation) * Mathf.Sin(angle), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Cos(angle));
                Vector3 targetPosition = KeepAboveSurface(bounds.center + shotDirection * radius * CinematicRanges[shot]);
                float blend = 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime);
                transform.position = KeepAboveSurface(Vector3.Lerp(transform.position, targetPosition, blend));
                Vector3 look = bounds.center - transform.position;
                if (look.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                return;
            }
            float minDistance = radius * 1.5f;
            float maxDistance = Mathf.Max(minDistance * 2f, radius * 15f);
            if (trackedVehicle != vehicle)
            {
                trackedVehicle = vehicle;
                center = bounds.center;
                Vector3 offset = transform.position - center;
                if (offset.sqrMagnitude < 1f) offset = new Vector3(75, 20, -90);
                yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f);
                distance = Mathf.Clamp(offset.magnitude * Mathf.Max(1f, radius * 2f / 60f), minDistance, maxDistance);
            }
            else
            {
                // Smooth changes in framing when a stage disappears, without changing the chosen zoom.
                center = Vector3.Lerp(center, bounds.center, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            }
            Mouse mouse = Mouse.current;
            if (passiveView || MissionSession.BlocksInput || mouse == null || !Application.isFocused) dragging = false;
            else
            {
                Vector2 pointer = mouse.position.ReadValue();
                bool inside = pointer.x >= 0 && pointer.x < Screen.width && pointer.y >= 0 && pointer.y < Screen.height;
                bool overUI = MissionUI.IsPointerOverUI(pointer);
                if (mouse.leftButton.wasPressedThisFrame) dragging = inside && !overUI;
                if (!inside || mouse.leftButton.wasReleasedThisFrame) dragging = false;
                if (dragging && mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * .2f;
                    pitch = Mathf.Clamp(pitch - delta.y * .2f, -80f, 80f);
                }
                if (inside && !overUI) distance *= Mathf.Exp(-mouse.scroll.ReadValue().y * .003f);
            }
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(p) * Mathf.Sin(y), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Cos(y));
            transform.position = KeepAboveSurface(center + direction * distance);
            transform.LookAt(center, Vector3.up);
        }

        private void ApplyOnboardPose()
        {
            Vector3 outward = onboardHull.right;
            Vector3 forward = (onboardHull.up + .28f * outward).normalized;
            // Put the hull to the right of the lens and the Earth-facing side to its left.
            Vector3 up = Vector3.Cross(outward, forward);
            up = Quaternion.AngleAxis(-9f, forward) * up;
            transform.SetPositionAndRotation(KeepAboveSurface(onboardHull.TransformPoint(mountPosition)),
                Quaternion.LookRotation(forward, up));
        }

        private void RestoreChaseView()
        {
            onboardHull = null;
            GetComponent<Camera>().fieldOfView = chaseFieldOfView;
            transform.SetPositionAndRotation(chasePosition, chaseRotation);
        }

        private static bool TryHullBounds(Transform hull, RocketPresentation view, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in hull.GetComponentsInChildren<Renderer>())
            {
                if (!IsHullRenderer(renderer, view)) continue;
                Bounds mesh = renderer.localBounds;
                Matrix4x4 toHull = hull.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 point = toHull.MultiplyPoint3x4(corner);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        private Vector3 KeepAboveSurface(Vector3 cameraPosition)
        {
            // Render coordinates are fixed-pad ENU (Unity east,up,north), based at
            // the current vehicle ECEF position in flight and at the pad otherwise.
            // Only move the camera; authoritative spacecraft state is untouched.
            var site = LaunchSites.All[director.SelectedSiteIndex];
            double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
            var origin = EarthFrames.GeodeticToEcef(lat, lon, site.ElevationMeters);
            if (director.FlightDriver is RocketFlightDriver flight && flight.HasPosition)
            {
                var state = flight.Simulation.State;
                origin = EarthFrames.InertialToEcef(state.Position,
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            }
            double east = cameraPosition.x, north = cameraPosition.z, up = cameraPosition.y;
            var point = origin + new Vector3d(
                -Math.Sin(lon) * east - Math.Sin(lat) * Math.Cos(lon) * north + Math.Cos(lat) * Math.Cos(lon) * up,
                 Math.Cos(lon) * east - Math.Sin(lat) * Math.Sin(lon) * north + Math.Cos(lat) * Math.Sin(lon) * up,
                 Math.Cos(lat) * north + Math.Sin(lat) * up);
            var geodetic = EarthFrames.EcefToGeodetic(point);
            // The current collision approximation uses the launch site's elevation.
            double minimumAltitude = site.ElevationMeters + 1;
            if (geodetic.AltitudeMeters >= minimumAltitude) return cameraPosition;
            var safe = EarthFrames.GeodeticToEcef(geodetic.LatitudeRadians, geodetic.LongitudeRadians, minimumAltitude);
            return CameraRelativeCoordinates.FromEcef(safe.X, safe.Y, safe.Z, origin.X, origin.Y, origin.Z, lat, lon);
        }

        private static bool TryVisibleBounds(Transform vehicle, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var view = vehicle.GetComponent<RocketPresentation>();
            foreach (Renderer renderer in vehicle.GetComponentsInChildren<Renderer>())
            {
                if (!IsHullRenderer(renderer, view)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static bool IsHullRenderer(Renderer renderer, RocketPresentation view) =>
            renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.GetComponentInParent<ReentryPlasma>() == null && !RocketPlume.IsExhaustRenderer(renderer) &&
            (view == null || view.exhaust == null || !renderer.transform.IsChildOf(view.exhaust));

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) dragging = false; }
        private void OnDisable() { dragging = false; }
    }
}
