using System;
using System.Threading;
using System.Threading.Tasks;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    public sealed partial class RocketFlightDriver
    {
        public Shader reentryShader;
        StarshipReturnController returnController;
        Task<Exception> returnPlanning;
        CancellationTokenSource returnPlanningCancellation;
        string returnPlanningError;
        float returnPlanningStarted;
        public float ReturnPlanningElapsedSeconds => IsPlanningReturn ? Time.realtimeSinceStartup - returnPlanningStarted : 0;
        public bool IsPlanningReturn => returnPlanning != null;
        public bool ReturnPlanningFailed => returnPlanningError != null;
        ReentryPlasma reentryPlasma;
        TowerCatchAnimator catchTower;
        string returnNotice;
        // The return presentation centers the upper-stage mesh on the point-mass
        // anchor. Catch lugs are an illustrative 21 m above that anchor.
        const float ShipMeshCenter = 98.0019f;
        float returnVisualBlend;
        public bool ReentryActive => returnController != null;
        public bool ReturnNeedsRealTime => !IsPlanningReturn && returnController != null &&
            (returnController.Phase == StarshipReturnPhase.LandingFlip || returnController.Phase == StarshipReturnPhase.PoweredLanding);
        public bool CanBeginReentry => !ReentryActive && !IsPaused && !MissionSession.BlocksInput &&
            director != null && (director.VehicleIndex == 1 || director.VehicleIndex == 3) && OrbitVerified && simulation != null &&
            simulation.Primary == CelestialPrimary.Earth &&
            simulation.State.StageIndex == 1 && !simulation.HasUsedCheats && !simulation.State.IsComplete;
        public string ReentryStatus => (simulation != null && simulation.HasUsedCheats ? "Modified flight · " : "") + (returnPlanningError ?? (IsPlanningReturn ? string.Format("Planning return · {0} trajectories checked · {1:0}s elapsed\nSimulation time paused · camera and map available", returnController.PlanningCompletedCandidates, ReturnPlanningElapsedSeconds) : returnController != null ? returnController.Status :
            returnNotice ?? "Return to Starbase requires verified Starship orbit. Propellant, approach speed and alignment constrain the catch."));
        public StarshipReturnController ReturnController => IsPlanningReturn ? null : returnController;

        public void BeginReentry()
        {
            if (!CanBeginReentry) return;
            var target = LaunchSites.All[0];
            var controller = new StarshipReturnController(target.Latitude * Math.PI / 180,
                target.Longitude * Math.PI / 180, 100);
            if (!controller.TryStart(simulation, out returnNotice)) return;
            returnController = controller;
            AssistanceEnabled = false; TimeRate = 1; accumulator = 0; actualTimeRate = 0;
            director.PrepareReturnEnvironment();
            launchSite = target;
            var tower = director.EnvironmentRoot.Find(LaunchEnvironment.StarbaseTowerName);
            catchTower = TowerCatchAnimator.Attach(tower);
            reentryPlasma = ReentryPlasma.Create(director.RocketVisual, Vector3.up * ShipMeshCenter,
                4.65f, 52.37f, reentryShader);
            returnPlanningStarted = Time.realtimeSinceStartup;
            Debug.Log("ESP_RETURN_PLANNING_STARTED");
            returnPlanningCancellation = new CancellationTokenSource();
            var cancellation = returnPlanningCancellation.Token;
            // One worker owns this controller and its private clones. It never touches
            // Unity objects or the authoritative simulation. Task completion publishes
            // the complete plan back to the main thread before flight advances.
            returnPlanning = Task.Run(() =>
            {
                try
                {
                    while (!cancellation.IsCancellationRequested && controller.Phase == StarshipReturnPhase.Planning)
                        controller.AdvancePlanning();
                    return (Exception)null;
                }
                catch (Exception error) { return error; }
            });
        }
        bool AdvanceReturnPlanning()
        {
            if (returnPlanningError != null) return true;
            if (returnPlanning == null) return false;
            if (!returnPlanning.IsCompleted) return true;
            Exception error = returnPlanning.GetAwaiter().GetResult();
            Debug.Log("ESP_RETURN_PLANNING_FINISHED elapsed=" + ReturnPlanningElapsedSeconds.ToString("F2") +
                " candidates=" + returnController.PlanningCompletedCandidates + " phase=" + returnController.Phase);
            returnPlanning = null;
            returnPlanningCancellation.Dispose(); returnPlanningCancellation = null;
            if (error != null)
            {
                Debug.LogException(error);
                returnPlanningError = "Return planning failed · reset the mission to retry";
            }
            return true;
        }
        void CancelReturnPlanning()
        {
            returnPlanningCancellation?.Cancel();
            returnPlanningCancellation?.Dispose();
            returnPlanningCancellation = null; returnPlanning = null; returnPlanningError = null;
        }
        void OnDestroy() => CancelReturnPlanning();
        Vector3 InertialDirectionToRender(Vector3d direction, double lat, double lon)
        {
            var e = EarthFrames.InertialToEcef(direction,
                Aerodynamics.EarthAngularVelocityRadiansPerSecond * simulation.State.MissionTime);
            return new Vector3((float)(-Math.Sin(lon)*e.X + Math.Cos(lon)*e.Y),
                (float)(Math.Cos(lat)*Math.Cos(lon)*e.X + Math.Cos(lat)*Math.Sin(lon)*e.Y + Math.Sin(lat)*e.Z),
                (float)(-Math.Sin(lat)*Math.Cos(lon)*e.X - Math.Sin(lat)*Math.Sin(lon)*e.Y + Math.Cos(lat)*e.Z));
        }
        void RenderReentry(Vector3d ecef, double lat, double lon)
        {
            if (returnController == null || director.RocketVisual == null) return;
            var axis = InertialDirectionToRender(simulation.ReturnAttitudeDirection, lat, lon).normalized;
            var air = simulation.AirRelativeVelocity;
            Vector3 flow = InertialDirectionToRender(air, lat, lon);
            // Keep the windward belly toward the relative airflow, with a stable
            // perpendicular reference during the retrograde/landing flips.
            Vector3 belly = Vector3.ProjectOnPlane(flow, axis).normalized;
            if (belly.sqrMagnitude < .01f) belly = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
            if (belly.sqrMagnitude < .01f) belly = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            director.RocketVisual.rotation = Quaternion.LookRotation(belly, axis) * Quaternion.Euler(0, -90, 0);
            returnVisualBlend = Mathf.MoveTowards(returnVisualBlend, 1, Time.unscaledDeltaTime * .5f);
            director.RocketVisual.position = Vector3.Lerp(Vector3.up * director.RocketBaseHeightMeters,
                -axis * ShipMeshCenter, returnVisualBlend);
            if (reentryPlasma)
                reentryPlasma.SetConditions((float)simulation.Physics.AirSpeedMetersPerSecond,
                    (float)simulation.Physics.DensityKgPerCubicMeter,
                    director.RocketVisual.InverseTransformDirection(flow), !simulation.State.IsComplete);
            if (catchTower) catchTower.SetApproachPose((IsPlanningReturn ? 0 : (float)returnController.CatchArmClosure01), simulation.State.MissionTime);
        }
        void ClearReentry()
        {
            CancelReturnPlanning();
            if (reentryPlasma) Destroy(reentryPlasma.gameObject);
            if (catchTower) catchTower.ResetWaitingPose();
            reentryPlasma = null; catchTower = null; returnController = null; returnNotice = null; returnVisualBlend = 0;
        }
    }
}
