using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in player check and screenshots from real, failed vertical flights.
    /// No injected impact state, synthetic demo checkpoint, or physics shortcuts.</summary>
    public sealed class RocketExplosionSmoke : MonoBehaviour
    {
        private RocketFlightDriver driver;
        private MissionDirector director;
        private RocketExplosion explosion;
        private bool failed;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-espExplosionSmoke") < 0) yield break;
            yield return null;
            driver = FindFirstObjectByType<RocketFlightDriver>();
            director = FindFirstObjectByType<MissionDirector>();
            explosion = FindFirstObjectByType<RocketExplosion>();
            if (!Check(driver != null && director != null && explosion != null && explosion.effectShader != null, "wiring")) yield break;
            MissionSession.Instance?.NewMission(false);
            director.GoToLaunch(0);
            int flag = Array.IndexOf(args, "-espVehicle");
            if (flag >= 0 && flag + 1 < args.Length && int.TryParse(args[flag + 1], out int vehicle)) director.SelectVehicle(vehicle);
            // SLS solids cannot be throttled off. This failure fixture uses Starship or the demo vehicle.
            if (!Check(director.VehicleIndex != 2, "use -espVehicle 0 or 1 for the 12 s cutoff fixture")) yield break;
            int initial = explosion.ExplosionCount;
            yield return FlyToImpact(12, true);
            if (failed) yield break;
            yield return null; // Allow the destruction edge through LateUpdate.
            if (!Check(driver.Simulation.IsDestroyed && explosion.ExplosionCount == initial + 1 && explosion.IsActive, "one destructive impact")) yield break;
            Capture("impact-flash.png");
            yield return new WaitForSecondsRealtime(.4f);
            Capture("impact-fire.png");
            MissionSession.Instance?.OpenPause();
            float frozen = explosion.EffectAgeSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(Mathf.Abs(explosion.EffectAgeSeconds - frozen) < .001f, "menu freezes effect")) yield break;
            MissionSession.Instance?.ResumeMission();
            yield return new WaitForSecondsRealtime(1.5f);
            Capture("impact-smoke.png");
            float timeout = Time.realtimeSinceStartup + 30;
            while (explosion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
            if (!Check(!explosion.IsActive && explosion.ExplosionCount == initial + 1, "effect expires without retrigger")) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(explosion.ExplosionCount == initial + 1, "destroyed state does not replay")) yield break;
            MissionSession.Instance?.RestartMission();
            yield return null;
            if (!Check(!explosion.IsActive && driver.Simulation == null && explosion.ExplosionCount == initial + 1, "restart clears effect")) yield break;
            Capture("impact-reset.png");
            yield return FlyToImpact(12, false);
            if (failed) yield break;
            yield return null;
            if (!Check(!driver.Simulation.IsDestroyed && !explosion.IsActive && explosion.ExplosionCount == initial + 1, "damage-disabled impact has no effect")) yield break;
            MissionSession.Instance?.RestartMission();
            yield return null;
            // Brief, low-speed hop: this explicitly checks the core's non-destructive touchdown.
            yield return FlyToImpact(.2, true, 1);
            if (failed) yield break;
            yield return null;
            if (!Check(!driver.Simulation.IsDestroyed && driver.Simulation.State.IsLanded && explosion.ExplosionCount == initial + 1,
                "gentle touchdown has no effect (speed=" + driver.Simulation.ImpactSpeedMetersPerSecond + ")")) yield break;
            Debug.Log("ESP_EXPLOSION_SMOKE_OK one impact, pause, expiry, restart, damage-disabled impact, gentle touchdown");
            if (Array.IndexOf(args, "-espKeepOpen") < 0) Application.Quit();
        }

        private IEnumerator FlyToImpact(double burnSeconds, bool damage, int timeRate = 20)
        {
            MissionSession.Instance?.SetCrashDestruction(damage);
            driver.SetThrottle(1); // Select manual flight before liftoff.
            director.Launch();
            if (!Check(driver.Simulation != null, "launch")) yield break;
            driver.DisableAssistance(); driver.SetTimeRate(timeRate);
            float timeout = Time.realtimeSinceStartup + 120;
            while (driver.MissionTimeSeconds < burnSeconds && Time.realtimeSinceStartup < timeout) yield return null;
            driver.SetThrottle(0);
            while (!driver.Simulation.State.IsLanded && Time.realtimeSinceStartup < timeout) yield return null;
            driver.SetTimeRate(1);
            Check(driver.Simulation.State.IsLanded, "flight reached impact");
            Debug.Log("ESP_EXPLOSION_FLIGHT t=" + driver.MissionTimeSeconds + " impactSpeed=" + driver.Simulation.ImpactSpeedMetersPerSecond + " damage=" + damage);
        }
        private bool Check(bool condition, string name)
        {
            if (condition) return true;
            failed = true;
            Debug.LogError("ESP_EXPLOSION_SMOKE_FAIL " + name);
            Application.Quit(1);
            return false;
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, name));
    }
}
