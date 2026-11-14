using System;
using System.Collections.Generic;
using FlightCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Connects the fixed-step flight core to the presentation; simulation state stays in doubles.</summary>
    public sealed class RocketFlightDriver : MonoBehaviour, IMissionFlightDriver
    {
        public MissionDirector director;
        public NasaWeatherProfile nasaWeather;
        private RocketSimulation simulation;
        private WeatherAtmosphere launchAtmosphere;
        private LaunchSite launchSite;
        private double accumulator, orbitSeconds;
        public bool OrbitVerified { get; private set; }
        private bool started;
        private AssistedAscent guidance;
        private float pendingThrottle = 1f;
        public bool AssistanceEnabled { get; private set; } = true;
        public int TimeRate { get; private set; } = 1;
        public void ToggleAssistance() { AssistanceEnabled = !AssistanceEnabled; }
        public void SetTimeRate(int rate)
        {
            if (rate != 1 && rate != 5 && rate != 20) throw new ArgumentOutOfRangeException(nameof(rate));
            TimeRate = rate;
        }
        private string weatherSource = "Standard atmosphere (no launch yet)";
        private const double StepSeconds = .02;
        public bool CanLaunch => !started;
        public string Status => simulation != null ? (OrbitVerified ? "Orbit verified (60 s unpowered)" : simulation.State.Status) : "Ready — " + (AssistanceEnabled ? "assisted" : "manual") + " • " + TimeRate + "× selected";
        public double AltitudeMeters => simulation != null ? simulation.State.AltitudeMeters : 0;
        public double SpeedMetersPerSecond => simulation != null ? simulation.State.SpeedRelativeGround : 0;
        public double MissionTimeSeconds => simulation != null ? simulation.State.MissionTime : 0;
        public float Throttle => simulation != null ? (float)simulation.Throttle : pendingThrottle;
        public string WeatherSourceLabel => weatherSource;
        public string FlightTelemetry => simulation == null ? (AssistanceEnabled ? "Assisted" : "Manual") + " • W/S or ↑/↓ for manual pitch • " + TimeRate + "× selected" :
            string.Format("{0} • pitch {1:F0}° • Stage {2} • Fuel {3:N0} kg • {4}× • {5}", AssistanceEnabled ? "Assisted" : "Manual", simulation.PitchDegrees, simulation.State.StageIndex + 1, simulation.State.PropellantKg, TimeRate, weatherSource) + (simulation.BoosterBurning ? " • SRBs burning (fixed thrust)" : "") + (accumulator >= StepSeconds ? " • simulation catching up" : "");
        public RocketSimulation Simulation => simulation;
        public bool HasPosition => simulation != null;
        public void SetPitchTarget(double pitchDegrees)
        {
            if (double.IsNaN(pitchDegrees) || double.IsInfinity(pitchDegrees)) throw new ArgumentOutOfRangeException(nameof(pitchDegrees));
            AssistanceEnabled = false;
            if (simulation != null) simulation.PitchDegrees = Math.Max(0, Math.Min(90, pitchDegrees));
        }
        public double LatitudeDegrees { get; private set; }
        public double LongitudeDegrees { get; private set; }

        private void Awake() { if (director != null) director.FlightDriver = this; }
        public void Launch(LaunchSite site, WeatherSnapshot weather)
        {
            if (started || site == null) return;
            DateTime now = DateTime.UtcNow;
            WeatherProfile profile = null;
            TimeSpan maxAge = TimeSpan.FromHours(12);
            if (nasaWeather != null && nasaWeather.Snapshot != null &&
                Math.Abs(nasaWeather.RequestedLatitudeDegrees - site.Latitude) < 1e-6 &&
                Math.Abs(nasaWeather.RequestedLongitudeDegrees - site.Longitude) < 1e-6 &&
                Fresh(nasaWeather.Snapshot.ValidTimeUtc, now, maxAge))
                profile = nasaWeather.Snapshot;
            else if (weather != null && Fresh(weather.ValidTimeUtc, now, TimeSpan.FromHours(2)))
            {
                try
                {
                    var levels = new List<WeatherLevel>();
                    foreach (var sample in weather.Samples)
                        levels.Add(new WeatherLevel(sample.AltitudeMSL, new AtmosphereSample(sample.TemperatureK, sample.PressurePa,
                            new Vector3d(sample.WindEastMps, sample.WindNorthMps, 0))));
                    profile = new WeatherProfile(levels, "Open-Meteo", weather.ValidTimeUtc, site.Latitude, site.Longitude);
                    maxAge = TimeSpan.FromHours(2);
                }
                catch (ArgumentException e) { Debug.LogWarning("Weather profile rejected: " + e.Message); }
            }
            var atmosphere = new WeatherAtmosphere(profile, now, maxAge);
            launchAtmosphere = atmosphere;
            weatherSource = atmosphere.SourceDescription;
            launchSite = site;
            simulation = new RocketSimulation(StepSeconds, (director.VehicleIndex == 1 ? RealVehiclePresets.BuildStarshipStages() : director.VehicleIndex == 2 ? RealVehiclePresets.BuildSlsStages() : StageDefinition.DemoStages()), atmosphere,
                site.Latitude * Math.PI / 180, site.Longitude * Math.PI / 180, site.ElevationMeters, director.VehicleIndex == 2 ? RealVehiclePresets.BuildSlsBoosterParameters() : null);
            guidance = new AssistedAscent(director.VehicleIndex == 1 ? 220000 : director.VehicleIndex == 2 ? 120000 : 160000, director.VehicleIndex == 2 ? 3 : 2);
            simulation.Throttle = pendingThrottle;
            simulation.Launch();
            accumulator = 0;
            started = true;
        }
        private static bool Fresh(DateTime time, DateTime now, TimeSpan age) => time.Kind == DateTimeKind.Utc && time <= now && now - time <= age;
        public void ResetMission()
        {
            simulation = null; launchAtmosphere = null; guidance = null; launchSite = null; started = false; accumulator = 0; orbitSeconds = 0; OrbitVerified = false;
            pendingThrottle = 1; AssistanceEnabled = true; TimeRate = 1;
            weatherSource = "Standard atmosphere (no launch yet)";
            if (director != null && director.EnvironmentRoot != null) director.EnvironmentRoot.position = Vector3.zero;
            if (director != null && director.RocketVisual != null)
            {
                director.RocketVisual.localPosition = Vector3.up * LaunchEnvironment.RocketBaseHeightMeters;
                director.RocketVisual.localRotation = Quaternion.identity;
                var view = director.RocketVisual.GetComponent<RocketPresentation>();
                foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true)) plume.SetFlow(0, 101325);
                foreach (string name in new[] { "LeftSRB", "RightSRB" }) { var solid = director.RocketVisual.Find(name); if (solid != null) solid.gameObject.SetActive(true); }
                if (view != null) { view.SetThrottle(0); if (view.booster != null) view.booster.gameObject.SetActive(true); }
            }
        }
        public void Stage() { if (simulation == null) return; if (simulation.BoosterAttached && simulation.BoosterPropellantKg <= 0) simulation.JettisonBoosters(); else simulation.Stage(); }
        public void SetThrottle(float value) { AssistanceEnabled = false; pendingThrottle = Mathf.Clamp01(value); if (simulation != null) simulation.Throttle = pendingThrottle; }

        private void Update()
        {
            if (simulation == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && director != null && !director.IsMapView)
            {
                float pitchInput = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                if (pitchInput != 0) { AssistanceEnabled = false; simulation.PitchDegrees = Math.Max(0, Math.Min(90, simulation.PitchDegrees + pitchInput * 20 * Time.unscaledDeltaTime)); }
                if (keyboard.spaceKey.wasPressedThisFrame) Stage();
            }
            // Keep all elapsed simulation time; limit work per frame and retain any backlog.
            accumulator += Time.unscaledDeltaTime * TimeRate;
            int steps = 0;
            while (accumulator >= StepSeconds && steps < 500)
            {
                if (AssistanceEnabled && guidance != null) guidance.Update(simulation);
                simulation.Step(); UpdateOrbitVerification(); accumulator -= StepSeconds; steps++;
            }
            RenderState();
        }
        private void UpdateOrbitVerification()
        {
            var state = simulation.State;
            bool unpowered = !simulation.BoosterBurning && (simulation.Throttle <= 0 || state.PropellantKg <= 0);
            const double mu = 3.986004418e14;
            const double minimumPerigeeRadius = 6378137.0 + 180000.0;
            var p = state.Position; var v = state.Velocity;
            double radius = p.Magnitude();
            double v2 = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            double energy = .5 * v2 - mu / radius;
            double hx = p.Y * v.Z - p.Z * v.Y, hy = p.Z * v.X - p.X * v.Z, hz = p.X * v.Y - p.Y * v.X;
            double h2 = hx * hx + hy * hy + hz * hz;
            double e2 = 1 + 2 * energy * h2 / (mu * mu);
            double perigee = h2 / (mu * (1 + Math.Sqrt(Math.Max(0, e2))));
            bool valid = state.IsLaunched && !state.IsLanded && unpowered && energy < 0 && e2 >= -1e-10 && perigee >= minimumPerigeeRadius;
            orbitSeconds = valid ? orbitSeconds + StepSeconds : 0;
            OrbitVerified = orbitSeconds >= 60;
        }
        private void RenderState()
        {
            var state = simulation.State;
            var ecef = EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            var geodetic = EarthFrames.EcefToGeodetic(ecef);
            LatitudeDegrees = geodetic.LatitudeRadians * 180 / Math.PI;
            LongitudeDegrees = geodetic.LongitudeRadians * 180 / Math.PI;
            if (director == null || director.EnvironmentRoot == null) return;
            double lat = launchSite.Latitude * Math.PI / 180, lon = launchSite.Longitude * Math.PI / 180;
            var pad = EarthFrames.GeodeticToEcef(lat, lon, launchSite.ElevationMeters);
            director.EnvironmentRoot.position = CameraRelativeCoordinates.FromEcef(pad.X, pad.Y, pad.Z, ecef.X, ecef.Y, ecef.Z, lat, lon);
            if (director.RocketVisual != null)
            {
                director.RocketVisual.position = Vector3.up * LaunchEnvironment.RocketBaseHeightMeters;
                float pressure = launchAtmosphere == null ? 101325 : (float)launchAtmosphere.Sample(Math.Max(-500, state.AltitudeMeters)).PressurePascals;
                foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true))
                    plume.SetFlow(!state.IsLanded && (plume.solidBooster ? simulation.BoosterBurning : plume.stageIndex == state.StageIndex && state.IsLaunched && state.PropellantKg > 0) ? (plume.solidBooster ? 1 : (float)simulation.Throttle) : 0, pressure);
                foreach (string name in new[] { "LeftSRB", "RightSRB" }) { var solid = director.RocketVisual.Find(name); if (solid != null) solid.gameObject.SetActive(simulation.BoosterAttached); }
                var view = director.RocketVisual.GetComponent<RocketPresentation>();
                if (view != null)
                {
                    if (view.booster != null) view.booster.gameObject.SetActive(state.StageIndex == 0);
                    view.SetThrottle(state.IsLaunched && !state.IsLanded && state.PropellantKg > 0 ? (float)simulation.Throttle : 0);
                }
                // Actual attitude is defined in ENU at the vehicle, not at the pad.
                double pitch = simulation.ActualPitchDegrees * Math.PI / 180;
                double heading = simulation.ActualHeadingDegrees * Math.PI / 180;
                double vlat = geodetic.LatitudeRadians, vlon = geodetic.LongitudeRadians;
                double east = Math.Cos(pitch) * Math.Sin(heading);
                double north = Math.Cos(pitch) * Math.Cos(heading);
                double up = Math.Sin(pitch);
                double x = -Math.Sin(vlon) * east - Math.Sin(vlat) * Math.Cos(vlon) * north + Math.Cos(vlat) * Math.Cos(vlon) * up;
                double y = Math.Cos(vlon) * east - Math.Sin(vlat) * Math.Sin(vlon) * north + Math.Cos(vlat) * Math.Sin(vlon) * up;
                double z = Math.Cos(vlat) * north + Math.Sin(vlat) * up;
                double localEast = -Math.Sin(lon) * x + Math.Cos(lon) * y;
                double localNorth = -Math.Sin(lat) * Math.Cos(lon) * x - Math.Sin(lat) * Math.Sin(lon) * y + Math.Cos(lat) * z;
                double localUp = Math.Cos(lat) * Math.Cos(lon) * x + Math.Cos(lat) * Math.Sin(lon) * y + Math.Sin(lat) * z;
                Vector3 direction = new Vector3((float)localEast, (float)localUp, (float)localNorth).normalized;
                // Choose a deterministic perpendicular reference; the flight model supplies no roll.
                director.RocketVisual.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            }
            if (!director.IsMapView && director.launchCamera != null)
            {
                Vector3 target = director.RocketVisual != null ? director.RocketVisual.position : Vector3.zero;
                Bounds bounds = default; bool found = false;
                if (director.RocketVisual != null)
                {
                    var rocketView = director.RocketVisual.GetComponent<RocketPresentation>();
                    foreach (Renderer renderer in director.RocketVisual.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || RocketPlume.IsExhaustRenderer(renderer) ||
                            (rocketView != null && rocketView.exhaust != null && renderer.transform.IsChildOf(rocketView.exhaust))) continue;
                        if (!found) { bounds = renderer.bounds; found = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    if (found) target = bounds.center;
                }
                float frameScale = found ? Mathf.Max(1, bounds.size.magnitude / 60f) : 1;
                director.launchCamera.transform.position = target + new Vector3(75, 20, -90) * frameScale;
                director.launchCamera.transform.LookAt(target);
            }
        }
    }
}
