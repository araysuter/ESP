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
        bool cameraCheckFailed;
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
        bool EnterOnboard(MissionDirector director, FlightCameraControls controls, string context)
        {
            var rocket = director.RocketVisual;
            var hull = rocket ? rocket.Find("UpperAssembly") : null;
            int total = 0, active = 0, eligible = 0;
            if (hull)
                foreach (var renderer in hull.GetComponentsInChildren<Renderer>(true))
                {
                    total++;
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) active++;
                    var view = rocket.GetComponent<RocketPresentation>();
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        !renderer.GetComponentInParent<ReentryPlasma>() && !RocketPlume.IsExhaustRenderer(renderer) &&
                        (!view || !view.exhaust || !renderer.transform.IsChildOf(view.exhaust))) eligible++;
                }
            Debug.Log("ESP_REENTRY_CAMERA " + context + " map=" + director.IsMapView +
                " controls=" + (controls != null) + " enabled=" + (controls && controls.enabled) +
                " passive=" + (controls && controls.passiveView) + " onboardBefore=" + (controls && controls.IsOnboardView) +
                " rocket=" + (rocket ? rocket.name : "null") + " hull=" + (hull ? hull.name : "null") +
                " renderers=" + total + " active=" + active + " eligible=" + eligible);
            // A previous user action may already have selected this view.
            if (controls && !controls.IsOnboardView) controls.ToggleOnboardView();
            bool entered = controls && controls.IsOnboardView;
            Debug.Log("ESP_REENTRY_CAMERA " + context + " onboardAfter=" + entered);
            if (!entered)
            {
                cameraCheckFailed = true;
                Debug.LogWarning("ESP_REENTRY_CAMERA_FAIL " + context + "; continuing flight to collect return evidence");
            }
            return entered;
        }

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
            var towerRoot = director.EnvironmentRoot.Find(LaunchEnvironment.StarbaseTowerName);
            var tower = towerRoot ? towerRoot.GetComponent<TowerCatchAnimator>() : null;
            if (!tower || !tower.IsBound || !tower.AtCatchHeight || !tower.IsOpen)
            { Fail("tower is not bound, high and open before launch"); yield break; }
            yield return Shot("00-tower-waiting");
            director.Launch(); flight.SetTimeRate(20);
            float deadline = Time.realtimeSinceStartup + 240;
            while (!flight.OrbitVerified && !flight.Simulation.State.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            if (!flight.CanBeginReentry) { Fail("nominal Starship did not verify orbit"); yield break; }
            yield return Shot("01-orbit-ready");
            var cameraControls = director.launchCamera.GetComponent<FlightCameraControls>();
            if (EnterOnboard(director, cameraControls, "orbit"))
            {
                yield return Shot("02-orbit-onboard");
                cameraControls.ToggleOnboardView();
                if (cameraControls.IsOnboardView)
                {
                    cameraCheckFailed = true;
                    Debug.LogWarning("ESP_REENTRY_CAMERA_FAIL chase camera did not restore; continuing flight");
                }
            }
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
            int plasmaAltitudeIndex = 0, flipCaptureIndex = 0;
            double flipStart = -1;
            double[] flipOffsets = { 0, .5, 1, 2, 3 };
            double[] plasmaAltitudes = { 60000, 50000, 40000, 30000, 20000 };
            while ((flight.IsPlanningReturn || !flight.ReturnController.IsComplete) && Time.realtimeSinceStartup < deadline)
            {
                if (flight.ReturnPlanningFailed) { Fail(flight.ReentryStatus); yield break; }
                var phase = flight.IsPlanningReturn ? StarshipReturnPhase.Planning : flight.ReturnController.Phase;
                if (flipStart < 0 && phase == StarshipReturnPhase.LandingFlip)
                    flipStart = flight.MissionTimeSeconds;
                if (phase != last)
                {
                    Debug.Log("ESP_REENTRY_PHASE " + phase + " t=" + flight.MissionTimeSeconds + " altitude=" + flight.AltitudeMeters +
                        " miss=" + (flight.IsPlanningReturn ? 0 : flight.ReturnController.TargetDistanceMeters) + " fuel=" + flight.Simulation.State.PropellantKg);
                    last = phase; yield return Shot("phase-" + phase);
                }
                flight.SetTimeRate(flight.CanHighWarp ? 1000 : 20);
                if (flipStart >= 0 && flipCaptureIndex < flipOffsets.Length &&
                    flight.MissionTimeSeconds - flipStart >= flipOffsets[flipCaptureIndex])
                {
                    double elapsed = flight.MissionTimeSeconds - flipStart;
                    Debug.Log("ESP_REENTRY_FLIP_CAPTURE requested=" + flipOffsets[flipCaptureIndex] + " elapsed=" + elapsed +
                        " throttle=" + flight.Throttle + " altitude=" + flight.AltitudeMeters);
                    yield return Shot("powered-flip-" + flipCaptureIndex);
                    flipCaptureIndex++;
                }
                if (plasmaAltitudeIndex < plasmaAltitudes.Length && phase == StarshipReturnPhase.AtmosphericEntry &&
                    flight.AltitudeMeters < plasmaAltitudes[plasmaAltitudeIndex])
                {
                    Debug.Log("ESP_REENTRY_PLASMA altitude=" + flight.AltitudeMeters + " airspeed=" + flight.Simulation.Physics.AirSpeedMetersPerSecond);
                    yield return Shot("plasma-" + (plasmaAltitudes[plasmaAltitudeIndex++] / 1000) + "km");
                    if (plasmaAltitudeIndex == 2)
                    {
                        if (EnterOnboard(director, cameraControls, "plasma"))
                        {
                            yield return Shot("plasma-onboard");
                            cameraControls.ToggleOnboardView();
                            if (cameraControls.IsOnboardView)
                            {
                                cameraCheckFailed = true;
                                Debug.LogWarning("ESP_REENTRY_CAMERA_FAIL plasma chase restore; continuing flight");
                            }
                        }
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
            towerRoot = director.EnvironmentRoot.Find(LaunchEnvironment.StarbaseTowerName);
            tower = towerRoot ? towerRoot.GetComponent<TowerCatchAnimator>() : null;
            if (!tower || !tower.IsBound || !tower.AtCatchHeight) { Fail("caught with tower missing or below catch height"); yield break; }
            if (flipCaptureIndex != flipOffsets.Length) { Fail("powered flip captures incomplete"); yield break; }
            flight.SetTimeRate(1); yield return new WaitForSecondsRealtime(7);
            yield return Shot("caught-arms-closed");
            director.ResetMission();
            if (flight.ReentryActive || flight.CanBeginReentry) { Fail("reset retained return state"); yield break; }
            if (cameraCheckFailed) { Fail("onboard camera check failed; return and reset evidence collected"); yield break; }
            Debug.Log("ESP_REENTRY_OK real pad-to-orbit-to-catch, same authoritative simulation, reset");
            Application.Quit(0);
        }
    }
}
