using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>One solar clock and direction, transformed into each presentation frame.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class MissionSunlight : MonoBehaviour
    {
        public MissionDirector director;
        public RocketFlightDriver driver;
        public Light sun;
        public float daylightIntensity = 1.5f;
        public DateTime CurrentUtc { get; private set; }
        public Vector3d SunEcef { get; private set; }
        public Vector3 LocalSunDirection { get; private set; }
        public bool InEarthShadow { get; private set; }
        public double SolarElevationDegrees { get; private set; }
        internal DateTime? ReviewUtc;
        public string Status => CurrentUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") +
            (ReviewUtc.HasValue ? " · LIGHTING REVIEW OVERRIDE" : driver != null && driver.HasPosition ? " · SIMULATED" : " · LIVE") +
            (InEarthShadow ? " · NIGHT / EARTH SHADOW" : " · SUNLIT");

        private void LateUpdate() { Apply(); }

        public void Apply()
        {
            if (director == null || sun == null) return;
            CurrentUtc = ReviewUtc ?? (driver != null && driver.HasPosition
                ? driver.LaunchUtc.AddSeconds(driver.MissionTimeSeconds) : DateTime.UtcNow);
            SunEcef = SolarLighting.DirectionEcef(CurrentUtc);
            var site = LaunchSites.All[director.SelectedSiteIndex];
            double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
            LocalSunDirection = CameraRelativeCoordinates.FromEcef(SunEcef.X, SunEcef.Y, SunEcef.Z, 0, 0, 0, lat, lon).normalized;
            var position = EarthFrames.GeodeticToEcef(lat, lon, Math.Max(0, site.ElevationMeters));
            if (driver != null && driver.HasPosition)
            {
                var state = driver.Simulation.State;
                position = EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            }
            var location = EarthFrames.EcefToGeodetic(position);
            SolarElevationDegrees = SolarLighting.ElevationDegrees(SunEcef, location.LatitudeRadians, location.LongitudeRadians);
            InEarthShadow = SolarLighting.IsOccluded(position, SunEcef);
            Vector3 direction = director.IsMapView ? new Vector3((float)SunEcef.X, (float)SunEcef.Z, (float)SunEcef.Y) : LocalSunDirection;
            Aim(sun, direction);
            sun.intensity = director.IsMapView || !InEarthShadow ? daylightIntensity : 0;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 10, (float)SolarElevationDegrees));
            RenderSettings.ambientLight = director.IsMapView ? new Color(.025f, .03f, .045f) :
                Color.Lerp(new Color(.025f, .03f, .045f), new Color(.25f, .29f, .32f), daylight);
            if (director.launchCamera != null)
            {
                float air = 1 - Mathf.Clamp01((float)(director.AltitudeMeters / 120000));
                director.launchCamera.backgroundColor = Color.Lerp(new Color(.002f, .005f, .014f), new Color(.11f, .20f, .29f), daylight * air);
            }
        }

        public static void Aim(Light light, Vector3 towardSun)
        {
            // Unity light forward is the direction rays travel, opposite the Sun vector.
            Vector3 forward = -towardSun.normalized;
            light.transform.rotation = Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .99f ? Vector3.right : Vector3.up);
        }
    }
}
