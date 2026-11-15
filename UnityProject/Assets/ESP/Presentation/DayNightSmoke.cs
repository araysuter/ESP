using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Development-player lighting review; flight positions always come from simulation.</summary>
    public sealed class DayNightSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espDayNightSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director != null) director.gameObject.AddComponent<DayNightSmoke>();
        }

        private IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            var lighting = GetComponent<MissionSunlight>();
            MissionSession.Instance.NewMission();
            director.SelectVehicle(0);
            // Equinox afternoon/midnight over the US sites, clearly labeled in telemetry.
            foreach (int hour in new[] { 18, 6 })
            {
                lighting.ReviewUtc = new DateTime(2026, 3, 20, hour, 0, 0, DateTimeKind.Utc);
                director.ShowMap();
                director.mapCamera.GetComponent<GlobeCameraControls>().Focus(25, -90);
                yield return Shot("map-" + hour);
                for (int site = 0; site < LaunchSites.All.Length; site++)
                {
                    director.GoToLaunch(site);
                    var padCamera = director.launchCamera;
                    padCamera.GetComponent<FlightCameraControls>().enabled = false;
                    padCamera.transform.position = new Vector3(75, 38, -90);
                    padCamera.transform.LookAt(new Vector3(0, 18, 0));
                    yield return Shot("pad-" + site + "-" + hour);
                    if (lighting.InEarthShadow != (hour == 6)) { Fail("pad shadow"); yield break; }
                }
            }
            lighting.ReviewUtc = null;
            director.launchCamera.GetComponent<FlightCameraControls>().enabled = true;
            director.SelectVehicle(0);
            director.Launch();
            driver.SetTimeRate(20);
            float end = Time.realtimeSinceStartup + 180;
            while (driver.AltitudeMeters < 150000 && Time.realtimeSinceStartup < end) yield return null;
            if (driver.AltitudeMeters < 150000) { Fail("ascent timeout"); yield break; }
            driver.SetTimeRate(1);
            yield return null;
            if (Math.Abs((lighting.CurrentUtc - driver.LaunchUtc).TotalSeconds - driver.MissionTimeSeconds) > .1)
            { Fail("simulation clock"); yield break; }
            MissionSession.Instance.OpenPause();
            yield return null;
            DateTime pausedUtc = lighting.CurrentUtc;
            yield return new WaitForSecondsRealtime(.5f);
            if (lighting.CurrentUtc != pausedUtc) { Fail("paused clock"); yield break; }
            MissionSession.Instance.ResumeMission();
            director.launchCamera.GetComponent<FlightCameraControls>().enabled = false;
            director.launchCamera.transform.position = new Vector3(0, 80, -150);
            director.launchCamera.transform.rotation = Quaternion.Euler(25, 0, 0);
            foreach (int hour in new[] { 18, 6 })
            {
                lighting.ReviewUtc = new DateTime(2026, 3, 20, hour, 0, 0, DateTimeKind.Utc);
                yield return Shot("orbit-" + hour);
                director.ShowMap();
                yield return Shot("flight-map-" + hour);
                director.GoToLaunch(director.SelectedSiteIndex);
            }
            lighting.ReviewUtc = null;
            director.ResetMission();
            yield return null;
            if (Math.Abs((lighting.CurrentUtc - DateTime.UtcNow).TotalSeconds) > 1) { Fail("reset clock"); yield break; }
            Debug.Log("ESP_DAY_NIGHT_OK map, both pads, simulated high-altitude view, time warp, pause, reset");
            Application.Quit();
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "day-night-" + name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
            var lighting = GetComponent<MissionSunlight>();
            Debug.Log("ESP_DAY_NIGHT_SHOT " + name + " " + lighting.Status + " elevation=" + lighting.SolarElevationDegrees);
        }
        private void OnGUI()
        {
            var lighting = GetComponent<MissionSunlight>();
            GUI.Label(new Rect(20, 180, Screen.width - 40, 50), "DEVELOPMENT LIGHTING REVIEW · " + lighting.Status, new GUIStyle(GUI.skin.label) { fontSize = 22 });
        }
        private static void Fail(string reason) { Debug.LogError("ESP_DAY_NIGHT_FAIL " + reason); Application.Quit(1); }
    }
}
