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
            int changes = 0;
            Action changed = () => changes++;
            layers.LayerChanged += changed;
            layers.RefreshLatest();
            deadline = Time.realtimeSinceStartup + 140;
            while (changes == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            layers.LayerChanged -= changed;
            if (changes == 0 || !layers.CloudsVisible || layers.CloudDensityTexture == original) { Fail("Manual latest refresh did not replace observations: " + layers.Status); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_REFRESH " + director.MapCredit);
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "nasa-freshness-latest.png"));
            yield return new WaitForSecondsRealtime(1);
            // Use an invalid advertised selection to force a real WMS error/empty
            // response. Existing observations must remain with their original date.
            original = layers.CloudDensityTexture;
            string date = layers.DisplayedDate, layer = layers.layerIds[1];
            layers.layerIds[1] = "ESP_NONEXISTENT_LAYER_FOR_FAILURE_TEST";
            layers.Select(1);
            deadline = Time.realtimeSinceStartup + 85;
            while (layers.Status.StartsWith("Loading") && Time.realtimeSinceStartup < deadline) yield return null;
            layers.layerIds[1] = layer;
            if (!layers.CloudsVisible || layers.CloudDensityTexture != original || layers.DisplayedDate != date || !layers.Status.Contains("retained"))
            { Fail("Failed refresh changed retained data/date or lacked failure status: " + layers.Status); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_FAILURE " + director.MapCredit);
            layers.Select(-1); yield return null;
            if (layers.CloudsVisible || layers.DisplayedDate != "" || Shader.GetGlobalFloat("_EspObservedCloudsEnabled") != 0)
            { Fail("Offline restore"); yield break; }
            Debug.Log("ESP_NASA_FRESHNESS_OK automatic latest manual-refresh failed-refresh-retention offline");
            Application.Quit();
        }
        static void Fail(string message) { Debug.LogError("ESP_NASA_FRESHNESS_FAIL " + message); Application.Quit(1); }
    }
}
