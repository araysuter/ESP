using System;
using FlightCore;

internal static class Program
{
    static int Main()
    {
        try
        {
            bool forceDepletion=Array.IndexOf(Environment.GetCommandLineArgs(),"--force-depletion")>=0;bool injected=false;
            double step=Array.IndexOf(Environment.GetCommandLineArgs(),"--half-step")>=0?.01:.02;
            var atmosphere=new WeatherAtmosphere(null,new DateTime(2026,11,14,0,0,0,DateTimeKind.Utc),TimeSpan.FromHours(3));
            var sim=new RocketSimulation(step,RealVehiclePresets.BuildStarshipStages(),atmosphere,25.997*Math.PI/180,-97.156*Math.PI/180);
            var controller=new StarshipReturnController(25.997*Math.PI/180,-97.156*Math.PI/180,50);
            if(controller.TryStart(sim,out _))throw new Exception("Accepted return from pad");
            var ascent=new AssistedAscent(220000,2);sim.Launch();
            for(int i=0;i<100000&&!ascent.HasCutOff&&!sim.State.IsComplete;i++){ascent.Update(sim);sim.Step();}
            if(!ascent.HasCutOff)throw new Exception("Nominal ascent did not orbit");
            for(int i=0;i<(int)Math.Round(60/step);i++)sim.Step();
            Console.WriteLine($"Orbit fixture t={sim.State.MissionTime:F2}s fuel={sim.State.PropellantKg:F1}kg alt={sim.State.AltitudeMeters:F1}m minimumOrbitAltitude={OrbitDiagnostics.Compute(sim.State.Position,sim.State.Velocity).MinimumWgs84AltitudeBoundMeters:F1}m");
            double fuel=sim.State.PropellantKg;var initial=sim.State;
            var unsupported=new RocketSimulation(step,RealVehiclePresets.BuildSlsStages(),atmosphere,0,0);
            if(new StarshipReturnController(0,0,50).TryStart(unsupported,out _))throw new Exception("Unsupported vehicle accepted");
            // Empty-tank rejection fixture only: the mission below resumes its original measured fuel.
            var tank=typeof(RocketSimulation).GetField("fuel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            tank.SetValue(sim,0d);
            if(new StarshipReturnController(25.997*Math.PI/180,-97.156*Math.PI/180,50).TryStart(sim,out _))throw new Exception("Empty-tank return accepted");
            tank.SetValue(sim,fuel);
            if(!controller.TryStart(sim,out var reason))throw new Exception(reason);
            if((sim.State.Position-initial.Position).Magnitude()!=0||(sim.State.Velocity-initial.Velocity).Magnitude()!=0)throw new Exception("Return activation changed translational state");
            int ticks=0;
            while(!sim.State.IsComplete&&ticks++<1600000)
            {
                controller.Update(sim);
                if(forceDepletion&&!injected&&controller.Phase==StarshipReturnPhase.LandingFlip) {tank.SetValue(sim,0d);injected=true;Console.WriteLine("Fault fixture: all remaining propellant lost at landing flip; continue ordinary physics.");}
                if(controller.Phase!=StarshipReturnPhase.Planning)sim.Step();
                var s=sim.State;
                if(s.PropellantKg>fuel+1e-8||s.PropellantKg<0)throw new Exception("Return created fuel");
                fuel=s.PropellantKg;
                if((controller.Phase==StarshipReturnPhase.PoweredLanding||controller.Phase==StarshipReturnPhase.LandingFlip)&&ticks%1000==0)Console.WriteLine($"LAND t={s.MissionTime:F1} alt={s.AltitudeMeters:F1} dist={controller.TargetDistanceMeters:F1} v={s.SpeedRelativeGround:F1} fuel={fuel:F0} throttle={sim.Throttle:F3}");
                if(ticks%50000==0)Console.WriteLine($"t={s.MissionTime:F1} phase={controller.Phase} altitude={s.AltitudeMeters:F0} fuel={fuel:F0} miss={controller.PredictedMissMeters:F0}");
            }
            controller.Update(sim);
            if(!sim.State.IsComplete)throw new Exception("Return did not reach a terminal ground/catch state within 12000 seconds");
            Console.WriteLine($"Return outcome={controller.Phase}, impact={sim.ImpactSpeedMetersPerSecond:F2}m/s, targetDistance={controller.TargetDistanceMeters:F1}m, predictedMiss={controller.PredictedMissMeters:F1}m, fuel={fuel:F1}kg peakHeat={controller.PeakHeatFluxWattsPerSquareMeter:F0}W/m2");
            Console.WriteLine($"Lug miss={controller.CatchLugDistanceMeters:F4}m heightError={controller.CatchLugAltitudeErrorMeters:F4}m speed={controller.CatchLugSpeedMetersPerSecond:F4}m/s arms={controller.CatchArmClosure01:F3}");
            if(sim.IsCaught&&(controller.CatchLugSpeedMetersPerSecond>=3||controller.CatchLugDistanceMeters>=.35||Math.Abs(controller.CatchLugAltitudeErrorMeters)>=.5||controller.CatchArmClosure01<.99))throw new Exception("Catch bypassed physical envelope");
            if(forceDepletion&&(!injected||sim.IsCaught||!sim.State.IsComplete))throw new Exception("Depleted-fuel approach produced a false catch or failed to terminate");
            if(!forceDepletion&&Array.IndexOf(Environment.GetCommandLineArgs(),"--require-catch")>=0&&!sim.IsCaught)throw new Exception("Nominal physical catch acceptance failed");
            Console.WriteLine("Return continuity, finite state, fuel and terminal outcome checks passed; use --require-catch for physical-catch acceptance.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
