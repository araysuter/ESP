using System;
using FlightCore;

internal static class Program
{
    static byte[] ReadCelestialData(string name)
    {
        var directory=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null) {
            var path=System.IO.Path.Combine(directory.FullName,"UnityProject","Assets","ESP","Resources",name);
            if(System.IO.File.Exists(path))return System.IO.File.ReadAllBytes(path);
            directory=directory.Parent;
        }
        throw new System.IO.FileNotFoundException("Cannot find celestial reference data",name);
    }
    static int Main()
    {
        try
        {
            bool forceDepletion=Array.IndexOf(Environment.GetCommandLineArgs(),"--force-depletion")>=0;bool injected=false;
            double step=Array.IndexOf(Environment.GetCommandLineArgs(),"--half-step")>=0?.01:.02;
            double coastSeconds=60,windEast=0,windNorth=0;
            foreach(var arg in Environment.GetCommandLineArgs()) {
                if(arg.StartsWith("--coast="))coastSeconds=double.Parse(arg.Substring(8),System.Globalization.CultureInfo.InvariantCulture);
                if(arg.StartsWith("--east-wind="))windEast=double.Parse(arg.Substring(12),System.Globalization.CultureInfo.InvariantCulture);
                if(arg.StartsWith("--north-wind="))windNorth=double.Parse(arg.Substring(13),System.Globalization.CultureInfo.InvariantCulture);
            }
            LunarEphemeris.ConfigureReferenceData(ReadCelestialData("MoonEphemeris.bytes"),ReadCelestialData("SunEphemeris.bytes"));
            var launchTime=new DateTime(2026,11,14,0,0,0,DateTimeKind.Utc);
            WeatherProfile? weather=null;
            if(windEast!=0||windNorth!=0) {
                var levels=new System.Collections.Generic.List<WeatherLevel>();
                foreach(double altitude in new[]{0d,100,500,1000,2000,5000,10000,20000,40000,60000,80000}) {
                    var standard=StandardAtmosphere.Sample(altitude);double taper=Math.Max(0,1-altitude/20000);
                    levels.Add(new WeatherLevel(altitude,new AtmosphereSample(standard.TemperatureKelvin,standard.PressurePascals,new Vector3d(windEast*taper,windNorth*taper,0))));
                }
                weather=new WeatherProfile(levels,"Synthetic validation crosswind",launchTime,25.997,-97.156);
            }
            var atmosphere=new WeatherAtmosphere(weather,launchTime,TimeSpan.FromHours(3));
            Console.WriteLine($"Fixture step={step} coast={coastSeconds}s windENU=({windEast},{windNorth},0)m/s");
            var sim=new RocketSimulation(step,RealVehiclePresets.BuildStarshipStages(),atmosphere,25.997*Math.PI/180,-97.156*Math.PI/180,launchUtc:launchTime);
            var controller=new StarshipReturnController(25.997*Math.PI/180,-97.156*Math.PI/180,100);
            if(controller.TryStart(sim,out _))throw new Exception("Accepted return from pad");
            var ascent=new AssistedAscent(220000,2);sim.Launch();
            for(int i=0;i<100000&&!ascent.HasCutOff&&!sim.State.IsComplete;i++){ascent.Update(sim);sim.Step();}
            if(!ascent.HasCutOff)throw new Exception("Nominal ascent did not orbit");
            for(int i=0;i<(int)Math.Round(coastSeconds/step);i++)sim.Step();
            Console.WriteLine($"Orbit fixture t={sim.State.MissionTime:F2}s fuel={sim.State.PropellantKg:F1}kg alt={sim.State.AltitudeMeters:F1}m minimumOrbitAltitude={OrbitDiagnostics.Compute(sim.State.Position,sim.State.Velocity).MinimumWgs84AltitudeBoundMeters:F1}m");
            double fuel=sim.State.PropellantKg;var initial=sim.State;
            if(sim.Celestial==null||sim.Primary!=CelestialPrimary.Earth||!sim.Celestial.HasReferenceData(0))throw new Exception("Celestial-enabled Earth/reference fixture missing");
            var cloneMethod=typeof(RocketSimulation).GetMethod("ReturnClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            foreach(var primary in new[]{CelestialPrimary.Moon,CelestialPrimary.Sun}) {
                var guardSim=(RocketSimulation)cloneMethod.Invoke(sim,new object[]{step})!;
                if(guardSim.Celestial==null||(guardSim.Celestial.MoonPosition(0)-sim.Celestial.MoonPosition(0)).Magnitude()>1e-8)throw new Exception("Return clone lost launch ephemeris");
                var guard=new StarshipReturnController(25.997*Math.PI/180,-97.156*Math.PI/180,100);
                if(!guard.TryStart(guardSim,out var guardReason))throw new Exception(guardReason);
                var center=primary==CelestialPrimary.Moon?guardSim.Celestial.MoonPosition(guardSim.State.MissionTime):guardSim.Celestial.SunPosition(guardSim.State.MissionTime);
                var radius=primary==CelestialPrimary.Moon?LunarEphemeris.MoonRadius:LunarEphemeris.SunRadius;
                typeof(RocketSimulation).GetField("position",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(guardSim,center+new Vector3d(radius+1000000,0,0));
                if(guardSim.Primary!=primary)throw new Exception("Non-Earth guard fixture primary mismatch");
                guardSim.Throttle=1;guard.Update(guardSim);
                if(guard.Phase!=StarshipReturnPhase.Failed||guardSim.Throttle!=0||guardSim.IsCaught)throw new Exception("Earth return did not stop outside Earth primary");
                if(new StarshipReturnController(0,0,100).TryStart(guardSim,out _))throw new Exception("Earth return started outside Earth primary");
            }
            var unsupported=new RocketSimulation(step,RealVehiclePresets.BuildSlsStages(),atmosphere,0,0);
            if(new StarshipReturnController(0,0,100).TryStart(unsupported,out _))throw new Exception("Unsupported vehicle accepted");
            // Empty-tank rejection fixture only: the mission below resumes its original measured fuel.
            var tank=typeof(RocketSimulation).GetField("fuel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            tank.SetValue(sim,0d);
            if(new StarshipReturnController(25.997*Math.PI/180,-97.156*Math.PI/180,100).TryStart(sim,out _))throw new Exception("Empty-tank return accepted");
            tank.SetValue(sim,fuel);
            if(!controller.TryStart(sim,out var reason))throw new Exception(reason);
            if((sim.ThrustDirectionInertial-sim.ReturnAttitudeDirection).Magnitude()>1e-12)throw new Exception("Return displayed thrust axis differs from actual return axis");
            if((sim.State.Position-initial.Position).Magnitude()!=0||(sim.State.Velocity-initial.Velocity).Magnitude()!=0)throw new Exception("Return activation changed translational state");
            int ticks=0;bool ignitedWhileTurning=false;double flipStart=double.NaN,flipAltitude=double.NaN,uprightTime=double.NaN;
            Vector3d inboundEcef=default;double ignitionRange=double.NaN, ignitionHorizontalSpeed=0,maxOvershoot=double.NegativeInfinity;
            var catchEcef=EarthFrames.GeodeticToEcef(25.997*Math.PI/180,-97.156*Math.PI/180,100);
            while(!sim.State.IsComplete&&ticks++<1600000)
            {
                controller.Update(sim);
                var beforeStep=sim.State;var axisBefore=sim.ReturnAttitudeDirection;
                double upDot=(axisBefore.X*beforeStep.Position.X+axisBefore.Y*beforeStep.Position.Y+axisBefore.Z*beforeStep.Position.Z)/beforeStep.Position.Magnitude();
                if(controller.Phase==StarshipReturnPhase.LandingFlip&&double.IsNaN(flipStart)){
                    flipStart=beforeStep.MissionTime;flipAltitude=beforeStep.AltitudeMeters;
                    var up=beforeStep.Position.Normalize();
                    var groundVelocity=beforeStep.Velocity-Aerodynamics.CorotatingAirVelocity(beforeStep.Position,Vector3d.Zero);
                    double radial=groundVelocity.X*up.X+groundVelocity.Y*up.Y+groundVelocity.Z*up.Z;
                    var horizontal=groundVelocity-up*radial;ignitionHorizontalSpeed=horizontal.Magnitude();
                    inboundEcef=EarthFrames.InertialToEcef(horizontal.Normalize(),Aerodynamics.EarthAngularVelocityRadiansPerSecond*flipStart);
                    var delta=catchEcef-EarthFrames.InertialToEcef(beforeStep.Position,Aerodynamics.EarthAngularVelocityRadiansPerSecond*flipStart);
                    ignitionRange=delta.X*inboundEcef.X+delta.Y*inboundEcef.Y+delta.Z*inboundEcef.Z;
                }
                if(!double.IsNaN(flipStart)&&double.IsNaN(uprightTime)&&upDot>.99)uprightTime=beforeStep.MissionTime;
                if(!forceDepletion&&controller.Phase==StarshipReturnPhase.LandingFlip&&sim.Throttle>0&&sim.Physics.ThrustNewtons>0&&upDot<Math.Cos(Math.PI/4))ignitedWhileTurning=true;
                if(forceDepletion&&!injected&&controller.Phase==StarshipReturnPhase.LandingFlip) {tank.SetValue(sim,0d);injected=true;Console.WriteLine("Fault fixture: all remaining propellant lost at landing flip; continue ordinary physics.");}
                if(controller.Phase!=StarshipReturnPhase.Planning)sim.Step();
                var s=sim.State;var axisAfter=sim.ReturnAttitudeDirection;
                double axisDot=Math.Max(-1,Math.Min(1,axisBefore.X*axisAfter.X+axisBefore.Y*axisAfter.Y+axisBefore.Z*axisAfter.Z));
                if(Math.Acos(axisDot)>45*Math.PI/180*step+1e-7)throw new Exception("Return attitude exceeded finite 45 degree/s rate limit");
                if(s.PropellantKg>fuel+1e-8||s.PropellantKg<0)throw new Exception("Return created fuel");
                fuel=s.PropellantKg;
                if(!double.IsNaN(flipStart)) {
                    var delta=EarthFrames.InertialToEcef(s.Position,Aerodynamics.EarthAngularVelocityRadiansPerSecond*s.MissionTime)-catchEcef;
                    maxOvershoot=Math.Max(maxOvershoot,delta.X*inboundEcef.X+delta.Y*inboundEcef.Y+delta.Z*inboundEcef.Z);
                }
                if((controller.Phase==StarshipReturnPhase.PoweredLanding||controller.Phase==StarshipReturnPhase.LandingFlip)&&ticks%1000==0)Console.WriteLine($"LAND t={s.MissionTime:F1} alt={s.AltitudeMeters:F1} dist={controller.TargetDistanceMeters:F1} v={s.SpeedRelativeGround:F1} fuel={fuel:F0} throttle={sim.Throttle:F3}");
                if(ticks%50000==0)Console.WriteLine($"t={s.MissionTime:F1} phase={controller.Phase} altitude={s.AltitudeMeters:F0} fuel={fuel:F0} miss={controller.PredictedMissMeters:F0}");
            }
            controller.Update(sim);
            if(!sim.State.IsComplete)throw new Exception("Return did not reach a terminal ground/catch state within 12000 seconds");
            Console.WriteLine($"Return outcome={controller.Phase}, impact={sim.ImpactSpeedMetersPerSecond:F2}m/s, targetDistance={controller.TargetDistanceMeters:F1}m, predictedMiss={controller.PredictedMissMeters:F1}m, fuel={fuel:F1}kg peakHeat={controller.PeakHeatFluxWattsPerSquareMeter:F0}W/m2");
            Console.WriteLine($"Powered flip: ignitionWhileTurning={ignitedWhileTurning} startAltitude={flipAltitude:F2}m tiltBelow8degAfter={uprightTime-flipStart:F3}s");
            if(!forceDepletion&&sim.IsCaught&&(!ignitedWhileTurning||!(uprightTime>flipStart)))throw new Exception("Catch did not originate from finite powered belly flip");
            Console.WriteLine($"APPROACH ignitionRange={ignitionRange:F3}m horizontalSpeed={ignitionHorizontalSpeed:F3}m/s maxOvershoot={maxOvershoot:F3}m duration={sim.State.MissionTime-flipStart:F3}s");
            if(!forceDepletion&&Array.IndexOf(Environment.GetCommandLineArgs(),"--require-catch")>=0) {
                if(double.IsNaN(ignitionRange)||ignitionRange<Math.Max(5,ignitionHorizontalSpeed))
                    throw new Exception("Landing ignition did not precede tower with stopping lead");
                if(maxOvershoot>5)throw new Exception("Powered approach overshot tower by more than 5 m");
            }
            Console.WriteLine($"Lug miss={controller.CatchLugDistanceMeters:F4}m heightError={controller.CatchLugAltitudeErrorMeters:F4}m speed={controller.CatchLugSpeedMetersPerSecond:F4}m/s arms={controller.CatchArmClosure01:F3}");
            if(sim.IsCaught&&(controller.CatchLugSpeedMetersPerSecond>=3||controller.CatchLugDistanceMeters>=.35||Math.Abs(controller.CatchLugAltitudeErrorMeters)>=.5||controller.CatchArmClosure01<.99))throw new Exception("Catch bypassed physical envelope");
            if(forceDepletion&&(!injected||sim.IsCaught||!sim.State.IsComplete))throw new Exception("Depleted-fuel approach produced a false catch or failed to terminate");
            var finalState=sim.State;var finalAxis=sim.ReturnAttitudeDirection;
            Console.WriteLine(FormattableString.Invariant($"RETURN_SIGNATURE step={step:R} coast={coastSeconds:R} windE={windEast:R} windN={windNorth:R} phase={controller.Phase} caught={sim.IsCaught} burnT={controller.BurnStartMissionTimeSeconds:R} burnDv={controller.PlannedDeltaVMetersPerSecond:R} predictedMiss={controller.PredictedMissMeters:R} predictedCatch={controller.PredictedCatchFeasible} t={finalState.MissionTime:R} fuel={finalState.PropellantKg:R} mass={finalState.MassKg:R} px={finalState.Position.X:R} py={finalState.Position.Y:R} pz={finalState.Position.Z:R} vx={finalState.Velocity.X:R} vy={finalState.Velocity.Y:R} vz={finalState.Velocity.Z:R} altitude={finalState.AltitudeMeters:R} groundSpeed={finalState.SpeedRelativeGround:R} q={finalState.DynamicPressurePascals:R} ax={finalAxis.X:R} ay={finalAxis.Y:R} az={finalAxis.Z:R} bank={sim.ReturnBankAngleRadians:R} lugMiss={controller.CatchLugDistanceMeters:R} lugHeight={controller.CatchLugAltitudeErrorMeters:R} lugSpeed={controller.CatchLugSpeedMetersPerSecond:R} arms={controller.CatchArmClosure01:R} peakHeat={controller.PeakHeatFluxWattsPerSquareMeter:R} peakQ={controller.PeakDynamicPressurePascals:R}"));
            if(!forceDepletion&&Array.IndexOf(Environment.GetCommandLineArgs(),"--require-catch")>=0&&!sim.IsCaught)throw new Exception("Nominal physical catch acceptance failed");
            Console.WriteLine("Return continuity, finite state, fuel and terminal outcome checks passed; use --require-catch for physical-catch acceptance.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
