using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in packaged check of the actual Kennedy model and site handoff.</summary>
    public sealed class MobileLauncherSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espMobileLauncherSmoke") >= 0)
                new GameObject("Mobile launcher smoke").AddComponent<MobileLauncherSmoke>();
        }

        private IEnumerator Start()
        {
            yield return null;
            var director = FindFirstObjectByType<MissionDirector>();
            if (!director) { Fail("Missing mission director"); yield break; }
            MissionSession.Instance.NewMission();
            var sunlight = director.GetComponent<MissionSunlight>();
            if (sunlight) sunlight.ReviewUtc = DateTime.UtcNow.Date.AddHours(18); // Explicit daylight review only.
            director.GoToLaunch(1);
            director.SelectVehicle(2);
            yield return new WaitForSecondsRealtime(1);
            var launcher = director.EnvironmentRoot.Find(LaunchEnvironment.ModernLauncherName);
            if (!launcher) { Fail("Missing modern launcher at Kennedy"); yield break; }
            var renderers = launcher.GetComponentsInChildren<Renderer>();
            if (renderers.Length != 2) { Fail("Expected the body and railings"); yield break; }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (!renderer.sharedMaterial || !renderer.sharedMaterial.mainTexture)
                { Fail("Missing source texture on " + renderer.name); yield break; }
            }
            if (Mathf.Abs(bounds.size.y - 121.92f) > .3f || Mathf.Abs(bounds.min.y) > .1f)
            { Fail("Unexpected model bounds " + bounds); yield break; }
            if (launcher.GetComponentsInChildren<Collider>(true).Length != 0)
            { Fail("Launch scenery contains a collider"); yield break; }
            if (!director.RocketVisual || Mathf.Abs(director.RocketVisual.localPosition.y - director.RocketBaseHeightMeters) > .05f)
            { Fail("Vehicle not at launcher visual deck height"); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "nasa-mobile-launcher-pad.png"));
            var flight = director.GetComponent<RocketFlightDriver>();
            director.Launch();
            if (!flight || flight.Simulation == null) { Fail("Mission did not start"); yield break; }
            flight.SetTimeRate(20);
            float timeout = Time.realtimeSinceStartup + 20;
            while (Time.realtimeSinceStartup < timeout && flight.MissionTimeSeconds < 10) yield return null;
            if (flight.MissionTimeSeconds < 10 || !flight.Simulation.State.IsLaunched || flight.Simulation.State.IsLanded)
            { Fail("SLS flight did not advance"); yield break; }
            director.ResetMission();
            yield return null;
            if (!director.EnvironmentRoot.Find(LaunchEnvironment.ModernLauncherName))
            { Fail("Reset lost the launcher"); yield break; }
            director.GoToLaunch(0);
            yield return null;
            if (director.EnvironmentRoot.Find(LaunchEnvironment.ModernLauncherName))
            { Fail("Kennedy launcher appeared at Starbase"); yield break; }
            Debug.Log("ESP_MOBILE_LAUNCHER_SMOKE_OK model, textures, SLS launch, reset and site isolation");
            Application.Quit();
        }

        private static void Fail(string message)
        {
            Debug.LogError("ESP_MOBILE_LAUNCHER_SMOKE_FAIL " + message);
            Application.Quit(1);
        }
    }
}
