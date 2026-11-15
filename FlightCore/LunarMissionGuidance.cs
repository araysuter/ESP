using System;
using System.Threading;

namespace FlightCore
{
    public enum LunarMissionPhase { Idle, Planning, CoastToDeparture, DepartureBurn, CoastToCorrection, CorrectionBurn, CoastToMoon, CaptureBurn, ReturnCoast, Complete, Failed }

    /// <summary>Experimental finite-burn lunar guidance. The predictor proposes an
    /// encounter; the live vehicle always moves through ordinary forces and fuel use.</summary>
    public sealed class LunarMissionGuidance
    {
        LunarEphemeris? ephemeris;
        Vector3d initialPosition,initialVelocity,burnAxis;
        double epoch,available,remainingDeltaV,burnStart,correctionAt;
        bool moonEncountered;
        int candidates;
        public LunarMissionPhase Phase { get; private set; }
        public LunarManeuver Maneuver { get; private set; }
        public LunarTransferPlan? Plan { get; private set; }
        public string Status { get; private set; } = "Lunar guidance idle";
        public int PlanningCandidates => Volatile.Read(ref candidates);
        public bool IsComplete => Phase==LunarMissionPhase.Complete || Phase==LunarMissionPhase.Failed;
        public bool IsBurning => Phase==LunarMissionPhase.DepartureBurn || Phase==LunarMissionPhase.CorrectionBurn || Phase==LunarMissionPhase.CaptureBurn;
        public bool IsActive => Phase!=LunarMissionPhase.Idle && !IsComplete;
        public bool CanCoastWarp => Phase==LunarMissionPhase.CoastToDeparture || Phase==LunarMissionPhase.CoastToCorrection || Phase==LunarMissionPhase.CoastToMoon || Phase==LunarMissionPhase.ReturnCoast;
        public double DepartureBurnStart => burnStart;
        public bool NeedsRealTime(RocketSimulation sim)
        {
            if(IsBurning)return true;
            double t=sim.State.MissionTime;
            if(Phase==LunarMissionPhase.CoastToDeparture && t>=burnStart-60)return true;
            if(Phase==LunarMissionPhase.CoastToCorrection && t>=correctionAt-10)return true;
            if(Phase==LunarMissionPhase.CoastToMoon && Maneuver==LunarManeuver.LunarOrbit && Plan!=null && t>=Plan.MoonClosestTime-900)return true;
            return false;
        }

        public bool TryStart(RocketSimulation simulation, LunarManeuver maneuver, out string reason)
        {
            if(simulation==null)throw new ArgumentNullException(nameof(simulation));
            var state=simulation.State;
            if(Phase!=LunarMissionPhase.Idle){reason="A lunar maneuver is already active";return false;}
            if(simulation.Celestial==null){reason="The lunar ephemeris is unavailable";return false;}
            if(!state.IsLaunched || state.IsComplete || simulation.Primary!=CelestialPrimary.Earth)
            {reason="Start from a live Earth orbit";return false;}
            if(simulation.BoosterBurning || simulation.Throttle>0)
            {reason="Cut throttle and wait for boosters to stop before planning";return false;}
            if(simulation.GravityMultiplier!=1){reason="Lunar planning requires normal gravity";return false;}
            var orbit=OrbitDiagnostics.Compute(state.Position,state.Velocity);
            if(!orbit.IsBound || orbit.MinimumWgs84AltitudeBoundMeters<120000)
            {reason="A bound Earth orbit above 120 km is required";return false;}
            available=simulation.LunarAvailableDeltaV();
            if(available<=0){reason="No usable propellant or thrust remains";return false;}
            initialPosition=state.Position;initialVelocity=state.Velocity;epoch=state.MissionTime;
            ephemeris=simulation.Celestial;Maneuver=maneuver;Phase=LunarMissionPhase.Planning;
            Status="Searching lunar trajectories";reason="";return true;
        }

        /// <summary>Run on a worker while simulation time is paused.</summary>
        public bool Calculate(CancellationToken cancellation,out string reason)
        {
            if(Phase!=LunarMissionPhase.Planning || ephemeris==null)throw new InvalidOperationException("No lunar search is active");
            bool found=LunarTransferPlanner.TryPlan(ephemeris,initialPosition,initialVelocity,epoch,Maneuver,available,cancellation,
                count=>Interlocked.Exchange(ref candidates,count),out var plan,out reason);
            Plan=plan;return found;
        }
        /// <summary>Commit the finished plan on the simulation's owning thread.</summary>
        public bool Activate(RocketSimulation sim,out string reason)
        {
            if(Phase!=LunarMissionPhase.Planning||Plan==null){reason="No lunar plan is available";return false;}
            var state=sim.State;
            if(Math.Abs(state.MissionTime-epoch)>1e-6 || (state.Position-initialPosition).Magnitude()>.01 || (state.Velocity-initialVelocity).Magnitude()>.001)
            {reason="Flight changed while planning";Phase=LunarMissionPhase.Failed;Status=reason;return false;}
            double duration=sim.LunarBurnSeconds(Plan.DeltaV.Magnitude());
            burnStart=Plan.DepartureTime-duration*.5;
            if(double.IsNaN(duration)||double.IsInfinity(duration)||burnStart<epoch+30)
            {reason="Insufficient time or thrust for the planned departure burn";Phase=LunarMissionPhase.Failed;Status=reason;return false;}
            sim.EnableLunarPointing();burnAxis=Plan.DeltaV.Normalize();remainingDeltaV=Plan.DeltaV.Magnitude();
            sim.ReturnCommand=burnAxis;Phase=LunarMissionPhase.CoastToDeparture;Status="Coasting to lunar departure burn";reason="";return true;
        }
        public void Abort(RocketSimulation sim,string reason)
        { sim.EndLunarPointing();Phase=LunarMissionPhase.Failed;Status=reason; }
        public void Cancel(RocketSimulation sim) => Abort(sim,"Lunar guidance cancelled");

        public void Update(RocketSimulation sim)
        {
            if(!IsActive || Phase==LunarMissionPhase.Planning || Plan==null || ephemeris==null)return;
            var state=sim.State;
            if(state.IsComplete){Abort(sim,"Lunar guidance stopped: "+state.Status);return;}
            if(sim.GravityMultiplier!=1){Abort(sim,"Lunar guidance stopped because gravity was changed");return;}
            if(Phase==LunarMissionPhase.CoastToDeparture)
            {
                sim.Throttle=0;sim.ReturnCommand=burnAxis;
                if(state.MissionTime>=burnStart) { Phase=LunarMissionPhase.DepartureBurn;Status="Executing lunar departure burn"; }
            }
            if(Phase==LunarMissionPhase.DepartureBurn || Phase==LunarMissionPhase.CorrectionBurn)
            {
                sim.ReturnCommand=burnAxis;
                if(remainingDeltaV<=.02)
                {
                    sim.Throttle=0;
                    if(Phase==LunarMissionPhase.DepartureBurn)
                    { Phase=LunarMissionPhase.CoastToCorrection;correctionAt=state.MissionTime+1800;Status="Coasting to lunar course correction"; }
                    else { Phase=LunarMissionPhase.CoastToMoon;Status="Coasting toward lunar encounter"; }
                }
                else
                {
                    if(state.PropellantKg<=0){Abort(sim,"Lunar burn incomplete: propellant exhausted");return;}
                    double thrust=sim.ReturnAvailableThrust;
                    if(thrust<=0){Abort(sim,"Lunar burn incomplete: no thrust available");return;}
                    double alignment=StarshipReturnController.Dot(sim.ReturnAxis,burnAxis);
                    sim.Throttle=alignment>.999 ? Math.Min(1,remainingDeltaV*state.MassKg/(thrust*sim.FixedStep)) : 0;
                    remainingDeltaV=Math.Max(0,remainingDeltaV-thrust*sim.Throttle/state.MassKg*sim.FixedStep);
                }
                return;
            }
            if(Phase==LunarMissionPhase.CoastToCorrection && state.MissionTime>=correctionAt)
            {
                sim.Throttle=0;
                if(!LunarTransferPlanner.TryCourseCorrection(ephemeris,state.Position,state.Velocity,state.MissionTime,Plan,out var correction))
                {Abort(sim,"Course correction could not be calculated; resume manual control");return;}
                if(correction.Magnitude()>.05)
                {
                    if(correction.Magnitude()+Plan.CaptureDeltaV>sim.LunarAvailableDeltaV())
                    {Abort(sim,"Insufficient propellant for lunar correction and capture");return;}
                    burnAxis=correction.Normalize();remainingDeltaV=correction.Magnitude();sim.ReturnCommand=burnAxis;
                    Phase=LunarMissionPhase.CorrectionBurn;Status="Executing lunar course correction";return;
                }
                Phase=LunarMissionPhase.CoastToMoon;Status="Coasting toward lunar encounter";
            }
            if(Phase==LunarMissionPhase.CoastToMoon || Phase==LunarMissionPhase.ReturnCoast)
            {
                sim.Throttle=0;
                if(sim.Primary==CelestialPrimary.Moon) moonEncountered=true;
                if(Maneuver==LunarManeuver.FreeReturn)
                {
                    if(moonEncountered && sim.Primary==CelestialPrimary.Earth){Phase=LunarMissionPhase.ReturnCoast;Status="Free return: coasting toward Earth";}
                    if(Phase==LunarMissionPhase.ReturnCoast && state.Position.Magnitude()<40000000 && StarshipReturnController.Dot(state.Position,state.Velocity)<0)
                    {sim.EndLunarPointing();Phase=LunarMissionPhase.Complete;Status="Free return reached Earth vicinity";}
                    else if(state.MissionTime>Plan.MoonClosestTime+16*86400)
                        Abort(sim,"Predicted free return did not reach Earth vicinity");
                    return;
                }
                if(sim.Primary==CelestialPrimary.Moon)
                {
                    var rel=state.Position-ephemeris.MoonPosition(state.MissionTime);
                    var relativeVelocity=state.Velocity-ephemeris.MoonVelocity(state.MissionTime);
                    if(relativeVelocity.Magnitude()>1) sim.ReturnCommand=relativeVelocity.Normalize()*(-1);
                    double captureSeconds=sim.LunarBurnSeconds(Plan.CaptureDeltaV);
                    if(state.MissionTime>=Plan.MoonClosestTime-captureSeconds*.5-10 && rel.Magnitude()<20000000)
                    {Phase=LunarMissionPhase.CaptureBurn;Status="Executing lunar orbit insertion";}
                }
                if(state.MissionTime>Plan.MoonClosestTime+3600 && Phase!=LunarMissionPhase.CaptureBurn)
                {Abort(sim,"Moon encounter was missed; resume manual control");return;}
            }
            if(Phase==LunarMissionPhase.CaptureBurn)
            {
                var rel=state.Position-ephemeris.MoonPosition(state.MissionTime);
                var rv=state.Velocity-ephemeris.MoonVelocity(state.MissionTime);
                double r=rel.Magnitude();var up=rel*(1/r);var tangent=rv-up*StarshipReturnController.Dot(rv,up);
                if(tangent.Magnitude()<1){Abort(sim,"Lunar insertion lost its orbital direction");return;}
                var target=tangent.Normalize()*Math.Sqrt(LunarEphemeris.MoonMu/r);
                var delta=target-rv;double required=delta.Magnitude();
                if(required<3)
                {
                    double energy=StarshipReturnController.Dot(rv,rv)*.5-LunarEphemeris.MoonMu/r;
                    var h=StarshipReturnController.Cross(rel,rv);double h2=StarshipReturnController.Dot(h,h);
                    double eccentricity=Math.Sqrt(Math.Max(0,1+2*energy*h2/(LunarEphemeris.MoonMu*LunarEphemeris.MoonMu)));
                    double peri=h2/(LunarEphemeris.MoonMu*(1+eccentricity));
                    if(energy>=0 || peri<LunarEphemeris.MoonRadius+10000)
                    {Abort(sim,"Lunar insertion did not establish a safe bound orbit");return;}
                    sim.EndLunarPointing();Phase=LunarMissionPhase.Complete;Status="Lunar orbit insertion complete";return;
                }
                if(state.PropellantKg<=0 || sim.ReturnAvailableThrust<=0)
                {Abort(sim,"Lunar orbit insertion incomplete: propellant or thrust exhausted");return;}
                sim.ReturnCommand=delta*(1/required);
                double alignment=StarshipReturnController.Dot(sim.ReturnAxis,sim.ReturnCommand);
                sim.Throttle=alignment>.999 ? Math.Min(1,required*state.MassKg/(sim.ReturnAvailableThrust*sim.FixedStep)) : 0;
                if(state.MissionTime>Plan.MoonClosestTime+3600)Abort(sim,"Lunar orbit insertion timed out");
            }
        }
    }
}
