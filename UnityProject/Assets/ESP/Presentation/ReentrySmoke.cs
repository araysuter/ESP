using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
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
            if (Debug.isDebugBuild && (Array.IndexOf(Environment.GetCommandLineArgs(), "-espReentrySmoke") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-espReentryReady") >= 0))
                new GameObject("Physical Starship return review").AddComponent<ReentrySmoke>();
        }
        [Serializable] sealed class ReturnFixture
        {
            public double missionTime, fuel, dryMass;
            public double[] position, velocity;
            public string weatherSource, weatherValidUtc;
            public double[] weatherAltitude, weatherTemperature, weatherPressure, windEast, windNorth;
        }
        void RecordFixture(RocketSimulation sim)
        {
            var state = sim.State;
            var atmosphere = (WeatherAtmosphere)typeof(RocketFlightDriver).GetField("launchAtmosphere", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flight);
            var profile = (WeatherProfile)typeof(WeatherAtmosphere).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(atmosphere);
            var fixture = new ReturnFixture { missionTime = state.MissionTime, fuel = state.PropellantKg, dryMass = state.MassKg-state.PropellantKg,
                position = new[] { state.Position.X, state.Position.Y, state.Position.Z }, velocity = new[] { state.Velocity.X, state.Velocity.Y, state.Velocity.Z },
                weatherSource = atmosphere.SourceDescription, weatherValidUtc = profile?.ValidTimeUtc.ToString("O") };
            if (profile != null)
            {
                int n = profile.Levels.Count;
                fixture.weatherAltitude = new double[n]; fixture.weatherTemperature = new double[n]; fixture.weatherPressure = new double[n];
                fixture.windEast = new double[n]; fixture.windNorth = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var level = profile.Levels[i]; fixture.weatherAltitude[i] = level.GeometricAltitudeMeters;
                    fixture.weatherTemperature[i] = level.Atmosphere.TemperatureKelvin; fixture.weatherPressure[i] = level.Atmosphere.PressurePascals;
                    fixture.windEast[i] = level.Atmosphere.WindEastNorthUpMetersPerSecond.X; fixture.windNorth[i] = level.Atmosphere.WindEastNorthUpMetersPerSecond.Y;
                }
            }
            File.WriteAllText(Path.Combine(directory, "return-fixture.json"), JsonUtility.ToJson(fixture, true));
            Debug.Log("ESP_REENTRY_ATMOSPHERE " + atmosphere.SourceDescription);
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
            var cameraControls = director.launchCamera.GetComponent<FlightCameraControls>();
            cameraControls.ToggleOnboardView();
            if (!cameraControls.IsOnboardView) { Fail("onboard camera did not engage"); yield break; }
            yield return Shot("02-orbit-onboard");
            cameraControls.ToggleOnboardView();
            if (cameraControls.IsOnboardView) { Fail("chase camera did not restore"); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espReentryReady") >= 0)
            {
                flight.SetTimeRate(1);
                Debug.Log("ESP_REENTRY_QA_READY verified orbit from actual pad launch; controls released to user");
                yield break;
            }
            var original = flight.Simulation;
            double fuel = original.State.PropellantKg;
            RecordFixture(original);
            flight.BeginReentry();
            if (!flight.ReentryActive || original != flight.Simulation || original.State.PropellantKg != fuel)
            { Fail("return changed simulation identity or fuel"); yield break; }
            deadline = Time.realtimeSinceStartup + 1100;
            StarshipReturnPhase last = StarshipReturnPhase.Idle;
            int plasmaAltitudeIndex = 0;
            double[] plasmaAltitudes = { 60000, 50000, 40000, 30000, 20000 };
            while ((flight.IsPlanningReturn || !flight.ReturnController.IsComplete) && Time.realtimeSinceStartup < deadline)
            {
                if (flight.ReturnPlanningFailed) { Fail(flight.ReentryStatus); yield break; }
                var phase = flight.IsPlanningReturn ? StarshipReturnPhase.Planning : flight.ReturnController.Phase;
                if (phase != last)
                {
                    Debug.Log("ESP_REENTRY_PHASE " + phase + " t=" + flight.MissionTimeSeconds + " altitude=" + flight.AltitudeMeters +
                        " miss=" + (flight.IsPlanningReturn ? 0 : flight.ReturnController.TargetDistanceMeters) + " fuel=" + flight.Simulation.State.PropellantKg);
                    last = phase; yield return Shot("phase-" + phase);
                }
                flight.SetTimeRate(flight.CanHighWarp ? 1000 : 20);
                if (plasmaAltitudeIndex < plasmaAltitudes.Length && phase == StarshipReturnPhase.AtmosphericEntry &&
                    flight.AltitudeMeters < plasmaAltitudes[plasmaAltitudeIndex])
                {
                    Debug.Log("ESP_REENTRY_PLASMA altitude=" + flight.AltitudeMeters + " airspeed=" + flight.Simulation.Physics.AirSpeedMetersPerSecond);
                    yield return Shot("plasma-" + (plasmaAltitudes[plasmaAltitudeIndex++] / 1000) + "km");
                    if (plasmaAltitudeIndex == 2)
                    {
                        cameraControls.ToggleOnboardView(); yield return Shot("plasma-onboard"); cameraControls.ToggleOnboardView();
                    }
                }
                yield return null;
            }
            if (flight.IsPlanningReturn) { Fail("return planning timed out"); yield break; }
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
