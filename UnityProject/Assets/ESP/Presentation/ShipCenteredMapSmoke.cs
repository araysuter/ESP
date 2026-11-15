using System;
using System.Collections;
using System.IO;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in packaged regression for ship tracking and Sun/Earth controls.</summary>
    public sealed class ShipCenteredMapSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espShipMapSmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director) director.gameObject.AddComponent<ShipCenteredMapSmoke>();
        }
        IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var driver = GetComponent<RocketFlightDriver>();
            var controls = director.mapCamera.GetComponent<GlobeCameraControls>();
            if (!controls || !GetComponent<SolarSystemMap>()) { Fail("Missing map controls/system"); yield break; }
            for (int vehicle = 0; vehicle < 3; vehicle++)
            {
                MissionSession.Instance.NewMission(false); director.SelectVehicle(vehicle);
                director.GoToLaunch(vehicle % 2); director.ShowMap(); controls.ShowShip();
                yield return new WaitForEndOfFrame();
                if (!Centered(director, controls)) { Fail("Pad not centered"); yield break; }
                director.Launch(); driver.SetTimeRate(20);
                float deadline = Time.realtimeSinceStartup + 130;
                while (driver.AltitudeMeters < 12000 && Time.realtimeSinceStartup < deadline) yield return null;
                if (driver.AltitudeMeters < 12000) { Fail("Vehicle " + vehicle + " ascent timeout"); yield break; }
                driver.SetTimeRate(1); director.ShowMap();
                yield return new WaitForEndOfFrame();
                if (!Centered(director, controls)) { Fail("Launched vehicle not centered"); yield break; }
                var previous = controls.FocusPosition;
                double started = driver.MissionTimeSeconds;
                driver.SetTimeRate(20);
                deadline = Time.realtimeSinceStartup + 10;
                while (driver.MissionTimeSeconds < started + 5 && Time.realtimeSinceStartup < deadline) yield return null;
                driver.SetTimeRate(1); yield return new WaitForEndOfFrame();
                if (Vector3.Distance(previous, controls.FocusPosition) < .00001f || !Centered(director, controls))
                { Fail("Camera did not follow moving vehicle"); yield break; }
                yield return Shot("ship-" + vehicle);
                controls.ShowSystem(); yield return new WaitForEndOfFrame();
                if (controls.Distance < 1000 || !Centered(director, controls)) { Fail("System zoom lost vehicle center"); yield break; }
                yield return Shot("system-" + vehicle);
                controls.ShowShip(); yield return new WaitForEndOfFrame();
                if (controls.Distance >= 1000 || !Centered(director, controls)) { Fail("SHIP failed to restore tracked view"); yield break; }
                director.GoToLaunch(director.SelectedSiteIndex);
                yield return new WaitForSecondsRealtime(1);
                if (!director.Inset.PreviewIsMap || !director.Inset.HasPreview || !director.Inset.HasMarker)
                { Fail("Flight map inset lost vehicle"); yield break; }
                director.ResetMission(); director.ShowMap(); controls.ShowShip();
                yield return new WaitForEndOfFrame();
                if (driver.HasPosition || !Centered(director, controls)) { Fail("Reset retained state or lost pad center"); yield break; }
                Debug.Log("ESP_SHIP_MAP_VEHICLE_OK " + vehicle + " ascent moving-center system ship inset reset");
            }
            Debug.Log("ESP_SHIP_MAP_OK vehicles=3 sites=2 real-ascent center-follow system-toggle inset reset");
            Application.Quit();
        }
        static bool Centered(MissionDirector director, GlobeCameraControls controls)
        {
            var p = director.mapCamera.WorldToViewportPoint(controls.FocusPosition);
            if (p.z <= 0 || Math.Abs(p.x - .5f) > .002f || Math.Abs(p.y - .5f) > .002f) return false;
            // Independently project the actual driver state, not only camera target.
            if (director.FlightDriver is RocketFlightDriver driver && driver.HasPosition)
            {
                var state = driver.Simulation.State;
                var ecef = EarthFrames.InertialToEcef(state.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
                var point = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
                p = director.mapCamera.WorldToViewportPoint(point);
                return p.z > 0 && Math.Abs(p.x - .5f) < .002f && Math.Abs(p.y - .5f) < .002f;
            }
            return true;
        }
        static IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "ship-map-" + name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        static void Fail(string reason) { Debug.LogError("ESP_SHIP_MAP_FAIL " + reason); Application.Quit(1); }
    }
}
