using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ESP.Presentation
{
    /// <summary>Opt-in packaged texture/flight rehearsal; uses only shipping flight controls and camera framing.</summary>
    public sealed class WaterfallPlumeSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver driver;
        private FlightCameraControls cameraControls;
        private MissionSunlight sunlight;
        private DateTime? previousReviewUtc;
        private bool daylight, keepOpen, finished, failed, cameraWasEnabled;
        private float deadline;
        private string phase = "initializing";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espWaterfallSmoke") >= 0)
                new GameObject("Waterfall plume rehearsal").AddComponent<WaterfallPlumeSmoke>();
        }

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 120;
            keepOpen = Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") >= 0;
            daylight = Array.IndexOf(Environment.GetCommandLineArgs(), "-espDaylightReview") >= 0;
            yield return null;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            if (!Check(director != null && driver != null && MissionSession.Instance != null, "missing mission components")) yield break;
            MissionSession.Instance.NewMission(false);
            director.GoToLaunch(0);
            director.SelectVehicle(1);
            sunlight = director.GetComponent<MissionSunlight>();
            if (sunlight != null) { previousReviewUtc = sunlight.ReviewUtc; if (daylight) sunlight.ReviewUtc = DateTime.UtcNow.Date.AddHours(18); }
            cameraControls = director.launchCamera.GetComponent<FlightCameraControls>();
            cameraWasEnabled = cameraControls != null && cameraControls.enabled;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!CheckBindings("pad", false, true)) yield break;
            director.Launch();
            driver.SetTimeRate(20);
            phase = "sea-level ascent";
            while (driver.MissionTimeSeconds < 4 && !failed) yield return null;
            if (!Check(driver.Simulation != null && !driver.Simulation.HasUsedCheats &&
                driver.Simulation.State.StageIndex == 0 && driver.AltitudeMeters < 10000 && !driver.Simulation.State.IsLanded,
                "expected genuine low-altitude booster burn")) yield break;
            driver.SetTimeRate(1);
            if (!CheckBindings("sea-level burn", true, false) || !FrameEngines()) yield break;
            yield return new WaitForEndOfFrame();
            LogState("SEA_LEVEL"); Capture("waterfall-sea-level.png");
            yield return new WaitForSecondsRealtime(.3f);
            RestoreCamera();
            driver.SetTimeRate(20);
            phase = "assisted staging and ascent";
            bool staged = false;
            while (!failed)
            {
                var state = driver.Simulation.State;
                if (!Check(!state.IsLanded, "flight impacted before upper-stage vacuum burn")) yield break;
                if (!staged && state.StageIndex == 1)
                {
                    staged = true;
                    if (!CheckBindings("stage transition", false, false)) yield break;
                    LogState("STAGED");
                }
                if (state.StageIndex == 1 && driver.AltitudeMeters > 100000 && state.PropellantKg > 0 && driver.Throttle > .05f) break;
                yield return null;
            }
            if (failed) yield break;
            driver.SetTimeRate(1);
            phase = "actual upper-stage vacuum burn";
            if (!Check(staged && !driver.Simulation.HasUsedCheats, "staging or normal-flight provenance missing") ||
                !CheckBindings("vacuum upper-stage burn", true, false) || !FrameEngines()) yield break;
            yield return new WaitForEndOfFrame();
            LogState("VACUUM"); Capture("waterfall-vacuum.png");
            yield return new WaitForSecondsRealtime(.3f);
            driver.SetThrottle(0);
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!CheckBindings("engine cutoff", false, true)) yield break;
            Capture("waterfall-cutoff.png");
            yield return new WaitForSecondsRealtime(.2f);
            RestoreCamera();
            director.ResetMission();
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!Check(driver.Simulation == null && driver.MissionTimeSeconds == 0 && driver.Throttle == 0 && driver.TimeRate == 1,
                "reset retained flight state") || !CheckBindings("reset", false, true)) yield break;
            phase = "complete";
            Debug.Log("ESP_WATERFALL_SMOKE_OK actual Starship sea-level burn, staging, upper-stage burn above100km, RGBA texture bindings, cutoff and reset; screenshots require visual review");
            Capture("waterfall-reset.png");
            finished = true;
            if (sunlight != null) sunlight.ReviewUtc = previousReviewUtc;
            yield return new WaitForSecondsRealtime(.3f);
            if (!keepOpen) Application.Quit();
        }

        private void Update()
        {
            if (!finished && !failed && deadline > 0 && Time.realtimeSinceStartup >= deadline)
            { Check(false, "120s deadline during " + phase); StopAllCoroutines(); }
        }

        private bool CheckBindings(string label, bool requireBurn, bool requireOff)
        {
            if (!Check(director.RocketVisual != null, label + ": missing rocket")) return false;
            int activePlumes = 0, lit = 0;
            foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>(true))
            {
                if (!plume.gameObject.activeInHierarchy) continue;
                activePlumes++;
                if (!Check(plume.UsesWaterfallTextures && plume.plumeShader != null && plume.plumeShader.name == "ESP/ProceduralPlume" &&
                    TextureOK(plume.plumeNoise1, "fx-noise-1") && TextureOK(plume.plumeNoise2, "fx-noise-2"), label + ": missing256RGBA plume textures/shader")) return false;
            }
            foreach (var renderer in director.RocketVisual.GetComponentsInChildren<Renderer>(true))
            {
                if (!RocketPlume.IsExhaustRenderer(renderer)) continue;
                var material = renderer.sharedMaterial;
                if (!Check(material.shader.isSupported && TextureOK(material.GetTexture("_NoiseTex1"), "fx-noise-1") &&
                    TextureOK(material.GetTexture("_NoiseTex2"), "fx-noise-2") && material.GetFloat("_TextureNoiseStrength") > 0,
                    label + ": actual material lost texture binding or shader support")) return false;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) lit++;
                if (!Check(!requireOff || !renderer.enabled, label + ": exhaust remained enabled at zero flow")) return false;
            }
            return Check(activePlumes > 0 && (!requireBurn || lit > 0), label + ": no active nozzle plumes/burning renderers");
        }

        private static bool TextureOK(Texture texture, string name) => texture is Texture2D image && image.name == name &&
            image.width == 256 && image.height == 256 && GraphicsFormatUtility.HasAlphaChannel(image.graphicsFormat) &&
            !GraphicsFormatUtility.IsSRGBFormat(image.graphicsFormat);

        private bool FrameEngines()
        {
            Vector3 center = Vector3.zero, exhaust = Vector3.zero; int count = 0;
            foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>())
            {
                if (plume.stageIndex != driver.Simulation.State.StageIndex || plume.nozzles == null) continue;
                foreach (var anchor in plume.nozzles)
                    if (anchor != null) { center += anchor.position; exhaust += anchor.TransformDirection(Vector3.down); count++; }
            }
            if (!Check(count > 0 && exhaust.sqrMagnitude > .001f, "no current-stage engine anchors for camera")) return false;
            center /= count; exhaust.Normalize();
            Vector3 side = Vector3.Cross(exhaust, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            side.Normalize();
            if (cameraControls != null) cameraControls.enabled = false;
            float distance = driver.Simulation.State.StageIndex == 0 ? 85f : 65f;
            director.launchCamera.transform.position = center + side * distance + exhaust * (distance * .2f);
            director.launchCamera.transform.LookAt(center + exhaust * 8f, Vector3.up);
            return true;
        }

        private void RestoreCamera()
        {
            if (director == null) return;
            director.ResetLaunchFraming();
            if (cameraControls != null) cameraControls.enabled = cameraWasEnabled;
        }
        private void LogState(string label) => Debug.Log("ESP_WATERFALL_" + label + " t=" + driver.MissionTimeSeconds +
            " altitude=" + driver.AltitudeMeters + " stage=" + driver.Simulation.State.StageIndex + " throttle=" + driver.Throttle +
            " pressurePa=" + driver.Simulation.Physics.PressurePascals + " source=" + driver.WeatherSourceLabel);
        private bool Check(bool condition, string message)
        {
            if (condition) return true;
            failed = true; Debug.LogError("ESP_WATERFALL_SMOKE_FAIL " + message); RestoreCamera();
            if (sunlight != null) sunlight.ReviewUtc = previousReviewUtc;
            if (!keepOpen) Application.Quit(1);
            return false;
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(16, 52, 900, 28), "WATERFALL TEXTURE QA · REAL SIMULATED FLIGHT · " + phase + (daylight ? " · DAYLIGHT UTC OVERRIDE" : ""));
        }
        private static void Capture(string file) => ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, file));
    }
}
