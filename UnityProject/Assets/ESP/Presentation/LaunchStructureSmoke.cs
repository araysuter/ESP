using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    // Opt-in asset review uses the shipping site/prefab construction and no flight-state overrides.
    public sealed class LaunchStructureSmoke : MonoBehaviour
    {
        private bool review;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espGantryReview") >= 0)
                new GameObject("Launch structure review").AddComponent<LaunchStructureSmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director == null) { Fail("Missing director"); yield break; }
            MissionSession.Instance.NewMission();
            director.GetComponent<MissionSunlight>().ReviewUtc = DateTime.UtcNow.Date.AddHours(18);
            review = true;
            director.GoToLaunch(1); director.SelectVehicle(2);
            yield return new WaitForSecondsRealtime(1);
            var structure = director.EnvironmentRoot.Find("NASA historical LC-39 gantry");
            if (structure == null || structure.GetComponentsInChildren<Renderer>().Length == 0)
            { Fail("Missing imported gantry"); yield break; }
            Bounds bounds = default; bool first = true;
            foreach (var renderer in structure.GetComponentsInChildren<Renderer>())
            { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            if (Mathf.Abs(bounds.size.y - 105.7f) > .3f || Mathf.Abs(bounds.min.y) > .2f)
            { Fail("Gantry scale/base " + bounds); yield break; }
            director.launchCamera.transform.position = new Vector3(180, 95, -220);
            director.launchCamera.transform.LookAt(new Vector3(-30, 48, 0));
            director.launchCamera.GetComponent<FlightCameraControls>().ResetFraming();
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "nasa-gantry-review.png"));
            yield return new WaitForSecondsRealtime(.4f);
            director.ShowMap(); director.GoToLaunch(1); director.ResetMission();
            yield return new WaitForSecondsRealtime(.5f);
            if (director.EnvironmentRoot.Find("NASA historical LC-39 gantry") == null)
            { Fail("Reset lost gantry"); yield break; }
            director.GoToLaunch(0);
            yield return null;
            if (director.EnvironmentRoot.Find("NASA historical LC-39 gantry") != null)
            { Fail("Historical Kennedy gantry appeared at Starbase"); yield break; }
            Debug.Log("ESP_GANTRY_SMOKE_OK imported model, height, base, site reset and Starbase isolation");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") < 0) Application.Quit();
        }
        private void OnGUI()
        { if (review) GUI.Label(new Rect(14, 52, 600, 28), "ASSET REVIEW · HISTORICAL LC-39 · DAYLIGHT OVERRIDE · ILLUSTRATIVE PLACEMENT"); }
        private static void Fail(string reason) { Debug.LogError("ESP_GANTRY_SMOKE_FAIL " + reason); Application.Quit(1); }
    }
}
