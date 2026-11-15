using System;
using System.Collections;
using System.IO;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    // Explicit development-only run: starts on pad and never fabricates flight state.
    public sealed class ReentrySmoke : MonoBehaviour
    {
        RocketFlightDriver flight;
        string directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espReentrySmoke") >= 0)
                new GameObject("Physical Starship return review").AddComponent<ReentrySmoke>();
        }
        void Fail(string reason) { Debug.LogError("ESP_REENTRY_FAIL " + reason); Application.Quit(1); }
        IEnumerator Shot(string name)
        {
            int previous = flight.TimeRate; flight.SetTimeRate(1);
            yield return null; // Let the HUD consume the new phase before capture.
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            yield return null;
            flight.SetTimeRate(previous);
        }
        IEnumerator Start()
        {
            yield return null;
            var director = FindFirstObjectByType<MissionDirector>();
            flight = FindFirstObjectByType<RocketFlightDriver>();
            directory = Path.Combine(Application.persistentDataPath, "ReentryReview");
            Directory.CreateDirectory(directory);
            if (!director || !flight || !MissionSession.Instance) { Fail("missing mission components"); yield break; }
            MissionSession.Instance.NewMission(); director.SelectVehicle(1); director.GoToLaunch(0);
            if (flight.CanBeginReentry) { Fail("button allowed on pad"); yield break; }
            director.Launch(); flight.SetTimeRate(20);
            float deadline = Time.realtimeSinceStartup + 240;
            while (!flight.OrbitVerified && !flight.Simulation.State.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            if (!flight.CanBeginReentry) { Fail("nominal Starship did not verify orbit"); yield break; }
            yield return Shot("01-orbit-ready");
            var original = flight.Simulation;
            double fuel = original.State.PropellantKg;
            flight.BeginReentry();
            if (!flight.ReentryActive || original != flight.Simulation || original.State.PropellantKg != fuel)
            { Fail("return changed simulation identity or fuel"); yield break; }
            deadline = Time.realtimeSinceStartup + 1100;
            StarshipReturnPhase last = StarshipReturnPhase.Idle;
            bool plasmaCaptured = false;
            while (!flight.ReturnController.IsComplete && Time.realtimeSinceStartup < deadline)
            {
                var phase = flight.ReturnController.Phase;
                if (phase != last)
                {
                    Debug.Log("ESP_REENTRY_PHASE " + phase + " t=" + flight.MissionTimeSeconds + " altitude=" + flight.AltitudeMeters +
                        " miss=" + flight.ReturnController.TargetDistanceMeters + " fuel=" + flight.Simulation.State.PropellantKg);
                    last = phase; yield return Shot("phase-" + phase);
                }
                flight.SetTimeRate(flight.CanHighWarp ? 1000 : 20);
                if (!plasmaCaptured && flight.Simulation.Physics.AirSpeedMetersPerSecond > 2000 && flight.AltitudeMeters < 60000)
                { plasmaCaptured = true; yield return Shot("plasma-entry"); }
                yield return null;
            }
            Debug.Log("ESP_REENTRY_TERMINAL phase=" + flight.ReturnController.Phase +
                " lugMiss=" + flight.ReturnController.CatchLugDistanceMeters +
                " lugHeight=" + flight.ReturnController.CatchLugAltitudeErrorMeters +
                " lugSpeed=" + flight.ReturnController.CatchLugSpeedMetersPerSecond +
                " peakHeat=" + flight.ReturnController.PeakHeatFluxWattsPerSquareMeter +
                " fuel=" + flight.Simulation.State.PropellantKg);
            yield return Shot("terminal");
            if (!flight.Simulation.IsCaught) { Fail("return ended without catch: " + flight.ReentryStatus); yield break; }
            flight.SetTimeRate(1); yield return new WaitForSecondsRealtime(7);
            yield return Shot("caught-arms-closed");
            director.ResetMission();
            if (flight.ReentryActive || flight.CanBeginReentry) { Fail("reset retained return state"); yield break; }
            Debug.Log("ESP_REENTRY_OK real pad-to-orbit-to-catch, same authoritative simulation, reset");
            Application.Quit(0);
        }
    }
}
