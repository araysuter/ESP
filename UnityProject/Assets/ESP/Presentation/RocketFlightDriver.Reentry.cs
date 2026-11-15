using System;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    public sealed partial class RocketFlightDriver
    {
        public Shader reentryShader;
        StarshipReturnController returnController;
        ReentryPlasma reentryPlasma;
        TowerCatchAnimator catchTower;
        string returnNotice;
        // The return presentation centers the upper-stage mesh on the point-mass
        // anchor. Catch lugs are an illustrative 21 m above that anchor.
        const float ShipMeshCenter = 98.0019f;
        float returnVisualBlend;
        public bool ReentryActive => returnController != null;
        public bool CanBeginReentry => !ReentryActive && !IsPaused && !MissionSession.BlocksInput &&
            director != null && director.VehicleIndex == 1 && OrbitVerified && simulation != null &&
            simulation.State.StageIndex == 1 && !simulation.HasUsedCheats && !simulation.State.IsComplete;
        public string ReentryStatus => returnController != null ? (returnController.Phase == StarshipReturnPhase.Planning ? "Planning return · simulation time paused" : returnController.Status) :
            returnNotice ?? "Return to Starbase requires verified Starship orbit. Propellant, approach speed and alignment constrain the catch.";
        public StarshipReturnController ReturnController => returnController;

        public void BeginReentry()
        {
            if (!CanBeginReentry) return;
            var target = LaunchSites.All[0];
            var controller = new StarshipReturnController(target.Latitude * Math.PI / 180,
                target.Longitude * Math.PI / 180, 50);
            if (!controller.TryStart(simulation, out returnNotice)) return;
            returnController = controller;
            AssistanceEnabled = false; TimeRate = 1; accumulator = 0; actualTimeRate = 0;
            director.PrepareReturnEnvironment();
            launchSite = target;
            var tower = director.EnvironmentRoot.Find(LaunchEnvironment.StarbaseTowerName);
            catchTower = TowerCatchAnimator.Attach(tower);
            reentryPlasma = ReentryPlasma.Create(director.RocketVisual, Vector3.up * ShipMeshCenter,
                4.65f, 52.37f, reentryShader);
        }
        bool AdvanceReturnPlanning()
        {
            if (returnController == null || returnController.Phase != StarshipReturnPhase.Planning) return false;
            // The explicitly labelled planning interval holds simulation time, so
            // a trajectory search cannot leave its own scheduled burn in the past.
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            long budget = System.Diagnostics.Stopwatch.Frequency / 200; // 5 ms per rendered frame.
            for (int i = 0; i < 500 && returnController.Phase == StarshipReturnPhase.Planning; i++)
            {
                returnController.Update(simulation);
                if (System.Diagnostics.Stopwatch.GetTimestamp() - started >= budget) break;
            }
            return true;
        }
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
            if (catchTower) catchTower.SetSimulationPose((float)returnController.CatchArmClosure01, 0);
        }
        void ClearReentry()
        {
            if (reentryPlasma) Destroy(reentryPlasma.gameObject);
            if (catchTower) catchTower.SetTarget(0, 0);
            reentryPlasma = null; catchTower = null; returnController = null; returnNotice = null; returnVisualBlend = 0;
        }
    }
}
