using System;
using System.Collections.Generic;
using FlightCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Connects the fixed-step flight core to the presentation; simulation state stays in doubles.</summary>
    public sealed partial class RocketFlightDriver : MonoBehaviour, IMissionFlightDriver
    {
        public MissionDirector director;
        public NasaWeatherProfile nasaWeather;
        private RocketSimulation simulation;
        private WeatherAtmosphere launchAtmosphere;
        private LaunchSite launchSite;
        private double accumulator, orbitSeconds;
        public bool OrbitVerified { get; private set; }
        public bool IsPaused { get; set; }
        public void DisableAssistance() { AssistanceEnabled = false; }
        public void InvalidateOrbitVerification() { orbitSeconds = 0; OrbitVerified = false; }
        private bool started;
        private AssistedAscent guidance;
        private UIDocument missionDocument;
        private float pendingThrottle = 1f;
        public bool AssistanceEnabled { get; private set; } = true;
        public int TimeRate { get; private set; } = 1;
        public bool SimulationCatchingUp { get; private set; }
        private double actualTimeRate;
        public double ActualTimeRate => IsPaused || MissionSession.BlocksInput || simulation == null || simulation.State.IsLanded ? 0 : actualTimeRate;
        // Higher rates are only offered after the normal mission has verified
        // a sustained, unpowered orbit. Integration remains at the fixed step.
        public bool CanHighWarp => simulation != null && !IsPlanningReturn && !IsPlanningLunar && simulation.State.AltitudeMeters > 120000 &&
            (LunarActive ? lunarGuidance.CanCoastWarp : ReentryActive ? (simulation.Primary == CelestialPrimary.Earth &&
                (returnController.Phase == StarshipReturnPhase.CoastToBurn ||
                (returnController.Phase == StarshipReturnPhase.EntryCoast && simulation.State.AltitudeMeters > 130000))) :
                (simulation.Primary != CelestialPrimary.Earth || OrbitVerified)) && !simulation.State.IsLanded &&
            simulation.State.IsLaunched && !simulation.BoosterBurning && simulation.Throttle <= 0 &&
            (LunarActive || !(MissionSession.Instance != null && MissionSession.Instance.ModifiedFlight));
        public void ToggleAssistance() { if (ReentryActive || LunarActive || IsPaused || (MissionSession.Instance != null && MissionSession.Instance.GravityMultiplier != 1)) return; AssistanceEnabled = !AssistanceEnabled; if (AssistanceEnabled) simulation?.DisableManualRotation(); }
        public void SetTimeRate(int rate)
        {
            if (Array.IndexOf(new[] { 1, 2, 3, 4, 5, 10, 20, 50, 100, 300, 1000 }, rate) < 0) throw new ArgumentOutOfRangeException(nameof(rate));
            if (rate > 1 && (IsPlanningReturn || IsPlanningLunar || ReturnNeedsRealTime || LunarNeedsRealTime)) return;
            if (rate > 20 && !CanHighWarp) return;
            if (TimeRate != rate) actualTimeRate = 0;
            TimeRate = rate;
        }
        private string weatherSource = "Standard atmosphere (no launch yet)";
        private const double StepSeconds = .02;
        private static readonly (StageDefinition[] Stages, SolidBoosterDefinition Boosters)[] VehiclePresets =
        {
            (StageDefinition.DemoStages(), null),
            (RealVehiclePresets.BuildStarshipStages(), null),
            (RealVehiclePresets.BuildSlsStages(), RealVehiclePresets.BuildSlsBoosterParameters()),
            (RealVehiclePresets.BuildSuperStarshipStages(), null)
        };
        private (StageDefinition[] Stages, SolidBoosterDefinition Boosters) SelectedPreset =>
            VehiclePresets[director != null ? director.VehicleIndex : 0];

        public int StageCount => SelectedPreset.Stages.Length;
        public double StagePropellantCapacityKg(int stageIndex) => SelectedPreset.Stages[stageIndex].PropellantKg;
        public double? BoosterPropellantCapacityKg => SelectedPreset.Boosters?.PropellantKg;

        public (int StageNumber, double StageKg, double? BoosterKg) FuelTelemetry
        {
            get
            {
                if (simulation == null)
                {
                    var preset = SelectedPreset;
                    return (1, preset.Stages[0].PropellantKg, preset.Boosters?.PropellantKg);
                }

                var state = simulation.State;
                return (state.StageIndex + 1, state.PropellantKg,
                    simulation.BoosterAttached ? simulation.BoosterPropellantKg : (double?)null);
            }
        }

        public bool CanLaunch => !started;
        public string Status => ReentryActive ? ReentryStatus : lunarGuidance != null || lunarNotice != null ? LunarStatus : simulation != null ? (OrbitVerified ? "Orbit verified (60 s unpowered)" : !simulation.State.IsLanded && AssistanceEnabled && guidance != null && guidance.IsCoasting ? "Coasting to ICPS circularization burn" : simulation.State.Status) : "Ready — " + (AssistanceEnabled ? "assisted" : "manual") + " • " + TimeRate + "× selected";
        public double AltitudeMeters => simulation != null ? simulation.State.AltitudeMeters : 0;
        public double SpeedMetersPerSecond => simulation != null ? simulation.State.SpeedRelativeGround : 0;
        public double MissionTimeSeconds => simulation != null ? simulation.State.MissionTime : 0;
        public float Throttle => simulation != null ? (float)simulation.Throttle : pendingThrottle;
        public string WeatherSourceLabel => weatherSource;
        // True when the atmosphere actually driving this launch was built from a
        // frozen weather column. False when the deterministic standard atmosphere is used
        // (no weather available, or an offline launch forced the standard atmosphere).
        public bool LaunchUsesWeather { get; private set; }
        public string FlightTelemetry => simulation == null ? (AssistanceEnabled ? "Assisted" : "Manual") + " • WASD or arrows to steer • " + TimeRate + "× selected" :
            string.Format("{0} • pitch {1:F0}° • {2}× • {3} SOI • {4}", AssistanceEnabled ? "Assisted" : "Manual", simulation.ActualPitchDegrees, TimeRate, simulation.Primary, weatherSource) + (simulation.Celestial != null && !simulation.Celestial.HasReferenceData(simulation.State.MissionTime) ? " • approximate ephemeris" : "") + (simulation.BoosterBurning ? " • SRBs burning (fixed thrust)" : "") + (SimulationCatchingUp ? " • rate limited" : "");
        public RocketSimulation Simulation => simulation;
        public bool HasPosition => simulation != null;
        public DateTime LaunchUtc { get; private set; }
        public void SetPitchTarget(double pitchDegrees)
        {
            if (double.IsNaN(pitchDegrees) || double.IsInfinity(pitchDegrees)) throw new ArgumentOutOfRangeException(nameof(pitchDegrees));
            if (ReentryActive || LunarActive) return;
            AssistanceEnabled = false;
            if (simulation != null) { simulation.DisableManualRotation(); simulation.PitchDegrees = Math.Max(-90, Math.Min(90, pitchDegrees)); }
        }
        public double LatitudeDegrees { get; private set; }
        public double LongitudeDegrees { get; private set; }

        private void Awake()
        {
            CelestialAssets.EnsureLoaded();
            if (director != null) director.FlightDriver = this;
            missionDocument = FindFirstObjectByType<UIDocument>();
            gameObject.AddComponent<MissionAudio>();
        }
        public void Launch(LaunchSite site, WeatherSnapshot weather)
        {
            Launch(site, weather, false);
        }
        public void Launch(LaunchSite site, WeatherSnapshot weather, bool forceOfflineAtmosphere)
        {
            if (IsPaused || started || site == null) return;
            DateTime now = DateTime.UtcNow;
            LaunchUtc = now;
            WeatherProfile profile = null;
            TimeSpan maxAge = TimeSpan.FromHours(12);
            if (!forceOfflineAtmosphere)
            {
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
            }
            var atmosphere = new WeatherAtmosphere(profile, now, maxAge);
            launchAtmosphere = atmosphere;
            LaunchUsesWeather = atmosphere.UsesWeather;
            weatherSource = atmosphere.SourceDescription;
            launchSite = site;
            var preset = SelectedPreset;
            simulation = new RocketSimulation(StepSeconds, preset.Stages, atmosphere,
                site.Latitude * Math.PI / 180, site.Longitude * Math.PI / 180, site.ElevationMeters, preset.Boosters, now);
            MissionSession.Instance?.ConfigureSimulation(simulation);
            director.GetComponent<RocketExplosion>()?.Arm(simulation);
            director.GetComponent<RocketSmokeTrail>()?.Arm(simulation);
            director.GetComponent<FlightTrailRecorder>()?.RecordSimulation(simulation);
            if (simulation.GravityMultiplier != 1) AssistanceEnabled = false;
            guidance = director.VehicleIndex == 2 ? RealVehiclePresets.BuildSlsGuidance() : director.VehicleIndex == 3 ? RealVehiclePresets.BuildSuperStarshipGuidance() : new AssistedAscent(director.VehicleIndex == 1 ? 220000 : 160000, 2);
            simulation.Throttle = pendingThrottle;
            simulation.Launch();
            accumulator = 0;
            started = true;
        }
        private static bool Fresh(DateTime time, DateTime now, TimeSpan age) => time.Kind == DateTimeKind.Utc && time <= now && now - time <= age;
        public void ResetMission()
        {
            ClearSeparatedStages();
            if (director != null) director.GetComponent<RocketSmokeTrail>()?.Clear();
            ClearReentry(); ClearLunarGuidance();
            simulation = null; launchAtmosphere = null; guidance = null; launchSite = null; started = false; accumulator = 0; orbitSeconds = 0; OrbitVerified = false;
            pendingThrottle = 0; AssistanceEnabled = true; TimeRate = 1; actualTimeRate = 0; SimulationCatchingUp = false;
            LatitudeDegrees = LongitudeDegrees = 0;
            LaunchUtc = default;
            weatherSource = "Standard atmosphere (no launch yet)";
            LaunchUsesWeather = false;
            if (director != null && director.EnvironmentRoot != null) director.EnvironmentRoot.position = Vector3.zero;
            if (director != null && director.RocketVisual != null)
            {
                director.RocketVisual.localPosition = Vector3.up * director.RocketBaseHeightMeters;
                director.RocketVisual.localRotation = Quaternion.identity;
                var view = director.RocketVisual.GetComponent<RocketPresentation>();
                foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true)) plume.SetFlow(0, 101325);
                foreach (string name in new[] { "LeftSRB", "RightSRB" }) { var solid = director.RocketVisual.Find(name); if (solid != null) solid.gameObject.SetActive(true); }
                if (view != null) { view.SetThrottle(0); if (view.booster != null) view.booster.gameObject.SetActive(true); }
            }
            if (director != null) { director.ResetLaunchFraming(); director.RefreshFlightBackdrop(); }
        }
        public void Stage() { if (ReentryActive || LunarActive || IsPaused || simulation == null) return; if (TimeRate > 20) TimeRate = 20; InvalidateOrbitVerification(); if (simulation.BoosterAttached && simulation.BoosterPropellantKg <= 0) simulation.JettisonBoosters(); else simulation.Stage(); }
        public void SetThrottle(float value) { if (ReentryActive || LunarActive || IsPaused) return; AssistanceEnabled = false; pendingThrottle = Mathf.Clamp01(value); if (simulation != null) simulation.Throttle = pendingThrottle; if (pendingThrottle > 0) InvalidateOrbitVerification(); if (TimeRate > 20 && !CanHighWarp) TimeRate = 20; }

        private void Update()
        {
            if (simulation == null || IsPaused || MissionSession.BlocksInput) return;
            DetectSeparatedStages();
            if (simulation.State.IsLanded) { returnController?.Update(simulation); if(LunarActive) lunarGuidance.Update(simulation); accumulator = 0; RenderState(); return; }
            if (AdvanceReturnPlanning() || AdvanceLunarPlanning()) { accumulator = 0; actualTimeRate = 0; RenderState(); return; }
            Keyboard keyboard = Keyboard.current;
            var focused = missionDocument?.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
            bool editingText = focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            bool controlFocused = focused is Button || focused is Slider ||
                focused?.GetFirstAncestorOfType<Button>() != null || focused?.GetFirstAncestorOfType<Slider>() != null;
            if (!ReentryActive && !LunarActive && keyboard != null && Application.isFocused && !editingText && !simulation.State.IsLanded)
            {
                float pitchInput = (keyboard.sKey.isPressed || (!controlFocused && keyboard.downArrowKey.isPressed) ? 1 : 0) - (keyboard.wKey.isPressed || (!controlFocused && keyboard.upArrowKey.isPressed) ? 1 : 0);
                float headingInput = (keyboard.dKey.isPressed || (!controlFocused && keyboard.rightArrowKey.isPressed) ? 1 : 0) - (keyboard.aKey.isPressed || (!controlFocused && keyboard.leftArrowKey.isPressed) ? 1 : 0);
                float rollInput = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                bool steering = pitchInput != 0 || headingInput != 0 || rollInput != 0;
                bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                if (steering) AssistanceEnabled = false;
                if (!AssistanceEnabled) simulation.SetRotationInputs(pitchInput * (shift ? 1 : .5), -headingInput * (shift ? 1 : .5), rollInput * (shift ? 1 : .5));
                float throttleInput = (shift && !steering ? 1 : 0) -
                    (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ? 1 : 0);
                if (throttleInput != 0) SetThrottle(Throttle + throttleInput * .35f * Time.unscaledDeltaTime);
                if (!controlFocused && keyboard.spaceKey.wasPressedThisFrame) Stage();
            }
            else if (!ReentryActive && !LunarActive && !AssistanceEnabled) simulation.SetRotationInputs(0, 0, 0);
            // Cap requested work to what can be simulated this frame. Do not queue
            // unbounded future flight after the player selects a lower rate.
            if (ReturnNeedsRealTime || LunarNeedsRealTime) TimeRate = 1;
            else if (TimeRate > 20 && !CanHighWarp) TimeRate = 20;
            double requested = accumulator + Time.unscaledDeltaTime * TimeRate;
            SimulationCatchingUp = requested >= 500 * StepSeconds;
            accumulator = Math.Min(requested, 500 * StepSeconds);
            int steps = 0;
            while (accumulator >= StepSeconds && steps < 500)
            {
                if (ReentryActive) returnController.Update(simulation);
                else if (LunarActive) lunarGuidance.Update(simulation);
                else if (AssistanceEnabled && guidance != null) guidance.Update(simulation);
                DetectSeparatedStages();
                AdvanceSeparatedStages(StepSeconds);
                simulation.Step();
                DetectSeparatedStages();
                if (simulation.State.IsComplete) { returnController?.Update(simulation); if(LunarActive) lunarGuidance.Update(simulation); }
                UpdateOrbitVerification(); accumulator -= StepSeconds; steps++;
                if (simulation.State.IsLanded)
                {
                    // Impact is terminal. Stop the controls and work backlog immediately;
                    // the final finite pose is still rendered below and reset stays available.
                    simulation.Throttle = 0; pendingThrottle = 0;
                    AssistanceEnabled = false; TimeRate = 1; accumulator = 0;
                    break;
                }
                if (TimeRate > 1 && (ReturnNeedsRealTime || LunarNeedsRealTime)) { TimeRate = 1; accumulator = 0; break; }
                if (TimeRate > 20 && !CanHighWarp) { TimeRate = 20; accumulator = 0; break; }
            }
            if (Time.unscaledDeltaTime > 0)
            {
                double measuredRate = steps * StepSeconds / Time.unscaledDeltaTime;
                double weight = 1 - Math.Exp(-Time.unscaledDeltaTime * 3);
                actualTimeRate += (measuredRate - actualTimeRate) * weight;
            }
            RenderState();
        }
        private void UpdateOrbitVerification()
        {
            const double minimumClearanceMeters = 180_000;
            const double requiredCoastSeconds = 60;
            if (simulation.HasUsedCheats || (MissionSession.Instance != null && MissionSession.Instance.ModifiedFlight)) { InvalidateOrbitVerification(); return; }
            var state = simulation.State;
            bool unpowered = !simulation.BoosterBurning && (simulation.Throttle <= 0 || state.PropellantKg <= 0);
            if (simulation.Primary != CelestialPrimary.Earth || !state.IsLaunched || state.IsLanded || !unpowered)
            {
                InvalidateOrbitVerification();
                return;
            }

            var orbit = OrbitDiagnostics.Compute(state.Position, state.Velocity);
            bool clearsEarth = orbit.IsBound && orbit.MinimumWgs84AltitudeBoundMeters >= minimumClearanceMeters;
            orbitSeconds = clearsEarth ? orbitSeconds + StepSeconds : 0;
            OrbitVerified = orbitSeconds >= requiredCoastSeconds;
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
            RenderSeparatedStages(ecef, lat, lon, state.MissionTime);
            if (director.RocketVisual != null)
            {
                director.RocketVisual.position = Vector3.up * director.RocketBaseHeightMeters;
                float pressure = (float)simulation.Physics.PressurePascals;
                foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true))
                    plume.SetFlow(!state.IsLanded && (plume.solidBooster ? simulation.BoosterBurning : plume.stageIndex == state.StageIndex && state.IsLaunched && state.PropellantKg > 0) ? (plume.solidBooster ? 1 : (float)simulation.Throttle) : 0, pressure);
                foreach (string name in new[] { "LeftSRB", "RightSRB" }) { var solid = director.RocketVisual.Find(name); if (solid != null) solid.gameObject.SetActive(simulation.BoosterAttached); }
                var view = director.RocketVisual.GetComponent<RocketPresentation>();
                if (view != null)
                {
                    if (view.booster != null) view.booster.gameObject.SetActive(state.StageIndex == 0);
                    view.SetThrottle(state.IsLaunched && !state.IsLanded && state.PropellantKg > 0 ? (float)simulation.Throttle : 0);
                }
                // Render the same thrust axis used by the force model, expressed
                // in the Earth-fixed pad frame used by the camera-relative scene.
                var axis = EarthFrames.InertialToEcef(simulation.ThrustDirectionInertial,
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
                double x = axis.X, y = axis.Y, z = axis.Z;
                double localEast = -Math.Sin(lon) * x + Math.Cos(lon) * y;
                double localNorth = -Math.Sin(lat) * Math.Cos(lon) * x - Math.Sin(lat) * Math.Sin(lon) * y + Math.Cos(lat) * z;
                double localUp = Math.Cos(lat) * Math.Cos(lon) * x + Math.Cos(lat) * Math.Sin(lon) * y + Math.Sin(lat) * z;
                Vector3 direction = new Vector3((float)localEast, (float)localUp, (float)localNorth).normalized;
                // Choose a deterministic perpendicular reference; the flight model supplies no roll.
                if (simulation.ManualRotation && !ReentryActive)
                {
                    var forward = EarthFrames.InertialToEcef(simulation.ManualForwardInertial, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
                    Vector3 localForward = new Vector3((float)(-Math.Sin(lon)*forward.X+Math.Cos(lon)*forward.Y), (float)(Math.Cos(lat)*Math.Cos(lon)*forward.X+Math.Cos(lat)*Math.Sin(lon)*forward.Y+Math.Sin(lat)*forward.Z), (float)(-Math.Sin(lat)*Math.Cos(lon)*forward.X-Math.Sin(lat)*Math.Sin(lon)*forward.Y+Math.Cos(lat)*forward.Z));
                    director.RocketVisual.rotation = Quaternion.LookRotation(localForward, direction);
                }
                else director.RocketVisual.rotation = Quaternion.FromToRotation(Vector3.up, direction);
                RenderReentry(ecef, lat, lon);
            }

        }
    }
}
