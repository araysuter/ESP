using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in bounded development review; never used by a normal player.</summary>
    public sealed class NightLightsSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espNightLightsSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director != null) director.gameObject.AddComponent<NightLightsSmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            var lighting = GetComponent<MissionSunlight>();
            var tiles = GetComponent<LocalSurfaceTiles>();
            if (lighting == null || lighting.nightLights == null || lighting.nightLights.width != 4096 ||
                lighting.nightLights.format != TextureFormat.BC4 || lighting.nightLights.isReadable)
            { Fail("offline texture import/budget"); yield break; }
            MissionSession.Instance.NewMission(); director.SelectVehicle(0); director.ShowMap();
            var controls = director.mapCamera.GetComponent<GlobeCameraControls>();
            var names = new[] { "americas", "europe", "asia", "australia" };
            var latitude = new[] { 30f, 35f, 30f, -30f };
            var longitude = new[] { -90f, 20f, 115f, 135f };
            var nightHour = new[] { 6, 0, 12, 12 };
            for (int i = 0; i < names.Length; i++)
            {
                controls.Focus(latitude[i], longitude[i]);
                lighting.ReviewUtc = new DateTime(2026, 3, 20, nightHour[i], 0, 0, DateTimeKind.Utc);
                yield return Shot(names[i] + "-night");
                if (i == 0)
                {
                    float strength = lighting.nightLightsStrength;
                    lighting.nightLightsStrength = 0;
                    yield return Shot("americas-night-no-emission");
                    lighting.nightLightsStrength = strength;
                }
                lighting.ReviewUtc = lighting.ReviewUtc.Value.AddHours(12);
                yield return Shot(names[i] + "-day");
            }
            // Offline fallback and surface tiles share exactly the geographic emission.
            tiles.networkEnabled = false; tiles.ClearTiles();
            director.GoToLaunch(1);
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc);
            yield return Shot("offline-pad-night");
            tiles.networkEnabled = true;
            yield return new WaitForSecondsRealtime(8);
            yield return Shot("streamed-pad-night");
            lighting.ReviewUtc = null;
            director.Launch(); driver.SetTimeRate(20);
            float deadline = Time.realtimeSinceStartup + 120;
            while (driver.AltitudeMeters < 150000 && Time.realtimeSinceStartup < deadline) yield return null;
            if (driver.AltitudeMeters < 150000) { Fail("verified ascent timeout"); yield break; }
            driver.SetTimeRate(1);
            MissionSession.Instance.OpenPause();
            var camera = director.launchCamera;
            camera.GetComponent<FlightCameraControls>().enabled = false;
            camera.transform.position = new Vector3(0, 80, -150);
            camera.transform.rotation = Quaternion.Euler(35, 0, 0);
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc);
            yield return Shot("flight-night");
            lighting.ReviewUtc = lighting.ReviewUtc.Value.AddHours(12);
            yield return Shot("flight-day");
            MissionSession.Instance.ResumeMission();
            lighting.ReviewUtc = null;
            camera.GetComponent<FlightCameraControls>().enabled = true;
            director.ResetMission();
            yield return null;
            if (Math.Abs((lighting.CurrentUtc - DateTime.UtcNow).TotalSeconds) > 1) { Fail("reset clock"); yield break; }
            Debug.Log("ESP_NIGHT_LIGHTS_OK BC4 offline map-regions day-night real-ascent flight reset");
            Application.Quit();
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "night-lights-" + name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
            Debug.Log("ESP_NIGHT_LIGHTS_SHOT " + name + " " + GetComponent<MissionSunlight>().Status);
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(20, 180, Screen.width - 40, 40), "DEVELOPMENT NIGHT LIGHT REVIEW · HISTORICAL 2016 COMPOSITE", new GUIStyle(GUI.skin.label) { fontSize = 20 });
        }
        private static void Fail(string message) { Debug.LogError("ESP_NIGHT_LIGHTS_FAIL " + message); Application.Quit(1); }
    }
}
