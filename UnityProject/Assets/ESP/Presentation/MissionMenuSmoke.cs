using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    // Opt-in packaged verification of the shipping session and fixed-step driver.
    public sealed class MissionMenuSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espMenuSmoke") < 0) return;
            new GameObject("Mission menu smoke").AddComponent<MissionMenuSmoke>();
        }

        private bool Check(bool condition, string message)
        {
            if (condition) return true;
            Debug.LogError("ESP_MENU_SMOKE_FAIL " + message);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") < 0) Application.Quit(1);
            return false;
        }

        private IEnumerator Start()
        {
            yield return null;
            var session = MissionSession.Instance;
            var director = FindFirstObjectByType<MissionDirector>();
            var driver = FindFirstObjectByType<RocketFlightDriver>();
            if (!Check(session != null && director != null && driver != null, "missing session, director or driver")) yield break;
            if (!Check(session.Page == MissionMenuPage.Start && MissionSession.BlocksInput && MissionSession.IsSimulationPaused && driver.IsPaused && director.IsMapView && driver.Simulation == null, "initial menu state")) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "menu-smoke-start.png"));
            yield return new WaitForSecondsRealtime(.2f);
            session.NewMission(false);
            yield return null;
            if (!Check(session.Page == MissionMenuPage.None && !MissionSession.BlocksInput && !driver.IsPaused && director.IsMapView, "new mission")) yield break;
            director.GoToLaunch(0);
            director.Launch();
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(driver.Simulation != null && driver.MissionTimeSeconds > 0, "launch did not advance")) yield break;
            session.OpenPause();
            yield return null;
            if (!Check(session.Page == MissionMenuPage.Pause && MissionSession.BlocksInput && MissionSession.IsSimulationPaused && driver.IsPaused, "pause state")) yield break;
            var before = driver.Simulation.State;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "menu-smoke-pause.png"));
            yield return new WaitForSecondsRealtime(.3f);
            var after = driver.Simulation.State;
            if (!Check(before.MissionTime == after.MissionTime && before.PropellantKg == after.PropellantKg && before.Position.X == after.Position.X && before.Position.Y == after.Position.Y && before.Position.Z == after.Position.Z, "state changed while paused")) yield break;
            session.OpenSettings();
            yield return null;
            if (!Check(session.Page == MissionMenuPage.Settings && MissionSession.BlocksInput && MissionSession.IsSimulationPaused && driver.IsPaused, "settings did not retain pause")) yield break;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "menu-smoke-settings.png"));
            session.SetVSync(false);
            session.SetFrameRateLimit(30);
            if (!Check(session.FrameRateLimit == 30 && !session.VSync && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 30, "frame cap settings")) yield break;
            session.SetVSync(true);
            if (!Check(session.FrameRateLimit == 30 && session.VSync && QualitySettings.vSyncCount == 1 && Application.targetFrameRate == -1, "VSync settings")) yield break;
            session.SetVSync(false);
            session.SetFrameRateLimit(60);
            session.Back();
            yield return null;
            if (!Check(session.Page == MissionMenuPage.Pause && MissionSession.BlocksInput && MissionSession.IsSimulationPaused && driver.IsPaused, "back did not return to pause")) yield break;
            session.OpenCheats();
            yield return new WaitForSecondsRealtime(.3f);
            session.SetInfiniteFuel(true);
            session.SetCrashDestruction(false);
            session.SetGravityMultiplier(.5);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "menu-smoke-cheats.png"));
            yield return new WaitForSecondsRealtime(.2f);
            session.SetGravityMultiplier(1);
            if (!Check(session.ModifiedFlight && !driver.OrbitVerified, "cheat use was not sticky")) yield break;
            session.ResetCheats();
            if (!Check(session.ModifiedFlight && !driver.OrbitVerified, "reset cleared modified flight")) yield break;
            session.ResumeMission();
            yield return new WaitForSecondsRealtime(.3f);
            double elapsed = driver.MissionTimeSeconds - before.MissionTime;
            if (!Check(!driver.IsPaused && elapsed > 0 && elapsed < 1.0, "resume did not advance normally: " + elapsed)) yield break;
            session.SetPhysicsVisible(true);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "menu-smoke-physics.png"));
            yield return new WaitForSecondsRealtime(.2f);
            session.ReturnToStart();
            if (!Check(driver.IsPaused && session.HasSession, "return to start lost session or pause")) yield break;
            session.ResumeMission();
            if (!Check(!driver.IsPaused && !director.IsMapView, "resume from start lost flight view")) yield break;
            session.NewMission(false);
            if (!Check(!session.ModifiedFlight && driver.Simulation == null, "new mission retained overrides")) yield break;
            Debug.Log("ESP_MENU_SMOKE_OK");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") < 0) Application.Quit();
        }
    }
}
