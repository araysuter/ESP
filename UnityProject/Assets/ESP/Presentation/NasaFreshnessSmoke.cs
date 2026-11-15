using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in packaged check against actual NASA observations.</summary>
    public sealed class NasaFreshnessSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espNasaFreshnessSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director) director.gameObject.AddComponent<NasaFreshnessSmoke>();
        }
        IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var layers = director.mapLayers;
            MissionSession.Instance.NewMission(); director.ShowMap();
            // Do not select clouds: this check must exercise normal startup.
            float deadline = Time.realtimeSinceStartup + 140;
            while (!layers.CloudsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            if (!layers.CloudsVisible || layers.SelectedIndex != 1) { Fail("Automatic observed clouds unavailable: " + layers.Status); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_STARTUP " + director.MapCredit);
            var original = layers.CloudDensityTexture;
            string cloudDate = layers.DisplayedDate;
            director.SelectLayer(0); yield return null;
            if (layers.CloudsVisible || layers.DisplayedIndex != -1 || layers.earth.material.mainTexture != layers.offlineTexture)
            { Fail("Surface selection"); yield break; }
            director.SelectLayer(2);
            if (!layers.CloudsVisible || layers.CloudDensityTexture != original || layers.DisplayedDate != cloudDate || layers.IsLoading)
            { Fail("Surface -> Clouds did not restore cached texture/date immediately"); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_CLOUD_RESTORE same-texture same-date no-download");
            // First satellite selection must finish despite repeated clicks on
            // that same loading mode. It must leave observed clouds visible while
            // pending, with the requested/displayed distinction exposed to the UI.
            director.SelectLayer(1);
            for (int click = 0; click < 12; click++) { director.SelectLayer(1); yield return null; }
            deadline = Time.realtimeSinceStartup + 200;
            while (layers.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
            if (layers.DisplayedIndex != 0 || layers.CloudsVisible)
            { Fail("Satellite did not load after repeated same-mode clicks: " + layers.Status); yield break; }
            var satellite = layers.earth.material.mainTexture;
            string satelliteDate = layers.DisplayedDate;
            // Exercise the same director API used by the three map buttons.
            for (int cycle = 0; cycle < 8; cycle++)
            {
                director.SelectLayer(2);
                if (!layers.CloudsVisible || layers.CloudDensityTexture != original || layers.DisplayedDate != cloudDate || layers.IsLoading)
                { Fail("Satellite -> Clouds cache restore"); yield break; }
                director.SelectLayer(0);
                if (layers.CloudsVisible || layers.DisplayedIndex != -1 || layers.earth.material.mainTexture != layers.offlineTexture)
                { Fail("Clouds -> Surface restore"); yield break; }
                director.SelectLayer(1);
                if (layers.CloudsVisible || layers.DisplayedIndex != 0 || layers.earth.material.mainTexture != satellite || layers.DisplayedDate != satelliteDate || layers.IsLoading)
                { Fail("Surface -> Satellite cache restore"); yield break; }
                yield return null;
            }
            director.SelectLayer(2);
            Debug.Log("ESP_NASA_FRESHNESS_SWITCHES_OK repeated-clicks eight-cached-mode-cycles texture-date-identity");
            layers.RefreshLatest();
            deadline = Time.realtimeSinceStartup + 140;
            while (layers.CloudDensityTexture == original && Time.realtimeSinceStartup < deadline) yield return null;
            if (!layers.CloudsVisible || layers.CloudDensityTexture == original) { Fail("Manual latest refresh did not replace observations: " + layers.Status); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_REFRESH " + director.MapCredit);
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "nasa-freshness-latest.png"));
            yield return new WaitForSecondsRealtime(1);
            // Use an invalid advertised selection to force a real WMS error/empty
            // response. Existing observations must remain with their original date.
            original = layers.CloudDensityTexture;
            string date = layers.DisplayedDate, layer = layers.layerIds[1];
            layers.layerIds[1] = "ESP_NONEXISTENT_LAYER_FOR_FAILURE_TEST";
            layers.Select(1,true);
            deadline = Time.realtimeSinceStartup + 85;
            while (layers.Status.StartsWith("Loading") && Time.realtimeSinceStartup < deadline) yield return null;
            layers.layerIds[1] = layer;
            if (!layers.CloudsVisible || layers.CloudDensityTexture != original || layers.DisplayedDate != date || !layers.Status.Contains("retained"))
            { Fail("Failed refresh changed retained data/date or lacked failure status: " + layers.Status); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_FAILURE " + director.MapCredit);
            layers.Select(-1); yield return null;
            if (layers.CloudsVisible || layers.DisplayedDate != "" || Shader.GetGlobalFloat("_EspObservedCloudsEnabled") != 0)
            { Fail("Offline restore"); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_OK automatic latest cached-mode-cycles repeated-clicks manual-refresh failed-refresh-retention offline");
            Application.Quit();
        }
        static void Fail(string message) { Debug.LogError("ESP_NASA_FRESHNESS_FAIL " + message); Application.Quit(1); }
    }
}
