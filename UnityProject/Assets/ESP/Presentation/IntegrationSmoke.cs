using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    // Development player only; opts in explicitly and uses the real UI and flight simulation.
    public sealed class IntegrationSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver flight;
        private Button launch;
        private string directory;
        
        private bool tutorialHadPreference;
        private int tutorialPreference;
        private bool tutorialPreferenceSaved;
        private bool QuitRequested => Array.IndexOf(Environment.GetCommandLineArgs(), "-espIntegrationQuit") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espIntegrationSmoke") >= 0)
                new GameObject("Integration review").AddComponent<IntegrationSmoke>();
        }

        private bool Check(bool value, string description)
        {
            if (value) { Debug.Log("ESP_INTEGRATION_CHECK " + description); return true; }
            Debug.LogError("ESP_INTEGRATION_FAIL " + description);
            if (director) director.ResetMission();
            if (QuitRequested) Application.Quit(1);
            return false;
        }

        private void PressLaunch() => Press(launch);
        private static void Press(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private bool Counting => launch != null && launch.text.StartsWith("T−", StringComparison.Ordinal) && !launch.enabledSelf;
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("ESP_INTEGRATION_CAPTURE " + path + " altitude=" + flight.AltitudeMeters + " missionTime=" + flight.MissionTimeSeconds);
            yield return null;
        }

        private bool CheckTileBounds(LocalSurfaceTiles tiles)
        {
            if (tiles.ResidentTileCount <= 48 && tiles.ActiveRequestCount <= 2) return true;
            return Check(false, "surface tile request/cache bounds exceeded");
        }

        private IEnumerator MeasureOrbitFrames(string label)
        {
            var samples = new List<float>();
            float until = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < until) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000); }
            samples.Sort();
            if (samples.Count > 0)
                Debug.Log("ESP_INTEGRATION_FRAME_TIMING " + label + " samples=" + samples.Count +
                    " p50_ms=" + samples[samples.Count / 2].ToString("F2") +
                    " p95_ms=" + samples[Mathf.Clamp(Mathf.CeilToInt(samples.Count * .95f)-1,0,samples.Count-1)].ToString("F2") +
                    " (1x, capped wall-frame intervals, not GPU timings)");
        }

        private IEnumerator ReviewOrbitalGround(LocalSurfaceTiles tiles)
        {
            var camera = director.launchCamera;
            var controls = camera.GetComponent<FlightCameraControls>();
            controls.enabled = false;
            var state = flight.Simulation.State;
            var ecef = FlightCore.EarthFrames.InertialToEcef(state.Position,
                FlightCore.Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            var site = LaunchSites.All[director.SelectedSiteIndex];
            var up = CameraRelativeCoordinates.FromEcef(ecef.X, ecef.Y, ecef.Z, 0, 0, 0,
                site.Latitude * Math.PI / 180, site.Longitude * Math.PI / 180).normalized;
            var tangent = Vector3.Cross(up, Vector3.right).normalized;
            if (tangent.sqrMagnitude < .1f) tangent = Vector3.forward;
            camera.transform.position = director.RocketVisual.position + up * 250 + tangent * 160;
            camera.transform.LookAt(director.RocketVisual.position - up * 100000, tangent);
            yield return new WaitForSecondsRealtime(1);
            int active = 0;
            foreach (Transform child in tiles.transform)
                if (child.gameObject.activeInHierarchy && (child.name.StartsWith("NASA Landsat ") || child.name.StartsWith("NASA Blue Marble ") || child.name.StartsWith("USGS ortho "))) active++;
            if (!Check(active > 0, "current orbital surface tiles active")) yield break;
            yield return Shot("orbit-ground-detail");
            yield return MeasureOrbitFrames("orbit-detail");
            // Oblique view checks the curved terrain-to-globe horizon transition.
            camera.transform.LookAt(director.RocketVisual.position + tangent * 600000 - up * 200000, up);
            yield return new WaitForSecondsRealtime(1);
            yield return Shot("orbit-oblique-detail");
            var lighting = director.GetComponent<MissionSunlight>();
            var lightingDriver = lighting.driver;
            var oldReview = lighting.ReviewUtc;
            var geo = FlightCore.EarthFrames.EcefToGeodetic(ecef);
            DateTime best = lighting.CurrentUtc;
            double bestElevation = -90;
            for (int hour = 0; hour < 24; hour++)
            {
                var utc = lighting.CurrentUtc.Date.AddHours(hour);
                double elevation = FlightCore.SolarLighting.ElevationDegrees(FlightCore.SolarLighting.DirectionEcef(utc), geo.LatitudeRadians, geo.LongitudeRadians);
                if (elevation > bestElevation) { bestElevation = elevation; best = utc; }
            }
            // Lighting-only diagnostic override, explicitly named in capture/log;
            // authoritative flight, atmosphere and orbital state remain unchanged.
            lighting.driver = null; lighting.ReviewUtc = best; lighting.Apply();
            yield return new WaitForSecondsRealtime(1);
            Debug.Log("ESP_INTEGRATION_LIGHTING_REVIEW daytime override " + best.ToString("O"));
            yield return Shot("orbit-oblique-daylight-review");
            lighting.driver = lightingDriver; lighting.ReviewUtc = oldReview; lighting.Apply();
            tiles.networkEnabled = false;
            tiles.ClearTiles();
            yield return new WaitForSecondsRealtime(1);
            yield return Shot("orbit-oblique-offline");
            yield return MeasureOrbitFrames("orbit-offline");
            controls.enabled = true;
            controls.ResetFraming();
        }

        private void OnDestroy()
        {
            if (!tutorialPreferenceSaved) return;
            if (tutorialHadPreference) PlayerPrefs.SetInt("ESP.Tutorial.Seen", tutorialPreference);
            else PlayerPrefs.DeleteKey("ESP.Tutorial.Seen");
            PlayerPrefs.Save();
        }

        private IEnumerator Start()
        {
            yield return null;
            directory = Path.Combine(Application.persistentDataPath, "IntegrationReview");
            Directory.CreateDirectory(directory);
            director = FindFirstObjectByType<MissionDirector>();
            flight = FindFirstObjectByType<RocketFlightDriver>();
            var ui = FindFirstObjectByType<MissionUI>();
            var session = MissionSession.Instance;
            if (!Check(director && flight && ui && session, "mission components exist")) yield break;
            tutorialHadPreference = PlayerPrefs.HasKey("ESP.Tutorial.Seen");
            tutorialPreference = PlayerPrefs.GetInt("ESP.Tutorial.Seen", 0);
            tutorialPreferenceSaved = true;
            PlayerPrefs.DeleteKey("ESP.Tutorial.Seen");
            session.NewMission();
            yield return null; yield return null;
            var tutorialRoot = ui.GetComponent<UIDocument>().rootVisualElement;
            if (!Check(session.Page == MissionMenuPage.Tutorial && MissionSession.BlocksInput &&
                tutorialRoot.Q<VisualElement>("tutorial-controls") != null, "first mission opens paused tutorial")) yield break;
            yield return Shot("first-mission-tutorial");
            Press(tutorialRoot.Q<Button>("tutorial-done"));
            yield return null;
            if (!Check(session.Page == MissionMenuPage.None && PlayerPrefs.GetInt("ESP.Tutorial.Seen") == 1,
                "tutorial dismissal resumes and persists")) yield break;
            session.NewMission();
            if (!Check(session.Page == MissionMenuPage.None, "subsequent mission skips tutorial")) yield break;
            session.OpenSettings(); yield return null; yield return null;
            Press(tutorialRoot.Q<Button>("open-tutorial")); yield return null;
            if (!Check(session.Page == MissionMenuPage.Tutorial, "tutorial reopens from settings")) yield break;
            session.Back();
            if (!Check(session.Page == MissionMenuPage.Settings, "tutorial back returns to settings")) yield break;
            session.ResumeMission();
            director.GoToLaunch(0);
            yield return null;
            launch = ui.GetComponent<UIDocument>().rootVisualElement.Q<Button>("launch-stage-action");
            if (!Check(launch != null && launch.enabledInHierarchy && director.CanLaunch, "launch button ready")) yield break;

            var document = ui.GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            var minimize = root.Q<Button>("live-preview-minimize");
            var caption = root.Q<Label>("live-preview-caption");
            if (!Check(minimize != null && caption != null, "preview controls exist")) yield break;
            Press(minimize);
            yield return new WaitForSecondsRealtime(.6f);
            if (!Check(minimize.text == "+" && caption.text == "LIVE PREVIEW" && !director.IsMapView,
                "preview keyboard activation minimizes without switching view and survives refresh")) yield break;
            yield return Shot("preview-minimized");
            Press(minimize);
            yield return null;
            if (!Check(minimize.text == "−" && caption.text.Contains("EARTH MAP"), "preview restores current caption")) yield break;
            float savedScale = document.panelSettings.scale;
            foreach (float scaleValue in new[] { .75f, 1.25f })
            {
                session.OpenSettings();
                yield return null; yield return null;
                var slider = root.Q<Slider>("gui-scale");
                if (!Check(slider != null, "GUI scale slider exists")) yield break;
                slider.value = scaleValue;
                yield return null; yield return null;
                if (!Check(Mathf.Abs(document.panelSettings.scale - scaleValue) < .001f, "GUI scale applied " + scaleValue)) yield break;
                yield return Shot("settings-scale-" + scaleValue.ToString("F2"));
                session.ResumeMission();
                yield return null; yield return null;
                if (!Check(root.worldBound.Contains(launch.worldBound.min) && root.worldBound.Contains(launch.worldBound.max), "launch control remains in viewport at scale " + scaleValue)) yield break;
                yield return Shot("flight-scale-" + scaleValue.ToString("F2"));
            }
            Screen.SetResolution(1280, 720, false);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("compact-flight-scale-1.25");
            if (!Check(root.worldBound.Contains(launch.worldBound.min) && root.worldBound.Contains(launch.worldBound.max),
                "compact window keeps launch control in viewport")) yield break;
            Screen.SetResolution(1440, 900, false);
            document.panelSettings.scale = savedScale;
            yield return null;
            session.OpenSettings();
            yield return null; yield return null;
            var statsToggle = root.Q<Toggle>("nerd-stats-toggle");
            if (!Check(statsToggle != null, "stats overlay toggle exists")) yield break;
            bool savedStats = statsToggle.value;
            statsToggle.value = true;
            session.ResumeMission();
            yield return new WaitForSecondsRealtime(.6f);
            var statsPanel = root.Q<VisualElement>("nerd-stats-panel");
            var statsValues = root.Q<Label>("nerd-stats-values");
            if (!Check(statsPanel.resolvedStyle.display == DisplayStyle.Flex && statsValues.text.Contains("Unity allocated"),
                "stats overlay displays accurately labeled diagnostics")) yield break;
            yield return Shot("stats-overlay");
            var header = statsPanel.Q<VisualElement>("nerd-stats-drag-handle");
            if (!Check(header != null && header.panel.Pick(header.worldBound.center) != null, "stats drag header participates in shared UI picking")) yield break;
            statsPanel.style.left = 100000; statsPanel.style.top = 100000;
            Screen.SetResolution(1280, 720, false);
            yield return new WaitForSecondsRealtime(.6f);
            if (!Check(root.worldBound.Contains(statsPanel.worldBound.min) && root.worldBound.Contains(statsPanel.worldBound.max), "stats panel reclamped after resize")) yield break;
            yield return Shot("stats-compact-clamped");
            Screen.SetResolution(1440, 900, false);
            yield return new WaitForSecondsRealtime(.5f);
            statsPanel.style.left = Mathf.Max(0, root.resolvedStyle.width - statsPanel.resolvedStyle.width - 20);
            yield return null; yield return null;
            Vector2 rememberedStatsPosition = new Vector2(statsPanel.resolvedStyle.left, statsPanel.resolvedStyle.top);
            session.OpenPause();
            yield return null; yield return null;
            Screen.SetResolution(960, 540, false);
            yield return new WaitForSecondsRealtime(.4f);
            Screen.SetResolution(1440, 900, false);
            yield return new WaitForSecondsRealtime(.4f);
            session.ResumeMission();
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(Mathf.Abs(statsPanel.resolvedStyle.left - rememberedStatsPosition.x) < 1 &&
                Mathf.Abs(statsPanel.resolvedStyle.top - rememberedStatsPosition.y) < 1,
                "hidden stats panel preserves position across temporary window resize")) yield break;
            var flightCamera = director.launchCamera.GetComponent<FlightCameraControls>();
            var cinematic = root.Q<Button>("cinematic-camera-toggle");
            if (!Check(cinematic != null && flightCamera != null, "cinematic controls exist")) yield break;
            float originalFov = director.launchCamera.fieldOfView;
            Press(cinematic);
            if (!Check(flightCamera.IsCinematicView && !flightCamera.IsOnboardView, "cinematic entered")) yield break;
            for (int shot = 0; shot < 4; shot++)
            {
                yield return new WaitForSecondsRealtime(shot == 0 ? 1f : 8.1f);
                var cameraPosition = director.launchCamera.transform.position;
                if (!Check(!float.IsNaN(cameraPosition.x) && cameraPosition.magnitude < 10000000, "cinematic shot finite " + shot)) yield break;
                yield return Shot("cinematic-" + shot);
            }
            Press(root.Q<Button>("flight-camera-toggle"));
            if (!Check(!flightCamera.IsCinematicView && flightCamera.IsOnboardView, "onboard exits cinematic")) yield break;
            Press(cinematic);
            if (!Check(flightCamera.IsCinematicView && !flightCamera.IsOnboardView &&
                Mathf.Abs(director.launchCamera.fieldOfView - originalFov) < .01f, "cinematic exits onboard and restores FOV")) yield break;
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(!flightCamera.IsCinematicView && !flightCamera.IsOnboardView, "reset restores chase mode")) yield break;

            session.OpenSettings();
            yield return null; yield return null;
            root.Q<Toggle>("nerd-stats-toggle").value = savedStats;
            session.ResumeMission();
            yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espUiOnly") >= 0) { Debug.Log("ESP_UI_REVIEW_OK"); Application.Quit(0); yield break; }
            var tiles = FindFirstObjectByType<LocalSurfaceTiles>();
            if (!Check(tiles != null, "surface tile controller exists")) yield break;
            tiles.networkEnabled = false;
            tiles.ClearTiles();
            director.ResetMission();
            yield return new WaitForSecondsRealtime(1);
            if (!Check(tiles.ActiveRequestCount == 0 && tiles.ResidentTileCount == 0,
                "offline pad uses fallback without tile requests")) yield break;
            yield return Shot("offline-pad");
            tiles.networkEnabled = true;

            PressLaunch();
            yield return null;
            if (!Check(Counting && flight.Simulation == null, "button starts countdown without launching")) yield break;
            yield return Shot("countdown");
            session.OpenPause();
            yield return null;
            string pausedText = launch.text;
            yield return new WaitForSecondsRealtime(3.2f);
            if (!Check(MissionSession.BlocksInput && Counting && launch.text == pausedText && flight.Simulation == null, "pause holds countdown past launch deadline")) yield break;
            session.ResumeMission();
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(!Counting && launch.enabledSelf && flight.Simulation == null, "reset cancels countdown")) yield break;

            PressLaunch(); yield return null;
            if (!Check(Counting, "countdown restarted for site change")) yield break;
            director.GoToLaunch(1); yield return new WaitForSecondsRealtime(.3f);
            if (!Check(!Counting && director.SelectedSiteIndex == 1 && flight.Simulation == null, "site change cancels countdown")) yield break;
            director.GoToLaunch(0); yield return new WaitForSecondsRealtime(.3f);
            int alternate = -1;
            for (int i = 0; i < director.vehiclePrefabs.Length; i++)
                if (i != director.VehicleIndex && director.vehiclePrefabs[i] != null) { alternate = i; break; }
            if (!Check(alternate >= 0, "alternate vehicle available")) yield break;
            PressLaunch(); yield return null;
            if (!Check(Counting, "countdown restarted for vehicle change")) yield break;
            director.SelectVehicle(alternate); yield return new WaitForSecondsRealtime(.3f);
            if (!Check(!Counting && director.VehicleIndex == alternate && flight.Simulation == null, "vehicle change cancels countdown")) yield break;
            // Starship is the established nominal flight used by the existing return smoke.
            director.SelectVehicle(1); director.GoToLaunch(0); yield return new WaitForSecondsRealtime(.3f);
            if (!Check(director.VehicleIndex == 1 && director.CanLaunch, "nominal Starship ready")) yield break;
            PressLaunch(); yield return null;
            if (!Check(Counting && flight.Simulation == null, "final countdown started")) yield break;
            float deadline = Time.realtimeSinceStartup + 8;
            while (flight.Simulation == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(flight.Simulation != null && !director.CanLaunch, "countdown launched actual simulation")) yield break;
            flight.SetTimeRate(20);
            double[] thresholds = { 40000, 80000, 150000, 200000 };
            int next = 0;
            var frameMs = new List<float>();
            deadline = Time.realtimeSinceStartup + 240;
            while (!flight.OrbitVerified && !flight.Simulation.State.IsComplete && Time.realtimeSinceStartup < deadline)
            {
                frameMs.Add(Time.unscaledDeltaTime * 1000f);
                if (!CheckTileBounds(tiles)) yield break;
                if (next < thresholds.Length && flight.AltitudeMeters >= thresholds[next])
                {
                    yield return Shot("ascent-" + (thresholds[next] / 1000).ToString("F0") + "km");
                    next++;
                }
                yield return null;
            }
            if (!Check(flight.OrbitVerified, "real assisted ascent verified orbit")) yield break;
            if (!Check(next == thresholds.Length, "all ascent altitude captures reached")) yield break;
            flight.SetTimeRate(1);
            yield return new WaitForSecondsRealtime(5);
            yield return Shot("orbit-verified");
            Debug.Log("ESP_INTEGRATION_TILES orbit resident=" + tiles.ResidentTileCount + " requests=" + tiles.ActiveRequestCount + " status=" + tiles.Status);
            if (!Check(tiles.ResidentTileCount > 0, "surface tile cache populated at orbit")) yield break;
            yield return ReviewOrbitalGround(tiles);
            // Reset only imagery ownership to exercise an empty offline cache at the real orbit.
            tiles.networkEnabled = false;
            tiles.ClearTiles();
            yield return new WaitForSecondsRealtime(1);
            if (!Check(tiles.ResidentTileCount == 0 && tiles.ActiveRequestCount == 0, "offline orbit fallback has no tile requests")) yield break;
            yield return Shot("offline-orbit");
            tiles.networkEnabled = true;
            if (frameMs.Count > 0)
            {
                frameMs.Sort();
                double sum = 0; foreach (float ms in frameMs) sum += ms;
                Debug.Log("ESP_INTEGRATION_FRAME_TIMING ascent samples=" + frameMs.Count + " mean_ms=" + (sum / frameMs.Count).ToString("F2") + " p95_ms=" + frameMs[Mathf.Clamp(Mathf.CeilToInt(frameMs.Count * .95f) - 1, 0, frameMs.Count - 1)].ToString("F2") + " (whole ascent, unscaled frame intervals)");
            }
            director.ResetMission();
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(flight.Simulation == null && director.CanLaunch, "reset returns to pad")) yield break;
            Debug.Log("ESP_INTEGRATION_OK countdown cancellation and real ascent; controls released");
            if (QuitRequested) Application.Quit(0);
        }
    }
}
