using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Opt-in development-player check of the live staging HUD and flight driver.</summary>
    public sealed class StagingHudSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver driver;
        private MissionSession session;
        private VisualElement ui;
        private bool failed, keepOpen;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espStagingHudSmoke") >= 0)
                new GameObject("Staging HUD smoke").AddComponent<StagingHudSmoke>();
        }

        private IEnumerator Start()
        {
            keepOpen = Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") >= 0;
            deadline = Time.realtimeSinceStartup + 120;
            yield return null;
            yield return null;
            director = FindFirstObjectByType<MissionDirector>();
            driver = FindFirstObjectByType<RocketFlightDriver>();
            session = MissionSession.Instance;
            var hud = FindFirstObjectByType<MissionUI>();
            if (!Check(director != null && driver != null && session != null && hud != null, "mission wiring")) yield break;
            ui = hud.GetComponent<UIDocument>().rootVisualElement;
            yield return Resolution(1280, 720);
            if (failed) yield break;
            session.NewMission();
            director.GoToLaunch(0);
            yield return Settle();
            if (!Visibility(true, false, "flight")) yield break;

            // Each preset is checked before launch, after real stage separation, and after reset.
            for (int vehicle = 0; vehicle < 3; vehicle++)
            {
                director.SelectVehicle(vehicle);
                yield return Settle();
                if (!Check(director.VehicleIndex == vehicle && driver.Simulation == null, "preset " + vehicle) ||
                    !StageRow(1, "CURRENT", driver.StagePropellantCapacityKg(0), driver.StagePropellantCapacityKg(0)) ||
                    !StageRow(2, "QUEUED", driver.StagePropellantCapacityKg(1), driver.StagePropellantCapacityKg(1))) yield break;
                var booster = ui.Q<VisualElement>("solid-booster-stage");
                if (!Check(booster != null && (booster.resolvedStyle.display != DisplayStyle.None) == (vehicle == 2), "booster row preset " + vehicle)) yield break;
                if (vehicle == 2 && !BoosterRow("ATTACHED", driver.BoosterPropellantCapacityKg)) yield break;
                if (vehicle == 2) Capture("staging-hud-sls-prelaunch.png");

                driver.SetThrottle(vehicle == 2 ? 1 : 0);
                director.Launch();
                driver.DisableAssistance();
                if (!Check(driver.Simulation != null, "launch preset " + vehicle)) yield break;
                if (vehicle == 2)
                {
                    driver.SetTimeRate(20);
                    yield return Until(() => driver.MissionTimeSeconds >= 6, "SLS powered ascent");
                    if (failed) yield break;
                    session.OpenPause();
                    yield return Settle();
                    if (!Check(driver.Simulation.BoosterBurning && driver.Simulation.BoosterAttached &&
                        driver.Simulation.BoosterPropellantKg < driver.BoosterPropellantCapacityKg.Value, "SLS booster burns") ||
                        !BoosterRow("BURNING", driver.Simulation.BoosterPropellantKg) || !CurrentStage()) yield break;
                    Capture("staging-hud-sls-burning.png");
                    session.ResumeMission();
                    yield return Until(() => !driver.Simulation.BoosterBurning, "SLS natural burnout");
                    if (failed) yield break;
                    session.OpenPause();
                    yield return Settle();
                    if (!Check(driver.Simulation.BoosterAttached && driver.Simulation.BoosterPropellantKg <= 0, "SLS spent boosters remain attached") ||
                        !BoosterRow("SPENT", driver.Simulation.BoosterPropellantKg)) yield break;
                    session.ResumeMission();
                    director.Stage(); // Shipping control first jettisons spent boosters.
                    session.OpenPause();
                    yield return Settle();
                    if (!Check(!driver.Simulation.BoosterAttached && driver.Simulation.State.StageIndex == 0, "SLS booster jettison is separate from core stage") ||
                        !BoosterRow("SEPARATED", null) || !CurrentStage()) yield break;
                    session.ResumeMission();
                }
                director.Stage();
                session.OpenPause();
                yield return Settle();
                if (!Check(driver.Simulation.State.StageIndex == 1, "stage transition preset " + vehicle) ||
                    !StageRow(1, "SEPARATED", null, driver.StagePropellantCapacityKg(0)) || !CurrentStage()) yield break;
                session.ResumeMission();
                director.ResetMission();
                yield return Settle();
                if (!Check(driver.Simulation == null, "reset preset " + vehicle) ||
                    !StageRow(1, "CURRENT", driver.StagePropellantCapacityKg(0), driver.StagePropellantCapacityKg(0)) ||
                    !StageRow(2, "QUEUED", driver.StagePropellantCapacityKg(1), driver.StagePropellantCapacityKg(1))) yield break;
                if (vehicle == 2 && !BoosterRow("ATTACHED", driver.BoosterPropellantCapacityKg)) yield break;
            }

            director.ShowMap();
            yield return Settle();
            if (!Visibility(false, true, "map")) yield break;
            director.GoToLaunch(director.SelectedSiteIndex);
            yield return Settle();
            if (!Visibility(true, false, "flight restored")) yield break;
            yield return Submit("flight-information-toggle");
            yield return Settle();
            if (!Visibility(false, true, "information open") || !Check(Shown(ui.Q<VisualElement>("flight-information-panel")), "information panel open")) yield break;
            yield return Submit("close-information");
            yield return Settle();
            if (!Visibility(true, false, "information closed")) yield break;
            yield return Resolution(1280, 480);
            if (failed || !Visibility(false, true, "short window")) yield break;
            Capture("staging-hud-short-window.png");
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.5f);
            Debug.Log("ESP_STAGING_HUD_SMOKE_OK all presets, real staging, SLS natural burnout/jettison, reset, map/details, short-window fallback; screenshots require visual review");
            if (!keepOpen) Application.Quit();
        }

        private bool CurrentStage()
        {
            var fuel = driver.FuelTelemetry;
            return StageRow(fuel.StageNumber, fuel.StageKg <= 0 ? "EMPTY" : "CURRENT", fuel.StageKg,
                driver.StagePropellantCapacityKg(fuel.StageNumber - 1));
        }
        private bool BoosterRow(string state, double? remaining) =>
            Row("solid-booster-stage", "SOLID BOOSTERS", state, remaining, driver.BoosterPropellantCapacityKg.Value);
        private bool StageRow(int number, string state, double? remaining, double capacity) =>
            Row("staging-stage-" + number, "STAGE " + number, state, remaining, capacity);
        private bool Row(string name, string title, string state, double? remaining, double capacity)
        {
            var row = ui.Q<VisualElement>(name);
            var labels = row?.Query<Label>().ToList();
            double fraction = remaining.HasValue && capacity > 0 ? Math.Max(0, Math.Min(1, remaining.Value / capacity)) : 0;
            // HUD casts the fraction to float before formatting.
            string readout = remaining.HasValue ? string.Format(CultureInfo.CurrentCulture, "{0:N0} kg  ·  {1:P0}", remaining.Value, (float)fraction) : "—";
            return Check(Shown(row) && labels != null && labels.Count == 3 && labels[0].text == title && labels[1].text == state && labels[2].text == readout,
                name + " expected " + title + "/" + state + "/" + readout + " actual " + (labels == null ? "missing" : string.Join(" / ", labels.ConvertAll(x => x.text))));
        }
        private bool Visibility(bool stack, bool compact, string phase) => Check(
            Shown(ui.Q<VisualElement>("staging-stack")) == stack && Shown(ui.Q<VisualElement>("fuel-readout")) == compact,
            phase + " stack/compact visibility");
        private static bool Shown(VisualElement element) => element != null && element.resolvedStyle.display != DisplayStyle.None;
        private IEnumerator Settle() { yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame(); }
        private IEnumerator Submit(string name)
        {
            var button = ui.Q<Button>(name);
            if (!Check(button != null && button.enabledInHierarchy, "button " + name)) yield break;
            button.Focus();
            yield return null;
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        private IEnumerator Resolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float until = Time.realtimeSinceStartup + 5;
            do { yield return null; } while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until);
            yield return Settle();
            Check(Screen.width == width && Screen.height == height, "resolution " + width + "x" + height + " actual " + Screen.width + "x" + Screen.height);
        }
        private IEnumerator Until(Func<bool> condition, string phase)
        {
            while (!condition())
            {
                if (!Check(Time.realtimeSinceStartup < deadline, "timeout: " + phase) ||
                    !Check(driver.Simulation != null && !driver.Simulation.State.IsLanded, "landed: " + phase)) yield break;
                yield return null;
            }
        }
        private bool Check(bool condition, string reason)
        {
            if (condition && !failed) return true;
            if (!failed) Debug.LogError("ESP_STAGING_HUD_SMOKE_FAIL " + reason);
            failed = true;
            if (!keepOpen) Application.Quit(1);
            return false;
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, name));
    }
}
