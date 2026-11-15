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
        public Texture2D nightLights;
        [Range(0, 2)] public float nightLightsStrength = .65f;
        private Light mapSun;
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

        private Color flightAmbient;
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (mapSun != null) mapSun.enabled = false;
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (director == null) return;
            var backdrop = director.GetComponent<FlightEarthBackdrop>();
            bool isMap = camera == director.mapCamera || (director.Inset != null && camera == director.Inset.MapPreviewCamera);
            // URP's main-light selection must use the coordinate frame of THIS
            // camera, including the Base Earth / Overlay vehicle stack.
            RenderSettings.sun = isMap ? mapSun : backdrop != null && camera == backdrop.BackgroundCamera ? backdrop.BackgroundSun : sun;
            RenderSettings.ambientLight = isMap ? new Color(.025f, .03f, .045f) : flightAmbient;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            RenderSettings.sun = sun;
            RenderSettings.ambientLight = flightAmbient;
        }
        private void LateUpdate() { Apply(); }

        public void Apply()
        {
            if (director == null || sun == null) return;
            CurrentUtc = ReviewUtc ?? (driver != null && driver.HasPosition
                ? driver.LaunchUtc.AddSeconds(driver.MissionTimeSeconds) : DateTime.UtcNow);
            SunEcef = SolarLighting.DirectionEcef(CurrentUtc);
            Shader.SetGlobalVector("_EspSunDirectionEcef", new Vector4((float)SunEcef.X, (float)SunEcef.Z, (float)SunEcef.Y, 0));
            Shader.SetGlobalTexture("_EspNightLights", nightLights != null ? nightLights : Texture2D.blackTexture);
            Shader.SetGlobalFloat("_EspNightLightsStrength", nightLights != null ? nightLightsStrength : 0);
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
            // Both coordinate spaces are rendered in the same frame. Keep separate,
            // layer-isolated lights rather than rotating a shared Sun for the main view.
            if (mapSun == null)
            {
                var lightObject = new GameObject("Map ECEF sunlight");
                lightObject.transform.SetParent(transform, false);
                mapSun = lightObject.AddComponent<Light>();
                mapSun.type = LightType.Directional;
                mapSun.shadows = LightShadows.None;
                mapSun.cullingMask = 1 << MissionInsetView.MapLayer;
            }
            Aim(mapSun, new Vector3((float)SunEcef.X, (float)SunEcef.Z, (float)SunEcef.Y));
            mapSun.enabled = true;
            mapSun.color = sun.color;
            mapSun.intensity = daylightIntensity;
            sun.cullingMask &= ~((1 << MissionInsetView.MapLayer) | (1 << 31));
            Aim(sun, LocalSunDirection);
            sun.intensity = !InEarthShadow ? daylightIntensity : 0;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 10, (float)SolarElevationDegrees));
            flightAmbient = Color.Lerp(new Color(.025f, .03f, .045f), new Color(.25f, .29f, .32f), daylight);
            RenderSettings.ambientLight = flightAmbient;
            if (director.launchCamera != null)
            {
                float air = 1 - Mathf.Clamp01((float)(director.AltitudeMeters / 120000));
                director.launchCamera.backgroundColor = Color.Lerp(new Color(.002f, .005f, .014f), new Color(.11f, .20f, .29f), daylight * air);
            }
        }

        private void OnDisable() { Shader.SetGlobalFloat("_EspNightLightsStrength", 0); }

        public static void Aim(Light light, Vector3 towardSun)
        {
            // Unity light forward is the direction rays travel, opposite the Sun vector.
            Vector3 forward = -towardSun.normalized;
            light.transform.rotation = Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .99f ? Vector3.right : Vector3.up);
        }
    }
}
