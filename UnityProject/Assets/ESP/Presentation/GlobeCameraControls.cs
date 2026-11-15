using System;
using FlightCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>
    /// Orbits the camera around the vehicle while leaving Earth in its ECEF frame.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class GlobeCameraControls : MonoBehaviour
    {
        public Transform target;
        public bool useMissionUI;
        [SerializeField] private float degreesPerPixel = 0.2f;
        [SerializeField] private float zoomSensitivity = 0.001f;
        private float yaw;
        private float pitch;
        private float distance;
        private bool dragging;
        private bool alignSystemUp;
        private MissionDirector director;
        private Camera mapCamera;
        public float Distance => distance;
        public Vector3 FocusPosition => Center;
        public const float SystemDistance = 30000000f;

        private Vector3 Center
        {
            get
            {
                if (target != null) return target.position;
                if (!useMissionUI || director == null) return Vector3.zero;
                var driver = director.FlightDriver as RocketFlightDriver;
                if (driver != null && driver.Simulation != null && driver.Simulation.State.IsLaunched)
                {
                    var state = driver.Simulation.State;
                    return ToMap(EarthFrames.InertialToEcef(state.Position,
                        Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime));
                }
                var site = LaunchSites.All[director.SelectedSiteIndex];
                return ToMap(EarthFrames.GeodeticToEcef(site.Latitude * Math.PI / 180,
                    site.Longitude * Math.PI / 180, site.ElevationMeters));
            }
        }

        private static Vector3 ToMap(Vector3d value) => new Vector3(
            (float)(value.X / 1000000), (float)(value.Z / 1000000), (float)(value.Y / 1000000));

        private void Start()
        {
            director = FindFirstObjectByType<MissionDirector>();
            mapCamera = GetComponent<Camera>();
            Vector3 offset = transform.position - (useMissionUI ? Vector3.zero : Center);
            distance = Mathf.Clamp(offset.magnitude, 7.2f, SystemDistance);
            if (offset.sqrMagnitude < 1e-8f) offset = Vector3.back;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -85f, 85f);
            if (useMissionUI && director != null) SetDirection(Center.normalized);
            ApplyPose();
        }

        private void Update()
        {
            if (MissionSession.BlocksInput) { dragging = false; return; }
            Mouse mouse = Mouse.current;
            if (mouse == null) { dragging = false; return; }
            Vector2 pointer = mouse.position.ReadValue();
            bool insideWindow = pointer.x >= 0 && pointer.x < Screen.width && pointer.y >= 0 && pointer.y < Screen.height;
            bool overPanel = useMissionUI ? MissionUI.IsPointerOverUI(pointer) : (pointer.x>Screen.width-355 && pointer.y>Screen.height-350) || pointer.y < 180f || (pointer.x < 365f && pointer.y > Screen.height - 535f);
            if (mouse.leftButton.wasPressedThisFrame) dragging = insideWindow && !overPanel;
            if (!insideWindow || mouse.leftButton.wasReleasedThisFrame) dragging = false;
            if (dragging && mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0) alignSystemUp = false;
                yaw += delta.x * degreesPerPixel;
                pitch = Mathf.Clamp(pitch - delta.y * degreesPerPixel, -85f, 85f);
            }
            if (insideWindow && !overPanel)
            {
                float scroll = mouse.scroll.ReadValue().y;
                float wideZoom = Mathf.InverseLerp(1.5f, 4f, Mathf.Log10(Mathf.Max(1, distance)));
                distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * zoomSensitivity * Mathf.Lerp(1, 4, wideZoom)), 7.2f, SystemDistance);
            }
        }

        private void LateUpdate() { ApplyPose(); }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) dragging = false;
        }

        private void OnDisable()
        {
            dragging = false;
        }

        public void Focus(double latitude,double longitude){alignSystemUp=false;yaw=90-(float)longitude;pitch=Mathf.Clamp((float)latitude,-85,85);ApplyPose();}

        public void ShowShip()
        {
            distance = 20;
            alignSystemUp = false;
            if (Center.sqrMagnitude > 1e-8f) SetDirection(Center.normalized);
            ApplyPose();
        }

        public void ShowSystem()
        {
            distance = 500000;
            alignSystemUp = true;
            var sunlight = director != null ? director.GetComponent<MissionSunlight>() : null;
            DateTime utc = sunlight != null && sunlight.CurrentUtc != default ? sunlight.CurrentUtc : DateTime.UtcNow;
            SetDirection(SolarSystemMap.EclipticPole(utc));
            ApplyPose();
        }

        private void SetDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-8f) return;
            direction.Normalize();
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg, -85, 85);
        }

        // The WGS84 surface normal differs from the radial direction. Correct
        // against its tangent plane so oblate-Earth views remain unobstructed.
        public static Vector3 KeepEarthVisible(Vector3 center, Vector3 offset)
        {
            float length = offset.magnitude;
            if (length < 1e-8f || center.sqrMagnitude < 1e-8f ||
                !SolarSystemMap.EarthOccludes(center, center + offset)) return offset;
            const float a = 6.378137f, b = 6.3567523f;
            Vector3 normal = new Vector3(center.x / (a * a), center.y / (b * b), center.z / (a * a)).normalized;
            float inward = Vector3.Dot(offset, normal);
            Vector3 tangent = offset - Mathf.Min(0, inward) * normal;
            return (tangent + normal * .1f).normalized * length;
        }

        private void ApplyPose()
        {
            float yawRadians = yaw * Mathf.Deg2Rad;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitchRadians);
            Vector3 offset = new Vector3(horizontal * Mathf.Sin(yawRadians), Mathf.Sin(pitchRadians), horizontal * Mathf.Cos(yawRadians)) * distance;
            Vector3 center = Center;
            // Prevent a near-Earth camera from passing through the globe and
            // obscuring the vehicle when the user rotates around it.
            if (useMissionUI && distance < 100 && center.sqrMagnitude > 1e-8f)
            {
                offset = KeepEarthVisible(center, offset);
            }
            transform.position = center + offset;
            Vector3 up = Vector3.up;
            var sunlight = alignSystemUp && director != null ? director.GetComponent<MissionSunlight>() : null;
            if (sunlight != null && sunlight.CurrentUtc != default)
            {
                Vector3 forward = -offset.normalized;
                Vector3 sun = new Vector3((float)sunlight.SunEcef.X, (float)sunlight.SunEcef.Z, (float)sunlight.SunEcef.Y);
                Vector3 right = -Vector3.ProjectOnPlane(sun, forward).normalized;
                if (right.sqrMagnitude > .001f) up = Vector3.Cross(forward, right).normalized;
            }
            transform.LookAt(center, up);
            if (mapCamera != null)
            {
                mapCamera.nearClipPlane = Mathf.Clamp(distance * .00001f, .01f, 1f);
                mapCamera.farClipPlane = Mathf.Max(100, distance + 120 * SolarSystemMap.AstronomicalUnit + 1000);
            }
        }
    }
}
