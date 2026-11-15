using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in real-flight launch-cloud rehearsal; never fabricates flight state.</summary>
    public sealed class LaunchGroundEffectsSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espLaunchCloudSmoke") < 0) return;
            new GameObject("Launch cloud rehearsal").AddComponent<LaunchGroundEffectsSmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;
            var director = FindFirstObjectByType<MissionDirector>();
            var driver = FindFirstObjectByType<RocketFlightDriver>();
            var cloud = FindFirstObjectByType<LaunchGroundEffects>();
            var session = MissionSession.Instance;
            if (!Check(director != null && driver != null && cloud != null && cloud.smokeShader != null && session != null, "wiring")) yield break;
            session.NewMission(false); director.SelectVehicle(1); director.GoToLaunch(0);
            var sunlight = director.GetComponent<MissionSunlight>();
            DateTime? oldUtc = sunlight != null ? sunlight.ReviewUtc : null;
            if (sunlight != null) sunlight.ReviewUtc = DateTime.UtcNow.Date.AddHours(18);
            Debug.Log("ESP_LAUNCH_CLOUD_REVIEW daylight UTC override; actual simulated Starship flight");
            yield return new WaitForEndOfFrame();
            if (!Check(cloud.ActiveParticleCount == 0, "clear before launch")) yield break;
            driver.SetThrottle(1);
            driver.Launch(LaunchSites.All[0], null, true);
            // Review framing only: keep the pad cloud and full exhaust in shot.
            var controls = director.launchCamera.GetComponent<FlightCameraControls>();
            if (controls != null) controls.enabled = false;
            director.launchCamera.transform.position = new Vector3(190, 95, -230);
            director.launchCamera.transform.LookAt(new Vector3(0, 35, 0));
            director.launchCamera.fieldOfView = 50;
            Debug.Log("ESP_LAUNCH_CLOUD_REVIEW wide camera override for plume/pad visibility");
            float deadline = Time.realtimeSinceStartup + 60;
            foreach (double captureTime in new[] { 2d, 5, 10 })
            {
                while (driver.MissionTimeSeconds < captureTime && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForEndOfFrame();
                if (!Check(driver.MissionTimeSeconds >= captureTime && cloud.ActiveParticleCount > 0 &&
                    cloud.ActiveParticleCount <= LaunchGroundEffects.Capacity, "live cloud at " + captureTime)) yield break;
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "launch-cloud-" + captureTime + "s.png"));
                Debug.Log("ESP_LAUNCH_CLOUD_CAPTURE time=" + driver.MissionTimeSeconds + " puffs=" + cloud.ActiveParticleCount);
                yield return null;
            }
            session.OpenPause(); yield return new WaitForEndOfFrame();
            int count = cloud.ActiveParticleCount; double time = driver.MissionTimeSeconds;
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(cloud.ActiveParticleCount == count && driver.MissionTimeSeconds == time, "pause")) yield break;
            session.ResumeMission(); session.RestartMission();
            yield return new WaitForEndOfFrame();
            if (!Check(cloud.ActiveParticleCount == 0, "reset")) yield break;
            if (controls != null) controls.enabled = true;
            director.ResetLaunchFraming();
            if (sunlight != null) sunlight.ReviewUtc = oldUtc;
            Debug.Log("ESP_LAUNCH_CLOUD_SMOKE_OK Starship emission, capacity, pause, reset");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") < 0) Application.Quit();
        }
        private static bool Check(bool condition, string reason)
        {
            if (condition) return true;
            Debug.LogError("ESP_LAUNCH_CLOUD_SMOKE_FAIL " + reason); Application.Quit(1); return false;
        }
    }
}
