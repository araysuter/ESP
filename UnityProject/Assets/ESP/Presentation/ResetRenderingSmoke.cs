using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ESP.Presentation
{
    // Opt-in player regression. Every flight uses the shipping fixed-step driver;
    // no fabricated position, checkpoint, gravity override or teleported trajectory.
    public sealed class ResetRenderingSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver driver;
        private bool keepOpen, daylightReview;
        private CameraClearFlags initialClearFlags;
        private int initialCullingMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espResetSmoke") >= 0)
                new GameObject("Reset rendering regression").AddComponent<ResetRenderingSmoke>();
        }

        private IEnumerator Start()
        {
            keepOpen = Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") >= 0;
            yield return null;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            var session = MissionSession.Instance;
            if (!Check(director != null && driver != null && session != null, "missing mission components")) yield break;
            daylightReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-espDaylightReview") >= 0;
            if (daylightReview) director.GetComponent<MissionSunlight>().ReviewUtc = DateTime.UtcNow.Date.AddHours(18);
            session.NewMission();
            initialClearFlags = director.launchCamera.clearFlags;
            initialCullingMask = director.launchCamera.cullingMask;
            director.GoToLaunch(0);
            director.SelectVehicle(0);
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            if (!CheckPad("initial pad")) yield break;
            Vector3 padCameraPosition = director.launchCamera.transform.position;
            Quaternion padCameraRotation = director.launchCamera.transform.rotation;
            Capture("reset-smoke-pad.png");
            yield return new WaitForSecondsRealtime(8f);
            var surface = director.GetComponent<LocalSurfaceTiles>();
            Debug.Log("ESP_SURFACE_REVIEW resident=" + surface.ResidentTileCount + " active=" + surface.ActiveRequestCount + " status=" + surface.Status);
            Capture("reset-smoke-pad-loaded.png");
            yield return new WaitForSecondsRealtime(.15f);

            // Reproduce the camera orbit state that previously allowed looking
            // through the underside of the pad/Earth. This moves only the camera.
            director.launchCamera.transform.position = new Vector3(80, -100, -90);
            director.launchCamera.GetComponent<FlightCameraControls>().ResetFraming();
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!Check(director.launchCamera.transform.position.y >= .9f, "orbit camera remained beneath the pad")) yield break;
            Capture("reset-smoke-camera-surface.png");
            yield return new WaitForSecondsRealtime(.15f);
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.3f);

            // Short vertical burn and engine cutoff produce a genuine failed hop.
            driver.SetThrottle(1);
            director.Launch();
            driver.SetTimeRate(20);
            float deadline = Time.realtimeSinceStartup + 60;
            while (driver.MissionTimeSeconds < 12 && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(driver.Simulation != null && driver.Simulation.State.IsLaunched && driver.AltitudeMeters > 5, "short hop did not lift off")) yield break;
            driver.SetThrottle(0);
            deadline = Time.realtimeSinceStartup + 60;
            while (!driver.Simulation.State.IsLanded && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(driver.Simulation.State.IsLanded && driver.Simulation.IsDestroyed, "failed hop did not reach destructive impact")) yield break;
            yield return new WaitForEndOfFrame();
            if (!CheckRendering("impact") || !Check(driver.TimeRate == 1 && driver.Throttle == 0, "impact retained throttle or accelerated time")) yield break;
            double impactTime = driver.MissionTimeSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(driver.MissionTimeSeconds == impactTime, "terminal impact time advanced")) yield break;
            Debug.Log("ESP_RESET_SMOKE_IMPACT t=" + impactTime + " speed=" + driver.Simulation.ImpactSpeedMetersPerSecond);
            Capture("reset-smoke-impact.png");
            yield return new WaitForSecondsRealtime(.15f);

            // Exercise the shipping pause/restart path after a crash.
            session.OpenPause();
            session.RestartMission();
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            if (!CheckPad("crash restart") || !Check(!driver.IsPaused && !MissionSession.BlocksInput, "restart remained paused")) yield break;
            if (!Check(Vector3.Distance(director.launchCamera.transform.position, padCameraPosition) < .1f &&
                Quaternion.Angle(director.launchCamera.transform.rotation, padCameraRotation) < .1f, "reset retained previous camera framing")) yield break;
            Capture("reset-smoke-after-impact.png");
            yield return new WaitForSecondsRealtime(.15f);

            // Cross the former 120 km backdrop threshold through a normal assisted flight.
            director.Launch();
            driver.SetTimeRate(20);
            deadline = Time.realtimeSinceStartup + 180;
            double[] heights = { 5000, 30000, 60000, 110000, 119000, 121000 };
            int nextHeight = 0;
            while (driver.AltitudeMeters < 130000 && !driver.Simulation.State.IsLanded && Time.realtimeSinceStartup < deadline)
            {
                if (nextHeight < heights.Length && driver.AltitudeMeters >= heights[nextHeight])
                {
                    if (!CheckRendering("ascent " + heights[nextHeight])) yield break;
                    Capture("reset-smoke-altitude-" + ((int)heights[nextHeight]) + ".png");
                    Debug.Log("ESP_RENDER_ALTITUDE " + driver.AltitudeMeters);
                    nextHeight++;
                }
                yield return null;
            }
            if (!Check(driver.AltitudeMeters >= 130000, "assisted flight failed to reach 130 km")) yield break;
            yield return new WaitForEndOfFrame();
            if (!CheckRendering("high altitude")) yield break;
            Debug.Log("ESP_RESET_SMOKE_HIGH_ALTITUDE t=" + driver.MissionTimeSeconds + " altitude=" + driver.AltitudeMeters);
            Capture("reset-smoke-high-altitude.png");
            yield return new WaitForSecondsRealtime(.15f);
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            if (!CheckPad("high altitude reset")) yield break;
            Capture("reset-smoke-after-high-altitude.png");
            yield return new WaitForSecondsRealtime(.15f);

            for (int i = 0; i < 4; i++)
            {
                director.ShowMap();
                director.ResetMission();
                yield return new WaitForEndOfFrame();
                if (!CheckRendering("map reset " + i)) yield break;
                var data = director.launchCamera.GetUniversalAdditionalCameraData();
                if (!Check(data.renderType == CameraRenderType.Base && director.launchCamera.clearFlags == initialClearFlags &&
                    director.launchCamera.cullingMask == initialCullingMask, "map did not restore launch camera state")) yield break;
                director.GoToLaunch((i + 1) % LaunchSites.All.Length);
                yield return new WaitForSecondsRealtime(.2f);
                yield return new WaitForEndOfFrame();
                if (!CheckPad("site cycle " + i)) yield break;
            }
            Capture("reset-smoke-site-cycles.png");
            yield return new WaitForSecondsRealtime(.15f);
            director.Launch();
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(driver.Simulation != null && driver.MissionTimeSeconds > 0, "launch after repeated resets did not advance")) yield break;
            director.ShowMap();
            director.GoToLaunch(director.SelectedSiteIndex);
            yield return new WaitForEndOfFrame();
            if (!CheckRendering("resumed flight view")) yield break;
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            if (!CheckPad("final pad")) yield break;
            Capture("reset-smoke-final.png");
            Debug.Log("ESP_RESET_SMOKE_OK actual destructive hop, 130 km assisted flight, restart, repeated map/site resets and relaunch; screenshots require visual review");
            yield return new WaitForSecondsRealtime(.3f);
            if (!keepOpen) Application.Quit();
        }

        private void OnGUI()
        {
            if (daylightReview) GUI.Label(new Rect(16, 52, 450, 28), "RENDERING QA · DAYLIGHT OVERRIDE · REAL SIMULATED FLIGHT");
        }

        private bool CheckPad(string label)
        {
            return Check(driver.CanLaunch && driver.Simulation == null && driver.MissionTimeSeconds == 0 &&
                driver.TimeRate == 1 && driver.Throttle == 0, label + ": stale flight state") &&
                Check(director.EnvironmentRoot != null && director.RocketVisual != null &&
                    director.EnvironmentRoot.position.sqrMagnitude < .001f &&
                    Vector3.Distance(director.RocketVisual.position, Vector3.up * LaunchEnvironment.RocketBaseHeightMeters) < .001f &&
                    Quaternion.Angle(director.RocketVisual.rotation, Quaternion.identity) < .01f, label + ": stale pad pose") && CheckRendering(label);
        }

        private bool CheckRendering(string label)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var membership = new Dictionary<Camera, int>();
            int activeBases = 0;
            foreach (Camera camera in cameras)
            {
                if (!camera.isActiveAndEnabled) continue;
                if (!Check(Finite(camera.transform.position) && Finite(camera.transform.rotation) &&
                    Finite(camera.backgroundColor) && Finite(camera.fieldOfView) && camera.fieldOfView > 0 &&
                    camera.nearClipPlane > 0 && camera.farClipPlane > camera.nearClipPlane, label + ": invalid camera " + camera.name)) return false;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data.renderType != CameraRenderType.Base) continue;
                activeBases++;
                if (!Check(camera.clearFlags == CameraClearFlags.SolidColor || camera.clearFlags == CameraClearFlags.Skybox,
                    label + ": base camera does not clear color " + camera.name)) return false;
                foreach (Camera overlay in data.cameraStack)
                {
                    if (!Check(overlay != null && overlay.isActiveAndEnabled &&
                        overlay.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay, label + ": stale camera stack entry")) return false;
                    membership.TryGetValue(overlay, out int count);
                    membership[overlay] = count + 1;
                }
            }
            if (!Check(activeBases == 1, label + ": expected one active base camera, found " + activeBases)) return false;
            foreach (Camera camera in cameras)
            {
                if (!camera.isActiveAndEnabled || camera.GetUniversalAdditionalCameraData().renderType != CameraRenderType.Overlay) continue;
                if (!Check(membership.TryGetValue(camera, out int count) && count == 1, label + ": orphan or multiply stacked overlay " + camera.name)) return false;
            }
            return Check(director.EnvironmentRoot == null || Finite(director.EnvironmentRoot.position), label + ": invalid environment transform") &&
                Check(director.RocketVisual == null || (Finite(director.RocketVisual.position) && Finite(director.RocketVisual.rotation)), label + ": invalid vehicle transform");
        }

        private bool Check(bool condition, string message)
        {
            if (condition) return true;
            Debug.LogError("ESP_RESET_SMOKE_FAIL " + message);
            if (!keepOpen) Application.Quit(1);
            return false;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool Finite(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, name));
    }
}
