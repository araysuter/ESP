using System;
using System.Globalization;
using FlightCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    public enum MissionMenuPage { None, Start, Pause, Settings, Cheats }

    /// <summary>Owns menu state, explicit fixed-step pause and per-flight sandbox overrides.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MissionSession : MonoBehaviour
    {
        internal const float DefaultMilkyWayBrightness = 500f;
        internal const float DefaultMilkyWaySaturation = 200f;

        public static MissionSession Instance { get; private set; }
        public static bool BlocksInput => Instance != null && Instance.Page != MissionMenuPage.None;
        public static bool IsSimulationPaused => BlocksInput;
        public MissionMenuPage Page { get; private set; } = MissionMenuPage.Start;
        public bool HasSession { get; private set; }
        public bool PhysicsVisible { get; private set; }
        public double GravityMultiplier { get; private set; } = 1;
        public bool InfiniteFuel { get; private set; }
        public bool CrashDestructionEnabled { get; private set; } = true;
        public bool FlightDestroyed => driver?.Simulation?.IsDestroyed ?? false;
        public bool ModifiedFlight => modified || (driver?.Simulation?.HasUsedCheats ?? false);
        public int FrameRateLimit { get; private set; } = 60;
        public bool VSync { get; private set; }
        public float AuroraBrightness { get; private set; } = 1;
        public float AuroraColorIntensity { get; private set; } = 1;
        public float AuroraStormStrength { get; private set; } = 1;
        public float MilkyWayBrightness { get; private set; } = DefaultMilkyWayBrightness;
        public float MilkyWaySaturation { get; private set; } = DefaultMilkyWaySaturation;
        public string PhysicsText { get; private set; } = "Launch to inspect forces.\nSI units · authoritative flight core";
        private MissionDirector director;
        private RocketFlightDriver driver;
        private MissionMenuPage returnPage = MissionMenuPage.Start;
        private bool modified, resumeMap = true;
        private float nextTelemetry;
        private static readonly int[] FrameLimits = { 0, 30, 60, 90, 120, 144 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var ui = FindFirstObjectByType<MissionUI>();
            if (ui == null || Instance != null) return;
            ui.gameObject.AddComponent<MissionSession>();
            ui.gameObject.AddComponent<MissionMenuUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            int saved = PlayerPrefs.GetInt("ESP.Video.FrameLimit", 60);
            FrameRateLimit = Array.IndexOf(FrameLimits, saved) >= 0 ? saved : 60;
            VSync = PlayerPrefs.GetInt("ESP.Video.VSync", 0) == 1;
            AuroraBrightness = Mathf.Clamp(PlayerPrefs.GetFloat("ESP.Video.AuroraBrightness", 1), 0, 2);
            AuroraColorIntensity = Mathf.Clamp(PlayerPrefs.GetFloat("ESP.Video.AuroraColor", 1), 0, 2);
            AuroraStormStrength = Mathf.Clamp(PlayerPrefs.GetFloat("ESP.Video.AuroraStorm", 1), 0, 3);
            MilkyWayBrightness = Mathf.Clamp(PlayerPrefs.GetFloat("ESP.Video.MilkyWayBrightness", DefaultMilkyWayBrightness), 10, 500);
            MilkyWaySaturation = Mathf.Clamp(PlayerPrefs.GetFloat("ESP.Video.MilkyWaySaturation", DefaultMilkyWaySaturation), 10, 300);
            ApplyVideo();
            string[] args = Environment.GetCommandLineArgs();
            // Existing opt-in flight rehearsals retain their explicit entry path.
            if (Array.IndexOf(args, "-espMenuSmoke") < 0 &&
                (Array.IndexOf(args, "-espFlightSmoke") >= 0 || Array.IndexOf(args, "-espOfflineFlightSmoke") >= 0 || Array.IndexOf(args, "-espVehiclePreview") >= 0 || Array.IndexOf(args, "-espMissionCapture") >= 0))
            { HasSession = true; Page = MissionMenuPage.None; }
            SyncPause();
        }

        private void Update()
        {
            if (Application.isFocused && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Page == MissionMenuPage.None) OpenPause();
                else if (Page == MissionMenuPage.Pause) ResumeMission();
                else if (Page != MissionMenuPage.Start) Back();
            }
            SyncPause();
            if (Time.unscaledTime >= nextTelemetry)
            {
                nextTelemetry = Time.unscaledTime + .25f;
                RefreshTelemetry();
            }
        }

        private void SyncPause() { if (driver != null) driver.IsPaused = Page != MissionMenuPage.None; }
        private void Navigate(MissionMenuPage page) { Page = page; SyncPause(); }
        public void NewMission()
        {
            ClearMissionOverrides();
            director?.ResetMission();
            director?.ShowMap();
            HasSession = true; resumeMap = true;
            Navigate(MissionMenuPage.None);
        }
        public void ResumeMission()
        {
            if (!HasSession) return;
            if (Page == MissionMenuPage.Start && director != null)
            {
                if (resumeMap) director.ShowMap(); else director.GoToLaunch(director.SelectedSiteIndex);
            }
            Navigate(MissionMenuPage.None);
        }
        public void OpenPause() { if (HasSession) Navigate(MissionMenuPage.Pause); }
        public void OpenSettings() { returnPage = HasSession && Page != MissionMenuPage.Start ? MissionMenuPage.Pause : MissionMenuPage.Start; Navigate(MissionMenuPage.Settings); }
        public void OpenCheats() { if (HasSession) { returnPage = MissionMenuPage.Pause; Navigate(MissionMenuPage.Cheats); } }
        public void Back() { if (Page == MissionMenuPage.Settings || Page == MissionMenuPage.Cheats) Navigate(returnPage); else if (Page == MissionMenuPage.Pause) ResumeMission(); }
        public void ReturnToStart()
        {
            resumeMap = director == null || director.IsMapView;
            director?.ShowMap();
            Navigate(MissionMenuPage.Start);
        }
        public void RestartMission()
        {
            ClearMissionOverrides();
            director?.ResetMission();
            if (director != null) director.GoToLaunch(director.SelectedSiteIndex);
            HasSession = true;
            Navigate(MissionMenuPage.None);
        }
        private void ClearMissionOverrides()
        {
            modified = false; GravityMultiplier = 1; InfiniteFuel = false; CrashDestructionEnabled = true;
        }
        public void Quit() { PlayerPrefs.Save(); Application.Quit(); }
        public void SetPhysicsVisible(bool visible) { PhysicsVisible = visible; }
        public void SetGravityMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 5) throw new ArgumentOutOfRangeException(nameof(value));
            GravityMultiplier = value; modified |= value != 1; ApplyOverrides();
        }
        public void SetInfiniteFuel(bool value) { InfiniteFuel = value; modified |= value; ApplyOverrides(); }
        public void SetCrashDestruction(bool value) { CrashDestructionEnabled = value; modified |= !value; ApplyOverrides(); }
        public void ResetCheats() { GravityMultiplier = 1; InfiniteFuel = false; CrashDestructionEnabled = true; ApplyOverrides(); }
        private void ApplyOverrides()
        {
            if (driver != null)
            {
                if (GravityMultiplier != 1) driver.DisableAssistance();
                if (driver.Simulation != null) ConfigureSimulation(driver.Simulation);
                if (ModifiedFlight) driver.InvalidateOrbitVerification();
            }
        }
        public void ConfigureSimulation(RocketSimulation simulation)
        {
            simulation.GravityMultiplier = GravityMultiplier;
            simulation.InfiniteFuel = InfiniteFuel;
            simulation.CrashDestructionEnabled = CrashDestructionEnabled;
        }
        public void SetFrameRateLimit(int value)
        {
            if (Array.IndexOf(FrameLimits, value) < 0) throw new ArgumentOutOfRangeException(nameof(value));
            FrameRateLimit = value;
            if (value != 0) VSync = false;
            ApplyVideo(); SaveVideo();
        }
        public void SetVSync(bool value) { VSync = value; ApplyVideo(); SaveVideo(); }
        public void SetAuroraBrightness(float value) { AuroraBrightness = SaveAurora("ESP.Video.AuroraBrightness", value, 2); }
        public void SetAuroraColorIntensity(float value) { AuroraColorIntensity = SaveAurora("ESP.Video.AuroraColor", value, 2); }
        public void SetAuroraStormStrength(float value) { AuroraStormStrength = SaveAurora("ESP.Video.AuroraStorm", value, 3); }
        private static float SaveAurora(string key, float value, float maximum)
        {
            value = float.IsNaN(value) ? 0 : Mathf.Clamp(value, 0, maximum);
            PlayerPrefs.SetFloat(key, value); PlayerPrefs.Save(); return value;
        }
        public void SetMilkyWayBrightness(float value) { MilkyWayBrightness = Mathf.Clamp(value, 10, 500); PlayerPrefs.SetFloat("ESP.Video.MilkyWayBrightness", MilkyWayBrightness); PlayerPrefs.Save(); }
        public void SetMilkyWaySaturation(float value) { MilkyWaySaturation = Mathf.Clamp(value, 10, 300); PlayerPrefs.SetFloat("ESP.Video.MilkyWaySaturation", MilkyWaySaturation); PlayerPrefs.Save(); }
        private void ApplyVideo() { QualitySettings.vSyncCount = VSync ? 1 : 0; Application.targetFrameRate = VSync || FrameRateLimit == 0 ? -1 : FrameRateLimit; }
        private void SaveVideo() { PlayerPrefs.SetInt("ESP.Video.FrameLimit", FrameRateLimit); PlayerPrefs.SetInt("ESP.Video.VSync", VSync ? 1 : 0); PlayerPrefs.Save(); }
        private void RefreshTelemetry()
        {
            if (driver?.Simulation == null) { PhysicsText = "Launch to inspect forces.\nSI units · authoritative flight core"; return; }
            var simulation = driver.Simulation;
            var state = simulation.State;
            var p = simulation.Physics;
            PhysicsText = (simulation.IsDestroyed ? "DESTROYED — GROUND IMPACT\n\n" : "") + string.Format(CultureInfo.InvariantCulture,
                "Gravity          {0:F3} m/s²\nAir pressure     {1:N2} Pa\nAir density      {2:G4} kg/m³\nTemperature      {3:F1} K\nAir-relative v   {4:N1} m/s\nDynamic pressure {5:N1} Pa\nDrag             {6:N1} N\nThrust           {7:N0} N\nFree-flight a    {8:F3} m/s²\nFuel · stage     {9:N1} kg\nFuel · attached  {10:N1} kg\nVehicle mass     {11:N1} kg\n\n{12}",
                p.GravityMetersPerSecondSquared, p.PressurePascals, p.DensityKgPerCubicMeter, p.TemperatureKelvin,
                p.AirSpeedMetersPerSecond, p.DynamicPressurePascals, p.DragForceNewtons, p.ThrustNewtons,
                p.NetAccelerationMetersPerSecondSquared, state.PropellantKg, p.TotalPropellantKg, state.MassKg,
                state.IsLanded || !state.IsLaunched ? "Surface contact constrains motion." : driver.WeatherSourceLabel);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
