using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    // Opt-in packaged verification: all motion comes from the shipping fixed-step driver.
    public sealed class MissionFlightSmoke : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espFlightSmoke") < 0) yield break;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            yield return null;
            director.GoToLaunch(1);
            float end = Time.realtimeSinceStartup + 110;
            while (director.nasaWeather != null && director.nasaWeather.Snapshot == null && Time.realtimeSinceStartup < end)
                yield return new WaitForSecondsRealtime(1);
            Debug.Log("ESP_FLIGHT_WEATHER " + (director.nasaWeather != null ? director.nasaWeather.Status : "missing"));
            director.Launch();
            driver.SetTimeRate(20);
            bool launchShot = false, stageShot = false;
            end = Time.realtimeSinceStartup + 150;
            while (Time.realtimeSinceStartup < end && !driver.OrbitVerified)
            {
                if (!launchShot && driver.MissionTimeSeconds > 8) { launchShot = true; Capture("flight-launch.png"); }
                if (!stageShot && driver.Simulation != null && driver.Simulation.State.StageIndex == 1)
                { stageShot = true; Capture("flight-staging.png"); Debug.Log("ESP_FLIGHT_STAGE t=" + driver.MissionTimeSeconds); }
                if (driver.Simulation != null && driver.Simulation.State.IsLanded) break;
                yield return null;
            }
            if (!driver.OrbitVerified) { Debug.LogError("ESP_FLIGHT_SMOKE_FAIL " + driver.Status); Application.Quit(1); yield break; }
            driver.SetTimeRate(1);
            Debug.Log("ESP_FLIGHT_ORBIT_OK t=" + driver.MissionTimeSeconds + " altitude=" + driver.AltitudeMeters + " source=" + driver.WeatherSourceLabel);
            yield return new WaitForSecondsRealtime(.3f);
            Capture("flight-orbit.png");
            yield return new WaitForSecondsRealtime(1);
            director.ShowMap();
            yield return new WaitForSecondsRealtime(1);
            Capture("flight-map.png");
            yield return new WaitForSecondsRealtime(1);
            director.GoToLaunch(0);
            if (!driver.CanLaunch || driver.MissionTimeSeconds != 0) { Debug.LogError("ESP_FLIGHT_RESET_FAIL"); Application.Quit(1); yield break; }
            Debug.Log("ESP_FLIGHT_SMOKE_OK orbit, map, site-change reset");
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
        }
        private static void Capture(string file) { ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, file)); }
    }
}
