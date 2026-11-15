using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Bounded opt-in review with actual dated provider data, never synthetic weather.</summary>
    public sealed class CloudNightSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espCloudNightSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director) director.gameObject.AddComponent<CloudNightSmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            var lighting = GetComponent<MissionSunlight>();
            var layers = director.mapLayers;
            MissionSession.Instance.NewMission(false); director.SelectVehicle(0); director.ShowMap();
            layers.Select(1);
            float deadline = Time.realtimeSinceStartup + 125;
            while (!layers.CloudsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("ESP_CLOUD_NIGHT_DATA " + layers.DisplayedDescription + " | " + layers.Status);
            if (!layers.CloudsVisible) { Fail("No observed cloud data loaded; fallback retained"); yield break; }
            var controls = director.mapCamera.GetComponent<GlobeCameraControls>();
            controls.Focus(30, -90);
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc);
            yield return Shot("map-night-observed");
            float strength = lighting.nightLightsStrength;
            lighting.nightLightsStrength = 0;
            yield return Shot("map-night-cloud-only");
            lighting.nightLightsStrength = strength;
            var shell = layers.earth.GetComponent<CloudShell>();
            shell.Hide(); yield return Shot("map-night-clear-reference");
            shell.Show(layers.CloudDensityTexture, layers.CloudShader);
            foreach (int hour in new[] { 18, 0 })
            {
                lighting.ReviewUtc = new DateTime(2026, 3, 20, hour, 0, 0, DateTimeKind.Utc);
                yield return Shot("map-hour-" + hour);
            }
            var reviewLatitudes = new[] { 30f, 30f, 0f, 85f, -85f };
            var reviewLongitudes = new[] { 20f, 115f, 180f, 0f, 0f };
            var reviewHours = new[] { 11, 4, 0, 12, 12 };
            for (int region = 0; region < reviewLatitudes.Length; region++)
            {
                controls.Focus(reviewLatitudes[region], reviewLongitudes[region]);
                lighting.ReviewUtc = new DateTime(2026, 3, 20, reviewHours[region], 0, 0, DateTimeKind.Utc);
                yield return Shot("global-region-" + region);
            }
            // Exercise both actual pad surfaces, not only an upward sky view.
            foreach (int site in new[] { 0, 1 })
            {
                director.GoToLaunch(site);
                var padCamera = director.launchCamera;
                padCamera.GetComponent<FlightCameraControls>().enabled = false;
                padCamera.transform.position = new Vector3(90, 110, -180);
                padCamera.transform.LookAt(new Vector3(0, 20, 0));
                yield return new WaitForSecondsRealtime(8);
                foreach (int hour in new[] { 6, 18 })
                {
                    lighting.ReviewUtc = new DateTime(2026, 3, 20, hour, 0, 0, DateTimeKind.Utc);
                    yield return Shot("pad-" + site + "-hour-" + hour);
                }
                padCamera.GetComponent<FlightCameraControls>().enabled = true;
            }
            director.GoToLaunch(1);
            director.RefreshPointWeather(true);
            var camera = director.launchCamera;
            camera.GetComponent<FlightCameraControls>().enabled = false;
            camera.transform.position = new Vector3(75, 38, -90);
            camera.transform.rotation = Quaternion.Euler(-30, 0, 0);
            foreach (int hour in new[] { 6, 18 })
            {
                lighting.ReviewUtc = new DateTime(2026, 3, 20, hour, 0, 0, DateTimeKind.Utc);
                yield return Shot("below-cloud-hour-" + hour);
            }
            deadline = Time.realtimeSinceStartup + 20;
            while ((director.weather.Status.StartsWith("Loading") || director.nasaWeather.Status.StartsWith("Loading")) && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("ESP_CLOUD_NIGHT_WEATHER NASA=" + director.nasaWeather.Status + " | MODEL=" + director.weather.Status + " | POINT=" + director.pointWeather.Status + " " + director.pointWeather.validtime);
            lighting.ReviewUtc = null;
            director.Launch(); driver.SetTimeRate(20);
            Debug.Log("ESP_CLOUD_NIGHT_LAUNCH_SOURCE " + driver.WeatherSourceLabel);
            foreach (int altitude in new[] { 12000, 150000 })
            {
                deadline = Time.realtimeSinceStartup + 100;
                while (driver.AltitudeMeters < altitude && Time.realtimeSinceStartup < deadline) yield return null;
                if (driver.AltitudeMeters < altitude) { Fail("Real ascent timeout"); yield break; }
                driver.SetTimeRate(1);
                camera.transform.position = new Vector3(0, 80, -150);
                camera.transform.rotation = Quaternion.Euler(35, 0, 0);
                lighting.ReviewUtc = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc);
                yield return Shot("flight-night-" + altitude);
                lighting.ReviewUtc = lighting.ReviewUtc.Value.AddHours(12);
                yield return Shot("flight-day-" + altitude);
                lighting.ReviewUtc = null;
                driver.SetTimeRate(20);
            }
            driver.SetTimeRate(1);
            layers.Select(-1); yield return null;
            if (layers.CloudsVisible || Shader.GetGlobalFloat("_EspObservedCloudsEnabled") != 0) { Fail("Offline cloud gate"); yield break; }
            Debug.Log("ESP_CLOUD_NIGHT_OFFLINE " + layers.Status);
            lighting.ReviewUtc = null;
            camera.GetComponent<FlightCameraControls>().enabled = true;
            director.ResetMission();
            Debug.Log("ESP_CLOUD_NIGHT_OK observed-data day-night inner-outer-face real-ascent weather-source offline-reset");
            Application.Quit();
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "cloud-night-" + name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
            Debug.Log("ESP_CLOUD_NIGHT_SHOT " + name + " altitude=" + GetComponent<RocketFlightDriver>().AltitudeMeters);
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(20, 180, Screen.width - 40, 50), "DEVELOPMENT CLOUD REVIEW · ACTUAL DATED OBSERVATIONS / SOLAR TIME OVERRIDE", new GUIStyle(GUI.skin.label) { fontSize = 18 });
        }
        private static void Fail(string message) { Debug.LogError("ESP_CLOUD_NIGHT_FAIL " + message); Application.Quit(1); }
    }
}
