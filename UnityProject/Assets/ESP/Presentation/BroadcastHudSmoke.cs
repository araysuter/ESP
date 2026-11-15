using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Opt-in packaged HUD/inset regression; advances only the ordinary flight driver.</summary>
    public sealed class BroadcastHudSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver driver;
        private MissionSession session;
        private MissionInsetView inset;
        private VisualElement ui;
        private RenderTexture initialTexture;
        private int mapFrames, flightFrames, cameraBudget;
        private float deadline;
        private bool failed, finished, keepOpen;
        private MissionSunlight lighting;
        private DateTime? previousReviewUtc;
        private bool daylightReview, lightingRestored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espBroadcastSmoke") >= 0)
                new GameObject("Broadcast HUD packaged regression").AddComponent<BroadcastHudSmoke>();
        }

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 120;
            keepOpen = Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") >= 0;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            // Session UI appends to the shared document one frame after MissionUI.Start.
            yield return null;
            yield return null;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            session = MissionSession.Instance;
            var hud = FindFirstObjectByType<MissionUI>();
            if (!Check(director != null && driver != null && session != null && hud != null, "mission wiring")) yield break;
            inset = director.GetComponent<MissionInsetView>();
            ui = hud.GetComponent<UIDocument>().rootVisualElement;
            if (!Check(inset != null && ui != null && session.Page == MissionMenuPage.Start &&
                MissionSession.BlocksInput && driver.IsPaused && driver.Simulation == null, "initial start menu")) yield break;
            daylightReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-espDaylightReview") >= 0;
            if (daylightReview)
            {
                lighting = director.GetComponent<MissionSunlight>();
                if (!Check(lighting != null, "daylight review lighting")) yield break;
                previousReviewUtc = lighting.ReviewUtc;
                lighting.ReviewUtc = DateTime.UtcNow.Date.AddHours(18);
                Debug.Log("ESP_BROADCAST_DAYLIGHT_REVIEW_OVERRIDE " + lighting.ReviewUtc.Value.ToString("O") +
                    " lighting only; actual ordinary flight state");
            }

            yield return Resolution(1440, 900);
            if (failed) yield break;
            Capture("broadcast-start-1440x900.png");
            session.NewMission();
            yield return Preview(false, "new mission / flight inset");
            if (failed) yield break;
            if (!Check(!driver.IsPaused && !MissionSession.BlocksInput && director.IsMapView, "new mission state") || !HudLayout("new mission")) yield break;
            director.SelectVehicle(1);
            director.GoToLaunch(0);
            yield return Preview(true, "Starship pad / Earth inset");
            if (failed) yield break;
            initialTexture = inset.PreviewTexture;
            cameraBudget = Cameras();
            if (!Check(driver.CanLaunch && driver.Simulation == null && director.VehicleIndex == 1, "Starship pad state") ||
                !HudLayout("pad") || !PopulatedTexture("broadcast-pad-inset.png")) yield break;
            Capture("broadcast-pad-1440x900.png");

            director.Launch();
            driver.SetTimeRate(20);
            while (driver.MissionTimeSeconds < 30 && !failed && Time.realtimeSinceStartup < deadline)
            {
                if (!Check(driver.Simulation != null && !driver.Simulation.State.IsLanded, "actual Starship ascent")) yield break;
                yield return null;
            }
            if (!Check(driver.Simulation != null && driver.Simulation.State.IsLaunched && driver.MissionTimeSeconds >= 30 &&
                driver.AltitudeMeters > 10 && !driver.Simulation.HasUsedCheats && !session.ModifiedFlight, "30 s unmodified flight")) yield break;
            driver.SetTimeRate(1);
            // Pause the authoritative state before comparing the 5 Hz HUD's text exactly.
            session.OpenPause();
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(MissionSession.BlocksInput && driver.IsPaused, "pause retained by HUD") || !TelemetryUnits()) yield break;
            double pausedTime = driver.MissionTimeSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(driver.MissionTimeSeconds == pausedTime, "pause advanced flight")) yield break;
            session.ResumeMission();
            yield return Preview(true, "powered flight / Earth inset");
            if (failed || !HudLayout("powered flight") || !Lifecycle("powered flight")) yield break;
            Capture("broadcast-ascent-1440x900.png");

            director.ShowMap();
            yield return Preview(false, "map / live flight inset");
            if (failed || !HudLayout("map during flight") || !Lifecycle("map during flight") || !PopulatedTexture("broadcast-flight-inset.png")) yield break;
            Capture("broadcast-map-1440x900.png");
            for (int i = 0; i < 3; ++i)
            {
                director.GoToLaunch(director.SelectedSiteIndex);
                yield return Preview(true, "flight swap " + i);
                if (failed || !Lifecycle("flight swap " + i)) yield break;
                director.ShowMap();
                yield return Preview(false, "map swap " + i);
                if (failed || !Lifecycle("map swap " + i)) yield break;
            }

            session.RestartMission();
            yield return Preview(true, "restart / Earth inset");
            if (failed || !Check(driver.Simulation == null && driver.CanLaunch && driver.MissionTimeSeconds == 0 &&
                driver.Throttle == 0 && driver.TimeRate == 1 && !session.ModifiedFlight, "restart resets flight") || !Lifecycle("restart")) yield break;
            for (int i = 0; i < 4; ++i)
            {
                director.GoToLaunch(i % LaunchSites.All.Length);
                director.SelectVehicle(i % 3);
                yield return Preview(true, "site / vehicle " + i);
                if (failed || !Check(director.SelectedSiteIndex == i % LaunchSites.All.Length && director.VehicleIndex == i % 3 &&
                    director.EnvironmentRoot != null && director.RocketVisual != null && driver.Simulation == null,
                    "site / vehicle selection " + i) || !Lifecycle("site / vehicle " + i)) yield break;
                director.ShowMap();
                yield return Preview(false, "site map " + i);
                if (failed || !Lifecycle("site map " + i)) yield break;
            }

            yield return Resolution(1280, 720);
            if (failed) yield break;
            yield return Preview(false, "720p map");
            if (failed || !HudLayout("720p") || !Lifecycle("720p")) yield break;
            var infoButton = ui.Q<Button>("flight-information-toggle");
            if (!Check(infoButton != null && infoButton.enabledInHierarchy, "information control retained")) yield break;
            Debug.Log("ESP_BROADCAST_INFO_BEFORE button=" + Describe(infoButton) +
                " panel=" + Describe(ui.Q<VisualElement>("flight-information-panel")));
            infoButton.Focus();
            yield return null;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = infoButton;
                infoButton.SendEvent(submit);
                Debug.Log("ESP_BROADCAST_INFO_SUBMIT stopped=" + submit.isPropagationStopped + " target=" +
                    (submit.target as VisualElement)?.name);
            }
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            var info = ui.Q<VisualElement>("flight-information-panel");
            var close = ui.Q<Button>("close-information");
            var scroll = ui.Q<ScrollView>("flight-information-scroll");
            bool informationValid = info != null && close != null && info.resolvedStyle.display != DisplayStyle.None &&
                scroll != null && Inside(info.worldBound) && Inside(close.worldBound);
            Debug.Log("ESP_BROADCAST_INFO_AFTER screen=" + Screen.width + "x" + Screen.height +
                " ui=" + Describe(ui) + " panel=" + Describe(info) + " close=" + Describe(close) +
                " scrollExists=" + (scroll != null) + " scroll=" + Describe(scroll) +
                " panelInside=" + (info != null && Inside(info.worldBound)) +
                " closeInside=" + (close != null && Inside(close.worldBound)));
            if (!informationValid)
            {
                string capture = Path.Combine(Application.persistentDataPath, "broadcast-info-failure-1280x720.png");
                Debug.Log("ESP_BROADCAST_INFO_FAILURE_CAPTURE " + capture);
                ScreenCapture.CaptureScreenshot(capture);
                // CaptureScreenshot completes after rendering; preserve this frame before quitting.
                yield return new WaitForEndOfFrame();
                yield return new WaitForSecondsRealtime(.5f);
                Check(false, "720p information panel / close button bounded and scrollable");
                yield break;
            }
            Capture("broadcast-info-1280x720.png");
            close.Focus();
            yield return null;
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = close; close.SendEvent(submit); }
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            if (!Check(info.resolvedStyle.display == DisplayStyle.None, "information close control")) yield break;
            Capture("broadcast-map-1280x720.png");
            Debug.Log("ESP_BROADCAST_SMOKE_OK start/new mission, km/h and km, navball, rendered 384x216 map/flight insets, actual Starship ascent, pause, view cycles, reset/site/vehicle changes, bounded cameras/RT and 720p information panel; captures require visual review");
            finished = true;
            yield return new WaitForSecondsRealtime(.4f);
            RestoreLighting();
            if (!keepOpen) Application.Quit();
        }

        private IEnumerator Preview(bool map, string phase)
        {
            // MissionUI binds its texture on a 5 Hz refresh, independently of the
            // 10 Hz renderer. Settle that binding before requiring a fresh frame.
            yield return new WaitForSecondsRealtime(.3f);
            if (failed) yield break;
            int before = map ? mapFrames : flightFrames;
            float phaseDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 12);
            do
            {
                yield return new WaitForEndOfFrame();
                if (failed) yield break;
                if (inset.HasPreview && inset.PreviewIsMap == map && (map ? mapFrames : flightFrames) > before) break;
                if (!Check(Time.realtimeSinceStartup < phaseDeadline, phase + ": no fresh rendered preview")) yield break;
            } while (true);
            Camera alternate = map ? inset.MapPreviewCamera : inset.FlightPreviewCamera;
            var backdrop = director.GetComponent<FlightEarthBackdrop>();
            Check(inset.PreviewTexture != null && inset.PreviewTexture.IsCreated() && inset.PreviewTexture.width == 384 &&
                inset.PreviewTexture.height == 216 && alternate != null && alternate.targetTexture == inset.PreviewTexture &&
                director.mapCamera.targetTexture == null && director.launchCamera.targetTexture == null &&
                director.IsMapView != map && backdrop != null && backdrop.IsRendering &&
                backdrop.BackgroundCamera.targetTexture == (map ? null : inset.PreviewTexture), phase + ": camera targets / backdrop");
            if (map) Check(inset.HasMarker, phase + ": Earth inset vehicle marker");
            Debug.Log("ESP_BROADCAST_PREVIEW " + phase + " mapFrames=" + mapFrames + " flightFrames=" + flightFrames);
        }

        private bool TelemetryUnits()
        {
            var speed = ui.Q<Label>("speed-readout"); var altitude = ui.Q<Label>("altitude-readout");
            string expectedSpeed = (driver.SpeedMetersPerSecond * 3.6).ToString("N0", CultureInfo.CurrentCulture);
            string expectedAltitude = (driver.AltitudeMeters / 1000).ToString(Math.Abs(driver.AltitudeMeters) < 10000 ? "N2" : "N1", CultureInfo.CurrentCulture);
            return Check(speed != null && altitude != null && speed.text == expectedSpeed && altitude.text == expectedAltitude &&
                HasText(ui.Q<BroadcastGauge>("speed-readout-gauge"), "KM/H") &&
                HasText(ui.Q<BroadcastGauge>("altitude-readout-gauge"), "KM"), "authoritative km/h / km readouts");
        }

        private bool HudLayout(string phase)
        {
            var speed = ui.Q<BroadcastGauge>("speed-readout-gauge");
            var altitude = ui.Q<BroadcastGauge>("altitude-readout-gauge");
            var navball = ui.Q<NavballElement>("flight-navball");
            var preview = ui.Q<VisualElement>("live-preview");
            var image = ui.Q<Image>("live-preview-image");
            if (!Check(speed != null && altitude != null && navball != null && preview != null && image != null &&
                image.image == inset.PreviewTexture, phase + ": HUD / preview binding")) return false;
            return Check(Inside(speed.worldBound) && Inside(altitude.worldBound) && Inside(navball.worldBound) && Inside(preview.worldBound) &&
                Mathf.Abs(navball.worldBound.center.x - ui.worldBound.center.x) < 5 &&
                altitude.worldBound.xMax < navball.worldBound.xMin && preview.worldBound.xMin > navball.worldBound.xMax &&
                speed.worldBound.center.y > ui.worldBound.center.y && preview.worldBound.center.y > ui.worldBound.center.y,
                phase + ": lower-left gauges / center navball / lower-right inset bounds");
        }

        private bool Lifecycle(string phase)
        {
            int textures = 0;
            foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (texture != null && texture.name == "Live mission inset 384x216") textures++;
            return Check(inset.PreviewTexture == initialTexture && textures == 1 && Cameras() <= cameraBudget,
                phase + ": camera/RT growth; cameras=" + Cameras() + "/" + cameraBudget + " insetRTs=" + textures);
        }

        private bool PopulatedTexture(string filename)
        {
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var target = inset.PreviewTexture;
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32();
                int low = 765, high = 0;
                for (int i = 0; i < colors.Length; i += 17)
                {
                    int sum = colors[i].r + colors[i].g + colors[i].b;
                    low = Math.Min(low, sum); high = Math.Max(high, sum);
                }
                if (!Check(high - low >= 5, "rendered inset is uniform/empty: " + filename)) return false;
                File.WriteAllBytes(Path.Combine(Application.persistentDataPath, filename), pixels.EncodeToPNG());
                return true;
            }
            catch (Exception exception) { return Check(false, "inset readback: " + exception.Message); }
            finally { RenderTexture.active = previous; if (pixels != null) Destroy(pixels); }
        }

        private IEnumerator Resolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float until = Mathf.Min(deadline, Time.realtimeSinceStartup + 4);
            do { yield return null; } while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until);
            yield return new WaitForSecondsRealtime(.35f);
            yield return new WaitForEndOfFrame();
            Check(Screen.width == width && Screen.height == height, "requested resolution " + width + "x" + height + "; actual " + Screen.width + "x" + Screen.height);
        }
        private bool Inside(Rect rect)
        {
            Rect bounds = ui.worldBound;
            return !float.IsNaN(rect.width) && rect.width > 0 && rect.height > 0 && rect.xMin >= bounds.xMin - 2 &&
                rect.yMin >= bounds.yMin - 2 && rect.xMax <= bounds.xMax + 2 && rect.yMax <= bounds.yMax + 2;
        }
        private static bool HasText(VisualElement element, string text)
        {
            if (element == null) return false;
            if (element is Label label && label.text == text) return true;
            foreach (var child in element.Children()) if (HasText(child, text)) return true;
            return false;
        }
        private static string Describe(VisualElement element) => element == null ? "null" :
            element.name + "{display=" + element.resolvedStyle.display + ", visibility=" + element.resolvedStyle.visibility +
            ", world=" + element.worldBound + ", layout=" + element.layout + ", enabled=" + element.enabledInHierarchy + "}";
        private static int Cameras() => FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (inset == null) return;
            if (camera == inset.MapPreviewCamera) mapFrames++;
            if (camera == inset.FlightPreviewCamera) flightFrames++;
        }
        private void Update() { if (!failed && !finished && deadline > 0 && Time.realtimeSinceStartup >= deadline) Check(false, "120 s timeout"); }
        private bool Check(bool condition, string reason)
        {
            if (condition && !failed) return true;
            if (!failed) Debug.LogError("ESP_BROADCAST_SMOKE_FAIL " + reason);
            failed = true;
            RestoreLighting();
            if (!keepOpen) Application.Quit(1);
            return false;
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, name));
        private void RestoreLighting()
        {
            if (!daylightReview || lightingRestored) return;
            if (lighting != null) lighting.ReviewUtc = previousReviewUtc;
            lightingRestored = true;
        }
        private void OnGUI()
        {
            if (!daylightReview || lighting == null || lightingRestored) return;
            GUI.Box(new Rect(16, 92, 680, 46), GUIContent.none);
            GUI.Label(new Rect(24, 96, 664, 40),
                "DEVELOPMENT HUD REVIEW · DAYLIGHT UTC OVERRIDE\n" +
                lighting.ReviewUtc?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") + " · Real simulated flight; lighting review only");
        }
        private void OnDestroy() { RenderPipelineManager.endCameraRendering -= OnCameraRendered; RestoreLighting(); }
    }
}
