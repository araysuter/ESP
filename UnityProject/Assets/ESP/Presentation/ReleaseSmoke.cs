using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Explicit command-line CI probe of the packaged release, never enabled in normal play.</summary>
    public sealed class ReleaseSmoke : MonoBehaviour
    {
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espReleaseSmoke") < 0) return;
            new GameObject("Release package smoke").AddComponent<ReleaseSmoke>();
        }
        private void OnEnable() { Application.logMessageReceived += ObserveLog; }
        private void OnDisable() { Application.logMessageReceived -= ObserveLog; }
        private void ObserveLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) Fail("unhandled exception: " + message);
        }
        private void Fail(string reason)
        {
            if (failed) return;
            failed = true;
            Debug.LogError("ESP_RELEASE_SMOKE_FAIL " + reason);
            Application.Quit(1);
        }
        private IEnumerator Start()
        {
            try { PlanetSmoke.Validate(); }
            catch (Exception error) { Fail("planetary resources: " + error.Message); }
            if (failed) yield break;
            yield return null;
            var director = FindFirstObjectByType<MissionDirector>();
            var driver = FindFirstObjectByType<RocketFlightDriver>();
            var sunlight = FindFirstObjectByType<MissionSunlight>();
            if (!director || !driver || !sunlight || !director.mapCamera || !director.launchCamera)
            { Fail("missing mission scene components"); yield break; }
            if (director.vehiclePrefabs == null || director.vehiclePrefabs.Length != 3)
            { Fail("missing vehicle catalog"); yield break; }
            foreach (var prefab in director.vehiclePrefabs)
                if (!prefab || prefab.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                { Fail("missing vehicle mesh"); yield break; }
            for (int vehicle = 0; vehicle < 3; vehicle++)
            {
                MissionSession.Instance.NewMission(false);
                director.SelectVehicle(vehicle); director.GoToLaunch(0); director.Launch();
                driver.SetTimeRate(20);
                float deadline = Time.realtimeSinceStartup + 30;
                while (!failed && driver.AltitudeMeters < 100 && Time.realtimeSinceStartup < deadline) yield return null;
                if (failed) yield break;
                if (driver.AltitudeMeters < 100 || driver.Simulation == null || !driver.Simulation.State.IsLaunched)
                { Fail("vehicle " + vehicle + " did not ascend"); yield break; }
                director.ResetMission();
                yield return null;
                if (driver.HasPosition) { Fail("reset retained flight state"); yield break; }
            }
            director.ShowMap(); yield return null;
            var sky = director.mapCamera.GetComponentInChildren<StarField>();
            var skySetup = director.mapCamera.GetComponent<StarFieldSetup>();
            // The null graphics device does not retain material texture bindings.
            // Check the serialized assets and created component there; with a real
            // graphics device also verify the actual material binding.
            bool hasGraphics = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            var skyRenderer = sky ? sky.GetComponent<MeshRenderer>() : null;
            if (!sky || !skySetup || !skySetup.shader || !skySetup.sky || !skyRenderer ||
                !skyRenderer.sharedMaterial || (hasGraphics && !skyRenderer.sharedMaterial.GetTexture("_Sky")))
            { Fail("missing offline celestial sky"); yield break; }
            Debug.Log("ESP_RELEASE_SMOKE_OK version=" + Application.version + " platform=" + Application.platform +
                " graphics=" + SystemInfo.graphicsDeviceType + " sky=" + (hasGraphics ? "material-binding" : "asset-reference") +
                " vehicles=3 ascent/reset/map passed");
            Application.Quit(0);
        }
    }
}
