using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Opt-in development review of the actual assisted SLS mission and lighting.</summary>
    public sealed class VisualPolishSmoke : MonoBehaviour
    {
        private MissionDirector director;
        private RocketFlightDriver flight;
        private MissionSunlight lighting;
        private FlightCameraControls controls;
        private DateTime? savedReview;
        private bool savedControls, failed;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov, deadline;
        private string directory;
        private double peakAltitude, maxAltitudeDrop;
        private void LateUpdate()
        {
            if (flight == null || flight.Simulation == null || flight.OrbitVerified) return;
            double altitude = flight.AltitudeMeters;
            peakAltitude = Math.Max(peakAltitude, altitude);
            maxAltitudeDrop = Math.Max(maxAltitudeDrop, peakAltitude - altitude);
        }
        private Renderer atmosphereRenderer;
        private bool savedAtmosphere;
        private bool QuitRequested => Array.IndexOf(Environment.GetCommandLineArgs(), "-espVisualPolishQuit") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-espVisualPolishSmoke") >= 0)
                new GameObject("Visual polish review").AddComponent<VisualPolishSmoke>();
        }

        private IEnumerator Start()
        {
            yield return null; yield return null;
            director = FindFirstObjectByType<MissionDirector>();
            flight = FindFirstObjectByType<RocketFlightDriver>();
            var session = MissionSession.Instance;
            if (!Check(director && flight && session, "mission wiring")) { Finish(); yield break; }
            lighting = director.GetComponent<MissionSunlight>();
            if (!Check(lighting && director.launchCamera, "lighting and camera")) { Finish(); yield break; }
            controls = director.launchCamera.GetComponent<FlightCameraControls>();
            if (!Check(controls, "camera controls")) { Finish(); yield break; }
            savedReview = lighting.ReviewUtc; savedControls = controls.enabled;
            savedPosition = director.launchCamera.transform.position;
            savedRotation = director.launchCamera.transform.rotation;
            savedFov = director.launchCamera.fieldOfView;
            directory = Path.Combine(Application.persistentDataPath, "VisualPolishReview");
            Directory.CreateDirectory(directory);
            deadline = Time.realtimeSinceStartup + 240;
            session.NewMission(); session.ResumeMission(); director.SelectVehicle(2); director.GoToLaunch(1);
            yield return null; yield return null;
            if (!Check(director.VehicleIndex == 2 && director.SelectedSiteIndex == 1 && director.RocketVisual, "SLS at Kennedy")) { Finish(); yield break; }
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc); lighting.Apply();
            controls.enabled = false;
            director.launchCamera.transform.position = new Vector3(190, 95, -230);
            director.launchCamera.transform.LookAt(new Vector3(0, 45, 0));
            director.launchCamera.fieldOfView = 50;
            yield return Shot("sls-pad-day");
            var padRotation = director.launchCamera.transform.rotation;
            director.launchCamera.transform.rotation = Quaternion.LookRotation(lighting.LocalSunDirection, Vector3.up);
            yield return Shot("sun-pad-day");
            var sky = director.GetComponent<FlightEarthBackdrop>().BackgroundCamera.GetComponentInChildren<StarField>();
            if (!Check(sky && sky.SolarAngularDiameterDegrees > .52f && sky.SolarAngularDiameterDegrees < .54f && sky.SolarFlareVisibility == 1, "physical solar diameter and daylight flare")) { Finish(); yield break; }
            director.launchCamera.transform.rotation = padRotation;
            if (!Check(!lighting.InEarthShadow && Group("BoosterAssembly") && Group("UpperAssembly") && Group("LeftSRB") && Group("RightSRB"), "daylight and pad assemblies")) { Finish(); yield break; }
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 23, 30, 0, DateTimeKind.Utc); lighting.Apply();
            yield return Shot("sls-pad-sunset-review");
            director.launchCamera.transform.rotation = Quaternion.LookRotation(lighting.LocalSunDirection, Vector3.up);
            yield return Shot("sun-pad-sunset");
            director.launchCamera.transform.rotation = padRotation;
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc); lighting.Apply();
            yield return Shot("sls-pad-night-review");
            if (!Check(lighting.InEarthShadow && sky.SolarFlareVisibility == 0, "night Earth occlusion suppresses flare")) { Finish(); yield break; }
            lighting.ReviewUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc); lighting.Apply();
            director.ResetLaunchFraming(); controls.enabled = true;
            yield return null;
            // Explicit offline atmosphere preserves a deterministic real flight. The lighting override is cosmetic only.
            flight.Launch(LaunchSites.All[1], null, true);
            if (!Check(flight.Simulation != null && flight.AssistanceEnabled, "assisted launch")) { Finish(); yield break; }
            flight.SetTimeRate(20);
            yield return Until(() => flight.MissionTimeSeconds >= 5, "powered ascent");
            if (failed) { Finish(); yield break; }
            director.Stage();
            yield return null;
            if (!Check(flight.Simulation.BoosterBurning && flight.Simulation.BoosterAttached && flight.Simulation.State.StageIndex == 0, "early stage rejected during solid burn") ||
                !Check(Group("BoosterAssembly") && Group("LeftSRB") && Group("RightSRB") && Plume(false, 0) && Plume(true, 0), "core and solid assemblies and plumes active")) { Finish(); yield break; }
            yield return Shot("sls-powered-ascent");
            yield return Until(() => !flight.Simulation.BoosterAttached, "assisted booster jettison");
            if (failed) { Finish(); yield break; }
            yield return null;
            if (!Check(flight.Simulation.State.StageIndex == 0 && Group("BoosterAssembly") && !Group("LeftSRB") && !Group("RightSRB") && Plume(false, 0), "core flight after solid jettison")) { Finish(); yield break; }
            yield return Shot("sls-core-after-srb-separation");
            yield return Until(() => flight.Simulation.State.StageIndex == 1, "assisted core staging");
            if (failed) { Finish(); yield break; }
            yield return null;
            if (!Check(!Group("BoosterAssembly") && Group("UpperAssembly"), "upper stage after core separation")) { Finish(); yield break; }
            yield return Shot("sls-upper-coast");
            yield return Until(() => flight.Throttle > .5f, "ICPS circularization ignition");
            if (failed) { Finish(); yield break; }
            yield return null;
            if (!Check(Plume(false, 1), "ICPS plume at circularization ignition")) { Finish(); yield break; }
            yield return Shot("sls-upper-stage");
            yield return Until(() => flight.OrbitVerified, "verified orbit");
            if (failed) { Finish(); yield break; }
            if (!Check(maxAltitudeDrop < 1000, "SLS ascent has no atmospheric dive (max drop " + maxAltitudeDrop.ToString("F0") + " m)")) { Finish(); yield break; }
            var verifiedOrbit = FlightCore.OrbitDiagnostics.Compute(flight.Simulation.State.Position, flight.Simulation.State.Velocity);
            if (!Check(verifiedOrbit.ApogeeRadiusMeters - FlightCore.EarthFrames.SemiMajorAxisMeters < 1000000, "SLS bounded low Earth orbit")) { Finish(); yield break; }
            Debug.Log("ESP_VISUAL_POLISH_ORBIT perigee=" + verifiedOrbit.MinimumWgs84AltitudeBoundMeters + " apogee=" + (verifiedOrbit.ApogeeRadiusMeters-FlightCore.EarthFrames.SemiMajorAxisMeters));
            flight.SetTimeRate(1);
            session.OpenPause();
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) doc.rootVisualElement.style.opacity = 0;
            controls.enabled = false;
            var state = flight.Simulation.State;
            var ecef = FlightCore.EarthFrames.InertialToEcef(state.Position, FlightCore.Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            var site = LaunchSites.All[1];
            var up = CameraRelativeCoordinates.FromEcef(ecef.X, ecef.Y, ecef.Z, 0, 0, 0, site.Latitude * Math.PI / 180, site.Longitude * Math.PI / 180).normalized;
            var tangent = Vector3.Cross(up, Vector3.right).normalized;
            if (tangent.sqrMagnitude < .1f) tangent = Vector3.forward;
            director.launchCamera.transform.position = director.RocketVisual.position + up * 250 + tangent * 160;
            director.launchCamera.transform.LookAt(director.RocketVisual.position + tangent * 600000 - up * 200000, up);
            yield return Shot("sls-orbit-horizon");
            var atmosphere = FindFirstObjectByType<ContinuousAtmosphere>();
            atmosphereRenderer = atmosphere ? atmosphere.GetComponent<Renderer>() : null;
            if (atmosphereRenderer)
            {
                savedAtmosphere = atmosphereRenderer.enabled;
                atmosphereRenderer.enabled = true;
                yield return MeasureFrames("atmosphere-on");
                atmosphereRenderer.enabled = false;
                yield return MeasureFrames("atmosphere-off");
                atmosphereRenderer.enabled = savedAtmosphere;
            }
            else Debug.LogWarning("ESP_VISUAL_POLISH atmosphere renderer unavailable; frame comparison skipped");
            director.launchCamera.transform.rotation = Quaternion.LookRotation(lighting.LocalSunDirection, up);
            yield return Shot("sls-orbit-sun-review");
            session.ResumeMission();
            director.ResetMission();
            yield return null; yield return null;
            if (!Check(flight.Simulation == null && director.CanLaunch && Group("BoosterAssembly") && Group("UpperAssembly") && Group("LeftSRB") && Group("RightSRB"), "reset restores stack")) { Finish(); yield break; }
            Debug.Log("ESP_VISUAL_POLISH_OK SLS assisted flight, stage guard, visible assemblies/plumes, orbit and reset; captures require visual review");
            Finish();
        }

        private bool Group(string name)
        {
            var t = director.RocketVisual != null ? director.RocketVisual.Find(name) : null;
            if (!t || !t.gameObject.activeInHierarchy) return false;
            foreach (var renderer in t.GetComponentsInChildren<MeshRenderer>())
                if (renderer.enabled && !RocketPlume.IsExhaustRenderer(renderer)) return true;
            return false;
        }
        private bool Plume(bool solid, int stage)
        {
            foreach (var plume in director.RocketVisual.GetComponentsInChildren<RocketPlume>())
            {
                if (plume.solidBooster != solid || plume.stageIndex != stage) continue;
                foreach (var renderer in plume.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.enabled && RocketPlume.IsExhaustRenderer(renderer)) return true;
            }
            return false;
        }
        private IEnumerator Until(Func<bool> condition, string label)
        {
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= deadline || flight.Simulation == null || flight.Simulation.State.IsLanded)
                { Check(false, "timeout or landing: " + label); yield break; }
                yield return null;
            }
        }
        private IEnumerator MeasureFrames(string label)
        {
            var samples = new List<float>();
            // Same paused orbit, camera and settings for both 3 s samples.
            yield return null;
            float until = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < until) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000); }
            samples.Sort();
            if (samples.Count > 0) Debug.Log("ESP_VISUAL_POLISH_FRAME_TIMING " + label + " samples=" + samples.Count +
                " p50_ms=" + samples[samples.Count / 2].ToString("F2") +
                " p95_ms=" + samples[Mathf.Clamp(Mathf.CeilToInt(samples.Count * .95f) - 1, 0, samples.Count - 1)].ToString("F2") +
                " (wall frame intervals; not GPU timing)");
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("ESP_VISUAL_POLISH_CAPTURE " + path + " t=" + flight.MissionTimeSeconds + " lighting=" + lighting.Status + " elevation=" + lighting.SolarElevationDegrees);
            yield return null;
        }
        private bool Check(bool value, string label)
        {
            if (value) { Debug.Log("ESP_VISUAL_POLISH_CHECK " + label); return true; }
            failed = true; Debug.LogError("ESP_VISUAL_POLISH_FAIL " + label); return false;
        }
        private void Finish()
        {
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) doc.rootVisualElement.style.opacity = 1;
            if (atmosphereRenderer) atmosphereRenderer.enabled = savedAtmosphere;
            if (lighting) { lighting.ReviewUtc = savedReview; lighting.Apply(); }
            if (controls && director && director.launchCamera)
            {
                director.launchCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                director.launchCamera.fieldOfView = savedFov; controls.enabled = savedControls;
            }
            if (QuitRequested) Application.Quit(failed ? 1 : 0);
            Destroy(gameObject);
        }
    }
}
