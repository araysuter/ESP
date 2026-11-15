using System;
using System.Reflection;
using System.Threading;
using FlightCore;

/// <summary>Full finite-burn mission fixtures. Coast steps are accelerated for a
/// bounded headless test; burns retain the real 20 ms flight step.</summary>
public static class LunarGuidanceTests
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Set(RocketSimulation s,string name,object value)=>typeof(RocketSimulation).GetField(name,Flags)!.SetValue(s,value);
    static RocketSimulation Clone(RocketSimulation s,double step)=>(RocketSimulation)typeof(RocketSimulation).GetMethod("ReturnClone",Flags)!.Invoke(s,new object[]{step})!;
    static double Dot(Vector3d a,Vector3d b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    static Vector3d Cross(Vector3d a,Vector3d b)=>new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    public static void Run()
    {
        RejectInvalidStarts();
        foreach(int scenario in new[]{0,1,2,3})
            foreach(var mode in new[]{LunarManeuver.LunarOrbit,LunarManeuver.FreeReturn})
                RunMission(scenario,mode);
        Console.WriteLine("Lunar guidance finite-burn missions passed.");
    }
    static RocketSimulation Create(DateTime epoch,bool infinite,double node,double inclination,bool sls=false)
    {
        var air=new WeatherAtmosphere(null,epoch,TimeSpan.FromHours(3));
        var stage=sls ? RealVehiclePresets.BuildSlsStages()[1] : infinite ? RealVehiclePresets.BuildStarshipStages()[1] : RealVehiclePresets.BuildSuperStarshipStages()[1];
        var sim=new RocketSimulation(10,new[]{stage},air,0,0,0,null,epoch);
        double r=EarthFrames.SemiMajorAxisMeters+220000, speed=Math.Sqrt(Gravity.EarthGravitationalParameter/r);
        Set(sim,"position",new Vector3d(r*Math.Cos(node),r*Math.Sin(node),0));
        Set(sim,"velocity",new Vector3d(-speed*Math.Sin(node)*Math.Cos(inclination),speed*Math.Cos(node)*Math.Cos(inclination),speed*Math.Sin(inclination)));
        Set(sim,"launched",true);Set(sim,"commanded",true);if(infinite)sim.InfiniteFuel=true;return sim;
    }
    static void RejectInvalidStarts()
    {
        var epoch=new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc);
        var sim=Create(epoch,false,0,0);var guidance=new LunarMissionGuidance();
        sim.Throttle=1;
        if(guidance.TryStart(sim,LunarManeuver.FreeReturn,out _))throw new Exception("Powered maneuver accepted");
        sim.Throttle=0;Set(sim,"fuel",0d);
        if(guidance.TryStart(sim,LunarManeuver.FreeReturn,out _))throw new Exception("Empty tank accepted");
        Set(sim,"fuel",1d);
        if(!guidance.TryStart(sim,LunarManeuver.FreeReturn,out var reason))throw new Exception("Low-fuel setup rejected: "+reason);
        var before=sim.State;
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();bool threw=false;
            try { guidance.Calculate(cancelled.Token,out _); } catch(OperationCanceledException) { threw=true; }
            if(!threw)throw new Exception("Cancelled search kept running");
        }
        if(guidance.Calculate(CancellationToken.None,out _))throw new Exception("Fuel-insufficient transfer was accepted");
        if((sim.State.Position-before.Position).Magnitude()>0 || (sim.State.Velocity-before.Velocity).Magnitude()>0 || sim.State.MissionTime!=before.MissionTime)
            throw new Exception("Failed planning changed the live vessel");
        guidance.Cancel(sim);
        var finiteSls=Create(epoch,false,0,0,true);var finiteGuidance=new LunarMissionGuidance();
        if(!finiteGuidance.TryStart(finiteSls,LunarManeuver.LunarOrbit,out reason))throw new Exception("SLS fuel test could not start: "+reason);
        var finiteBefore=finiteSls.State;
        if(finiteGuidance.Calculate(CancellationToken.None,out _))throw new Exception("Fuel-insufficient SLS mission was accepted");
        if(finiteSls.State.MissionTime!=finiteBefore.MissionTime || finiteSls.State.PropellantKg!=finiteBefore.PropellantKg)
            throw new Exception("Failed SLS planning consumed live time or fuel");
        if(sim.Throttle!=0 || guidance.Phase!=LunarMissionPhase.Failed)throw new Exception("Cancellation did not restore control");
    }
    static void CheckAbortPaths(RocketSimulation sim,LunarMissionGuidance planned)
    {
        // Reuse the expensive computed plan, but exercise each abort on isolated
        // vehicle/controller copies. These are failure-path tests, not trajectories.
        var copyMethod=typeof(object).GetMethod("MemberwiseClone",Flags)!;
        LunarMissionGuidance Copy()=>(LunarMissionGuidance)copyMethod.Invoke(planned,null)!;
        void Phase(LunarMissionGuidance g,LunarMissionPhase phase)=>typeof(LunarMissionGuidance)
            .GetField("<Phase>k__BackingField",Flags)!.SetValue(g,phase);
        void Failed(RocketSimulation vehicle,LunarMissionGuidance g,string cause)
        {
            if(g.Phase!=LunarMissionPhase.Failed || vehicle.Throttle!=0 || !vehicle.ManualRotation)
                throw new Exception("Unsafe lunar abort: "+cause);
        }
        var vessel=Clone(sim,.02);var guidance=Copy();guidance.Cancel(vessel);Failed(vessel,guidance,"cancel");
        vessel=Clone(sim,.02);guidance=Copy();Phase(guidance,LunarMissionPhase.DepartureBurn);
        Set(vessel,"fuel",0d);guidance.Update(vessel);Failed(vessel,guidance,"fuel exhausted during departure");
        vessel=Clone(sim,.02);guidance=Copy();vessel.GravityMultiplier=.5;
        guidance.Update(vessel);Failed(vessel,guidance,"changed gravity");
        vessel=Clone(sim,.02);guidance=Copy();Phase(guidance,LunarMissionPhase.CoastToMoon);
        Set(vessel,"time",planned.Plan!.MoonClosestTime+3601);
        guidance.Update(vessel);Failed(vessel,guidance,"missed Moon encounter");
    }
    static void RunMission(int scenario,LunarManeuver mode)
    {
        var epoch=scenario==0 || scenario==3 ? new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc) : scenario==1 ? new DateTime(2026,6,21,12,0,0,DateTimeKind.Utc) : new DateTime(2027,2,2,6,0,0,DateTimeKind.Utc);
        double node=scenario==0 || scenario==3?0:scenario==1?.7:2.1, inc=scenario==0 || scenario==3?0:scenario==1?.49:.8;
        var sim=Create(epoch,scenario>=2,node,inc,scenario==3);var ephemeris=new LunarEphemeris(epoch);
        var guidance=new LunarMissionGuidance();
        if(!guidance.TryStart(sim,mode,out var reason))throw new Exception("Start failed: "+reason);
        var snapshot=sim.State;
        if(!guidance.Calculate(CancellationToken.None,out reason))throw new Exception(mode+" planning failed: "+reason);
        if(sim.State.MissionTime!=snapshot.MissionTime || (sim.State.Position-snapshot.Position).Magnitude()>0)throw new Exception("Search advanced the live vessel");
        if(!guidance.Activate(sim,out reason))throw new Exception("Activation failed: "+reason);
        if(scenario==3 && mode==LunarManeuver.LunarOrbit) CheckAbortPaths(sim,guidance);
        double step=10;int count=0;
        while(!guidance.IsComplete && sim.State.MissionTime<21*86400)
        {
            var state=sim.State;
            double wanted=(guidance.IsBurning || (guidance.Phase==LunarMissionPhase.CoastToDeparture && state.MissionTime>guidance.DepartureBurnStart-120) ||
                (guidance.Phase==LunarMissionPhase.CoastToMoon && mode==LunarManeuver.LunarOrbit && guidance.NeedsRealTime(sim))) ? .02 : 10;
            if(wanted!=step){sim=Clone(sim,wanted);step=wanted;}
            guidance.Update(sim);if(guidance.IsComplete)break;
            sim.Step();if(++count>3000000)throw new Exception("Guidance test exceeded step budget");
        }
        if(guidance.Phase!=LunarMissionPhase.Complete)throw new Exception(mode+" failed: "+guidance.Status);
        if(sim.Throttle!=0 || !sim.ManualRotation)throw new Exception("Guidance did not return control safely");
        if(mode==LunarManeuver.LunarOrbit)
        {
            var state=sim.State;var rel=state.Position-ephemeris.MoonPosition(state.MissionTime);
            var rv=state.Velocity-ephemeris.MoonVelocity(state.MissionTime);double r=rel.Magnitude(), energy=Dot(rv,rv)/2-LunarEphemeris.MoonMu/r;
            var h=Cross(rel,rv);double h2=Dot(h,h),ecc=Math.Sqrt(Math.Max(0,1+2*energy*h2/(LunarEphemeris.MoonMu*LunarEphemeris.MoonMu)));
            double peri=h2/(LunarEphemeris.MoonMu*(1+ecc));
            if(sim.Primary!=CelestialPrimary.Moon || energy>=0 || peri<LunarEphemeris.MoonRadius+10000)throw new Exception("Lunar orbit is not safely bound");
            sim=Clone(sim,10);for(int i=0;i<120;i++)sim.Step();
            if(sim.State.IsComplete || sim.Primary!=CelestialPrimary.Moon)throw new Exception("Lunar orbit did not coast safely");
        }
        else
        {
            var elements=OrbitDiagnostics.Compute(sim.State.Position,sim.State.Velocity);
            if(sim.Primary!=CelestialPrimary.Earth || elements.PerigeeRadiusMeters<EarthFrames.SemiMajorAxisMeters+StandardAtmosphere.VacuumGeometricAltitudeMeters)throw new Exception("Free return enters Earth's atmosphere");
            sim=Clone(sim,10);double smallest=double.PositiveInfinity;bool passed=false;
            for(int i=0;i<20000;i++)
            {
                var state=sim.State;double radius=state.Position.Magnitude();smallest=Math.Min(smallest,radius);
                if(i>0 && Dot(state.Position,state.Velocity)>=0){passed=true;break;}
                sim.Step();if(sim.State.IsComplete)throw new Exception("Free return impacted Earth");
            }
            if(!passed || smallest>40000000)throw new Exception("Free return did not reach Earth periapsis");
            if(smallest<EarthFrames.SemiMajorAxisMeters+StandardAtmosphere.VacuumGeometricAltitudeMeters)throw new Exception("Free return entered Earth's atmosphere");
        }
        Console.WriteLine("Lunar mission passed: "+scenario+" "+mode+" t="+sim.State.MissionTime);
    }
}
