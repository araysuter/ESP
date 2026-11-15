using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in development-player rehearsal using actual controls and flight state.</summary>
    public sealed class RocketSmokeTrailSmoke : MonoBehaviour
    {
        private RocketFlightDriver driver;
        private MissionDirector director;
        private RocketSmokeTrail smoke;
        private MissionSession session;
        private float deadline;
        private bool failed;
        private MissionSunlight lighting;
        private DateTime? previousReviewUtc;
        private bool daylightReview;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espSrbSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director != null && director.GetComponent<RocketSmokeTrailSmoke>() == null)
                director.gameObject.AddComponent<RocketSmokeTrailSmoke>();
        }
        private IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espSrbSmoke") < 0) yield break;
            deadline = Time.realtimeSinceStartup + 110;
            yield return null;
            driver = GetComponent<RocketFlightDriver>(); director = GetComponent<MissionDirector>();
            smoke = GetComponent<RocketSmokeTrail>(); session = MissionSession.Instance;
            if (!Check(driver != null && director != null && smoke != null && smoke.smokeShader != null && session != null, "wiring")) yield break;
            daylightReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-espDaylightReview") >= 0;
            if (daylightReview)
            {
                lighting = GetComponent<MissionSunlight>();
                if (!Check(lighting != null, "daylight review lighting")) yield break;
                previousReviewUtc = lighting.ReviewUtc;
                lighting.ReviewUtc = DateTime.UtcNow.Date.AddHours(18);
                Debug.Log("ESP_SRB_DAYLIGHT_REVIEW_OVERRIDE " + lighting.ReviewUtc.Value.ToString("O") + " lighting only; actual flight state");
            }
            session.NewMission(); director.SelectVehicle(2); director.GoToLaunch(1);
            yield return null;
            if (!Check(director.VehicleIndex == 2 && smoke.ActiveParticleCount == 0, "SLS prelaunch has no smoke")) yield break;
            // A real manual SRB-powered climb keeps the retained trail within the atmosphere.
            // The liquid engines are commanded off; the solid boosters cannot throttle off.
            driver.SetThrottle(0); director.Launch(); driver.DisableAssistance(); driver.SetPitchTarget(60); driver.SetTimeRate(20);
            yield return Until(() => driver.MissionTimeSeconds >= 6, "initial climb");
            if (failed) yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(driver.Simulation.State.IsLaunched && driver.Simulation.BoosterBurning && smoke.ActiveParticleCount > 0, "live SLS emission")) yield break;
            Capture("srb-smoke-pad.png");
            yield return null;
            session.OpenPause();
            yield return new WaitForEndOfFrame();
            int pausedCount = smoke.ActiveParticleCount;
            long pausedTotal = smoke.TotalEmittedCount;
            double pausedAge = smoke.VisualTimeSeconds, pausedMission = driver.MissionTimeSeconds;
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(smoke.ActiveParticleCount == pausedCount && smoke.TotalEmittedCount == pausedTotal &&
                Math.Abs(smoke.VisualTimeSeconds - pausedAge) < 1e-9 && Math.Abs(driver.MissionTimeSeconds - pausedMission) < 1e-9,
                "menu pauses count, emission and visual/mission age")) yield break;
            session.ResumeMission();
            yield return Until(() => driver.MissionTimeSeconds >= 50, "higher climb");
            if (failed) yield break;
            yield return new WaitForEndOfFrame(); Capture("srb-smoke-climb.png");
            // Map view must keep the same smoke lifecycle while the renderer is hidden.
            double mapStart = smoke.VisualTimeSeconds;
            director.ShowMap();
            yield return Until(() => smoke.VisualTimeSeconds >= mapStart + 3, "map smoke clock");
            if (failed) yield break;
            director.GoToLaunch(director.SelectedSiteIndex);
            yield return Until(() => driver.MissionTimeSeconds >= 110, "approach SRB burnout");
            if (failed) yield break;
            // Observe the natural cutoff at 1x so a slow render frame cannot consume
            // most of the 26-second retained tail before the jettison assertion.
            driver.SetTimeRate(1);
            yield return Until(() => !driver.Simulation.BoosterBurning, "actual SRB burnout");
            if (failed) yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(driver.Simulation.BoosterPropellantKg <= .001 && !driver.Simulation.State.IsLanded,
                "SRBs exhausted naturally before impact")) yield break;
            long atCutoff = smoke.TotalEmittedCount;
            double cutoffTime = driver.MissionTimeSeconds;
            if (!Check(smoke.ActiveParticleCount > 0, "existing smoke retained at burnout")) yield break;
            director.Stage(); // The shipping control jettisons exhausted SRBs.
            yield return new WaitForEndOfFrame();
            if (!Check(!driver.Simulation.BoosterAttached && smoke.ActiveParticleCount > 0, "jettison retains emitted smoke")) yield break;
            driver.SetTimeRate(20);
            yield return Until(() => driver.MissionTimeSeconds >= cutoffTime + RocketSmokeTrail.LifetimeSeconds + 1, "26 s tail expiration");
            if (failed) yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(smoke.TotalEmittedCount == atCutoff && smoke.ActiveParticleCount == 0, "no emissions after burnout; all puffs expire")) yield break;
            session.RestartMission();
            yield return new WaitForEndOfFrame();
            if (!Check(driver.Simulation == null && smoke.ActiveParticleCount == 0, "restart clears pool")) yield break;
            director.SelectVehicle(1);
            yield return null;
            long beforeStarship = smoke.TotalEmittedCount;
            director.Launch(); driver.SetTimeRate(20);
            yield return Until(() => driver.MissionTimeSeconds >= 12, "Starship comparison");
            if (failed) yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(driver.Simulation.State.IsLaunched && smoke.ActiveParticleCount == 0 && smoke.TotalEmittedCount == beforeStarship,
                "Starship emits no SRB smoke")) yield break;
            Debug.Log("ESP_SRB_SMOKE_OK real SLS flight, bounded pool, pause, map, natural burnout, jettison, expiry, reset, Starship");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espKeepOpen") < 0) Application.Quit();
        }
        private IEnumerator Until(Func<bool> condition, string phase)
        {
            while (!condition())
            {
                if (!Check(Time.realtimeSinceStartup < deadline, "110 s timeout: " + phase) ||
                    !Check(smoke.ActiveParticleCount >= 0 && smoke.ActiveParticleCount <= RocketSmokeTrail.Capacity, "pool capacity: " + phase)) yield break;
                if (driver.Simulation != null && driver.Simulation.State.IsLanded)
                { Check(false, "unexpected impact: " + phase); yield break; }
                yield return null;
            }
            Check(smoke.ActiveParticleCount <= RocketSmokeTrail.Capacity, "pool capacity: " + phase);
        }
        private bool Check(bool condition, string reason)
        {
            if (condition) return true;
            failed = true; Debug.LogError("ESP_SRB_SMOKE_FAIL " + reason); Application.Quit(1); return false;
        }
        private void OnGUI()
        {
            if (!daylightReview || lighting == null) return;
            GUI.Label(new Rect(20, 170, Screen.width - 40, 70),
                "DEVELOPMENT SMOKE REVIEW · DAYLIGHT UTC OVERRIDE\n" +
                lighting.ReviewUtc?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") + " · Actual simulated flight; lighting review only",
                new GUIStyle(GUI.skin.label) { fontSize = 20 });
        }
        private void OnDestroy()
        {
            if (daylightReview && lighting != null) lighting.ReviewUtc = previousReviewUtc;
        }
        private void Capture(string file)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, file));
            Debug.Log("ESP_SRB_SMOKE_CAPTURE " + file + " mission=" + driver.MissionTimeSeconds +
                " altitude=" + driver.AltitudeMeters + " active=" + smoke.ActiveParticleCount);
        }
    }
}
