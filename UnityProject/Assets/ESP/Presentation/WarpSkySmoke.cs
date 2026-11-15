using System;
using System.Collections;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in development-player test of real orbit warp and sky settings.</summary>
    public sealed class WarpSkySmoke : MonoBehaviour
    {
        private const string BrightnessKey = "ESP.Video.MilkyWayBrightness";
        private const string SaturationKey = "ESP.Video.MilkyWaySaturation";
        private bool hadBrightness, hadSaturation, saved, restored;
        private float brightness, saturation;
        private int frameLimit, vsync;
        private MissionSession session;
        private RocketFlightDriver driver;
        private MissionDirector director;
        private MissionSunlight sunlight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espWarpSkySmoke") >= 0)
                new GameObject("Warp and sky regression").AddComponent<WarpSkySmoke>();
        }

        private void Restore()
        {
            if (!saved || restored) return;
            restored = true;
            if (session != null)
            {
                session.SetMilkyWayBrightness(brightness);
                session.SetMilkyWaySaturation(saturation);
            }
            if (hadBrightness) PlayerPrefs.SetFloat(BrightnessKey, brightness); else PlayerPrefs.DeleteKey(BrightnessKey);
            if (hadSaturation) PlayerPrefs.SetFloat(SaturationKey, saturation); else PlayerPrefs.DeleteKey(SaturationKey);
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = frameLimit;
            if (sunlight != null) sunlight.ReviewUtc = null;
        }
        private void OnDestroy() { Restore(); }
        private bool Check(bool condition, string reason)
        {
            if (condition) return true;
            Debug.LogError("ESP_WARP_SKY_FAIL " + reason);
            Restore();
            Application.Quit(1);
            return false;
        }
        private static bool Close(float a, float b) => Mathf.Abs(a - b) < .001f;
        private static bool Close(Vector4 a, Vector4 b) => (a - b).sqrMagnitude < 1e-8f;
        private static Material Sky(Camera camera)
        {
            var field = camera ? camera.GetComponentInChildren<StarField>(true) : null;
            var renderer = field ? field.GetComponent<MeshRenderer>() : null;
            return renderer ? renderer.sharedMaterial : null;
        }

        private IEnumerator Start()
        {
            yield return null;
            session = MissionSession.Instance;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            sunlight = FindFirstObjectByType<MissionSunlight>();
            if (!Check(session && director && driver && sunlight, "missing mission components")) yield break;
            hadBrightness = PlayerPrefs.HasKey(BrightnessKey); hadSaturation = PlayerPrefs.HasKey(SaturationKey);
            brightness = PlayerPrefs.GetFloat(BrightnessKey, MissionSession.DefaultMilkyWayBrightness);
            saturation = PlayerPrefs.GetFloat(SaturationKey, MissionSession.DefaultMilkyWaySaturation);
            if (!Check(Close(session.MilkyWayBrightness, hadBrightness ? Mathf.Clamp(brightness, 10, 500) : 500) &&
                Close(session.MilkyWaySaturation, hadSaturation ? Mathf.Clamp(saturation, 10, 300) : 200),
                "initial sky defaults or saved preferences")) yield break;
            frameLimit = Application.targetFrameRate; vsync = QualitySettings.vSyncCount; saved = true;
            session.NewMission(false); director.SelectVehicle(0); director.ShowMap();
            sunlight.ReviewUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc);
            session.OpenPause(); session.OpenSettings();
            if (!Check(driver.IsPaused && MissionSession.BlocksInput, "settings must pause and block input")) yield break;
            session.SetMilkyWayBrightness(-1); session.SetMilkyWaySaturation(999);
            if (!Check(Close(session.MilkyWayBrightness, 10) && Close(session.MilkyWaySaturation, 300) &&
                Close(PlayerPrefs.GetFloat(BrightnessKey), 10) && Close(PlayerPrefs.GetFloat(SaturationKey), 300), "clamp/save")) yield break;
            session.SetMilkyWayBrightness(500); session.SetMilkyWaySaturation(10);
            yield return null; yield return null;
            var map = Sky(director.mapCamera);
            if (!Check(map && Close(map.GetVector("_SkyAdjust"), new Vector4(5, .1f, 0, 0)), "map sky parameters")) yield break;
            Vector4 sun = map.GetVector("_SunSky");
            session.SetMilkyWayBrightness(125); session.SetMilkyWaySaturation(175);
            yield return null; yield return null;
            if (!Check(Close(map.GetVector("_SkyAdjust"), new Vector4(1.25f, 1.75f, 0, 0)) &&
                Close(sun, map.GetVector("_SunSky")) && sun.w > 0 &&
                Close(PlayerPrefs.GetFloat(BrightnessKey), 125) && Close(PlayerPrefs.GetFloat(SaturationKey), 175), "map settings or sun changed")) yield break;
            session.Back(); session.ResumeMission(); director.GoToLaunch(0);
            yield return null; yield return null;
            var flightSky = FindFirstObjectByType<FlightEarthBackdrop>();
            var flightMaterial = flightSky ? Sky(flightSky.BackgroundCamera) : null;
            if (!Check(flightMaterial && flightMaterial != map &&
                Close(flightMaterial.GetVector("_SkyAdjust"), new Vector4(1.25f, 1.75f, 0, 0)), "flight sky material/settings")) yield break;
            var inset = director.Inset != null ? Sky(director.Inset.MapPreviewCamera) : null;
            if (!Check(inset && inset != map && inset != flightMaterial &&
                Close(inset.GetVector("_SkyAdjust"), new Vector4(1.25f, 1.75f, 0, 0)), "inset sky material/settings")) yield break;
            Vector4 flightSun = flightMaterial.GetVector("_SunSky");
            Vector4 insetSun = inset.GetVector("_SunSky");
            session.SetMilkyWayBrightness(250); session.SetMilkyWaySaturation(200);
            yield return null; yield return null;
            if (!Check(Close(flightMaterial.GetVector("_SkyAdjust"), new Vector4(2.5f, 2, 0, 0)) &&
                Close(flightSun, flightMaterial.GetVector("_SunSky")) && flightSun.w > 0 &&
                Close(inset.GetVector("_SkyAdjust"), new Vector4(2.5f, 2, 0, 0)) &&
                Close(insetSun, inset.GetVector("_SunSky")) && insetSun.w > 0, "flight/inset sun changed with settings")) yield break;
            sunlight.ReviewUtc = null;
            director.Launch();
            driver.SetTimeRate(50);
            if (!Check(driver.TimeRate == 1 && !driver.OrbitVerified, "high warp allowed before orbit")) yield break;
            driver.SetTimeRate(20);
            float deadline = Time.realtimeSinceStartup + 200;
            while (!driver.OrbitVerified && Time.realtimeSinceStartup < deadline && driver.Simulation != null && !driver.Simulation.State.IsLanded) yield return null;
            if (!Check(driver.OrbitVerified && driver.CanHighWarp, "nominal flight did not verify orbit")) yield break;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 30;
            driver.SetTimeRate(1000);
            if (!Check(driver.TimeRate == 1000, "1000x rejected in verified coast")) yield break;
            double startMission = driver.MissionTimeSeconds;
            float startWall = Time.realtimeSinceStartup;
            bool limited = false;
            while (Time.realtimeSinceStartup - startWall < 1.5f) { yield return null; limited |= driver.SimulationCatchingUp; }
            double wall = Time.realtimeSinceStartup - startWall, elapsed = driver.MissionTimeSeconds - startMission;
            double rate = elapsed / wall;
            if (!Check(limited && rate > 0 && rate < 1000 && driver.ActualTimeRate > 0 &&
                Math.Abs(driver.ActualTimeRate - rate) < Math.Max(150, rate * .5), "capped achieved rate=" + rate + " displayed=" + driver.ActualTimeRate)) yield break;
            driver.SetTimeRate(1); startMission = driver.MissionTimeSeconds; startWall = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startWall < 1) yield return null;
            elapsed = driver.MissionTimeSeconds - startMission; wall = Time.realtimeSinceStartup - startWall;
            if (!Check(Math.Abs(elapsed - wall) < .2, "lower rate retained backlog: " + elapsed + "s for " + wall + "s")) yield break;
            driver.SetTimeRate(1000); driver.Stage();
            if (!Check(driver.TimeRate <= 20 && !driver.OrbitVerified && !driver.CanHighWarp, "stage fallback/verification")) yield break;
            driver.SetTimeRate(1000);
            if (!Check(driver.TimeRate <= 20, "staged flight immediately regained high warp")) yield break;
            driver.SetTimeRate(20); deadline = Time.realtimeSinceStartup + 30;
            while (!driver.OrbitVerified && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(driver.OrbitVerified, "coast did not reverify after stage")) yield break;
            driver.SetTimeRate(1000); driver.SetThrottle(1); driver.SetThrottle(0);
            if (!Check(driver.TimeRate <= 20 && !driver.OrbitVerified && !driver.CanHighWarp, "throttle fallback/verification")) yield break;
            driver.SetTimeRate(1000);
            if (!Check(driver.TimeRate <= 20, "throttle immediately regained high warp")) yield break;
            session.OpenPause();
            double pausedTime = driver.MissionTimeSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(driver.MissionTimeSeconds == pausedTime && driver.ActualTimeRate == 0, "pause advanced flight")) yield break;
            session.OpenCheats(); session.SetInfiniteFuel(true);
            if (!Check(!driver.OrbitVerified && !driver.CanHighWarp, "cheat retained warp eligibility")) yield break;
            session.ResumeMission();
            director.ResetMission();
            if (!Check(driver.TimeRate == 1 && driver.MissionTimeSeconds == 0 && !driver.OrbitVerified, "reset")) yield break;
            Debug.Log("ESP_WARP_SKY_OK nominal orbit, gate, capped warp, rate reduction, stage, throttle, reset, sky settings and sun parameters");
            Restore(); Application.Quit(0);
        }
    }
}
