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
            bool preview = Array.IndexOf(Environment.GetCommandLineArgs(), "-espVehiclePreview") >= 0;
            if (!preview && Array.IndexOf(Environment.GetCommandLineArgs(), "-espFlightSmoke") < 0) yield break;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            yield return null;
            director.GoToLaunch(1);
            string[] arguments = Environment.GetCommandLineArgs();
            int vehicleFlag = Array.IndexOf(arguments, "-espVehicle");
            if (vehicleFlag >= 0 && vehicleFlag + 1 < arguments.Length && int.TryParse(arguments[vehicleFlag + 1], out int selectedVehicle))
                director.SelectVehicle(selectedVehicle);
            Debug.Log("ESP_FLIGHT_VEHICLE " + director.VehicleLabel);
            if (preview)
            {
                yield return new WaitForSecondsRealtime(2);
                Capture("vehicle-review-front.png");
                yield return new WaitForSecondsRealtime(1);
                Quaternion originalRotation = director.RocketVisual.localRotation;
                director.RocketVisual.Rotate(0, 180, 0, Space.Self);
                yield return new WaitForSecondsRealtime(1);
                Capture("vehicle-review-rear.png");
                yield return new WaitForSecondsRealtime(1);
                director.RocketVisual.localRotation = originalRotation;
                director.Launch();
                yield return new WaitForSecondsRealtime(10);
                Capture("vehicle-review-powered.png");
                yield return new WaitForSecondsRealtime(1);
                Debug.Log("ESP_VEHICLE_REVIEW_OK pad views and 10 seconds simulated flight");
                Application.Quit(); yield break;
            }
            float end = Time.realtimeSinceStartup + 110;
            bool skipWeatherWait = Array.IndexOf(arguments, "-espSkipWeatherWait") >= 0;
            while (!skipWeatherWait && director.nasaWeather != null && director.nasaWeather.Snapshot == null && Time.realtimeSinceStartup < end)
                yield return new WaitForSecondsRealtime(1);
            Debug.Log("ESP_FLIGHT_WEATHER " + (director.nasaWeather != null ? director.nasaWeather.Status : "missing"));
            director.Launch();
            driver.SetTimeRate(20);
            bool launchShot = false, stageShot = false;
            end = Time.realtimeSinceStartup + 240;
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
            var orbit = director.OrbitVisual != null ? director.OrbitVisual.Projection : null;
            if (orbit == null || !orbit.IsValid || !orbit.IsClosed || orbit.HasImpact)
            { Debug.LogError("ESP_ORBIT_MAP_FAIL " + (orbit == null ? "missing" : orbit.Status)); Application.Quit(1); yield break; }
            Debug.Log("ESP_ORBIT_MAP_OK points=" + orbit.EcefPoints.Count + " closed=" + orbit.IsClosed);
            director.mapCamera.GetComponent<GlobeCameraControls>()?.Focus(driver.LatitudeDegrees, driver.LongitudeDegrees);
            yield return new WaitForSecondsRealtime(1);
            Capture("flight-map.png");
            yield return new WaitForSecondsRealtime(1);
            if (Array.IndexOf(arguments, "-espGraphicsReview") >= 0)
            {
                director.GoToLaunch(director.SelectedSiteIndex);
                driver.SetThrottle(1);
                yield return new WaitForSecondsRealtime(4);
                Capture("flight-vacuum-burn.png");
                Debug.Log("ESP_VACUUM_BURN_REVIEW altitude=" + driver.AltitudeMeters + " throttle=" + driver.Throttle);
                yield return new WaitForSecondsRealtime(1);
                driver.SetThrottle(0);
            }
            if (Array.IndexOf(arguments, "-espKeepOpen") >= 0)
            { Debug.Log("ESP_FLIGHT_REVIEW_READY verified simulated orbit; map controls available"); yield break; }
            director.GoToLaunch(0);
            if (!driver.CanLaunch || driver.MissionTimeSeconds != 0) { Debug.LogError("ESP_FLIGHT_RESET_FAIL"); Application.Quit(1); yield break; }
            Debug.Log("ESP_FLIGHT_SMOKE_OK orbit, map, site-change reset");
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
        }
        private static void Capture(string file) { ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, file)); }
    }
}
