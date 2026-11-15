using System;
using System.Reflection;
using FlightCore;

public static class LunarChecks
{
    public static void Run(byte[] moonBytes, byte[] sunBytes)
    {
        DateTime epoch = new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc);
        var e = new LunarEphemeris(epoch);
        // NASA Horizons, 301 relative to 399, ICRF, geometric vectors, 2026-11-15 00:00 UTC.
        var jpl = CelestialFrames.J2000ToEcef(new Vector3d(170843719.7752895,-329222560.3547136,-162008713.4621705),epoch);
        var jplV = CelestialFrames.J2000ToEcef(new Vector3d(868.4226910680329,355.1891394568817,234.8573585333419),epoch);
        double error = (e.MoonPosition(0)-jpl).Magnitude(), velocityError=(e.MoonVelocity(0)-jplV).Magnitude();
        Console.WriteLine("JPL lunar reference position error meters: " + error + "; velocity error m/s: " + velocityError);
        if(error > 500000 || velocityError > 5) throw new Exception("Fallback lunar ephemeris differs excessively from JPL reference");
        LunarEphemeris.ConfigureReferenceData(moonBytes,sunBytes);
        if(!e.HasReferenceData(0)) throw new Exception("Reference ephemeris not loaded");
        if((e.MoonPosition(0)-jpl).Magnitude()>0.01 || (e.MoonVelocity(0)-jplV).Magnitude()>.0001) throw new Exception("JPL sample mismatch");
        var midpoint = CelestialFrames.J2000ToEcef(new Vector3d(180159702.7118007,-325270247.5968922,-159415059.7759267),epoch);
        var midpointV = CelestialFrames.J2000ToEcef(new Vector3d(856.6551133929983,376.6832931623298,245.4232638943290),epoch);
        double midpointError=(e.MoonPosition(10800)-midpoint).Magnitude();
        double midpointVelocityError=(e.MoonVelocity(10800)-midpointV).Magnitude();
        Console.WriteLine("JPL midpoint interpolation error meters: "+midpointError+"; velocity error m/s: "+midpointVelocityError);
        if(midpointError>1000 || midpointVelocityError>.1) throw new Exception("JPL interpolation error");
        var sunReference=CelestialFrames.J2000ToEcef(new Vector3d(-89547574424.51565,-108094391871.1102,-46856307198.81428),epoch);
        var sunVelocityReference=CelestialFrames.J2000ToEcef(new Vector3d(24209.17099224033,-16430.95143975751,-7121.563095383436),epoch);
        double sunError=(e.SunPosition(43200)-sunReference).Magnitude();
        double sunVelocityError=(e.SunVelocity(43200)-sunVelocityReference).Magnitude();
        Console.WriteLine("JPL solar midpoint interpolation error meters: "+sunError+"; velocity error m/s: "+sunVelocityError);
        if(sunError>1000 || sunVelocityError>.1) throw new Exception("Sun interpolation error");
        var end=new LunarEphemeris(new DateTime(2037,1,1,0,0,0,DateTimeKind.Utc));
        if((end.MoonPosition(.01)-end.MoonPosition(-.01)).Magnitude()>100) throw new Exception("Ephemeris boundary position discontinuity");
        if((end.MoonVelocity(.01)-end.MoonVelocity(-.01)).Magnitude()>1) throw new Exception("Ephemeris boundary velocity discontinuity");
        var moon=e.MoonPosition(0); var moonV=e.MoonVelocity(0);
        if(e.Primary(new Vector3d(7000000,0,0),0)!=CelestialPrimary.Earth) throw new Exception("Earth SOI classification");
        if(e.Primary(moon+new Vector3d(2000000,0,0),0)!=CelestialPrimary.Moon) throw new Exception("Moon SOI classification");
        if(e.Primary(new Vector3d(1000000000,0,0),0)!=CelestialPrimary.Sun) throw new Exception("Sun SOI classification");
        var atmosphere=new WeatherAtmosphere(null,epoch,TimeSpan.FromHours(1));
        var rocket=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        Set(rocket,"position",moon+new Vector3d(LunarEphemeris.MoonRadius+10,0,0));
        Set(rocket,"velocity",moonV+new Vector3d(-1000,0,0));
        Set(rocket,"commanded",true); Set(rocket,"launched",true);
        rocket.Step();
        if(!rocket.State.IsLanded || !rocket.State.IsDestroyed || !rocket.State.Status.Contains("Moon")) throw new Exception("Moon impact not detected");
        if(Math.Abs(rocket.ImpactSpeedMetersPerSecond-1000)>2) throw new Exception("Moon impact velocity");
        // Compare identical coast and powered states so gravity and the moving
        // primary cancel, exposing the actual thrust direction after one step.
        var localUp=new Vector3d(0,1,0);
        var coast=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        var powered=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        foreach(var craft in new[] { coast, powered }) {
            Set(craft,"position",moon+localUp*2000000); Set(craft,"velocity",moonV);
            Set(craft,"commanded",true); Set(craft,"launched",true);
        }
        powered.Throttle=1; coast.Step(); powered.Step();
        var thrustDelta=powered.State.Velocity-coast.State.Velocity;
        if(thrustDelta.Magnitude()<.01 || (thrustDelta.Normalize()-localUp).Magnitude()>.001)
            throw new Exception("Moon thrust does not follow local radial up");
        if((powered.ThrustDirectionInertial-thrustDelta.Normalize()).Magnitude()>.001)
            throw new Exception("Displayed thrust axis differs from simulated thrust");
        var sun=e.SunPosition(0); var sunV=e.SunVelocity(0);
        var sunRocket=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        Set(sunRocket,"position",sun+new Vector3d(LunarEphemeris.SunRadius+10,0,0));
        Set(sunRocket,"velocity",sunV+new Vector3d(-1000,0,0));
        Set(sunRocket,"commanded",true); Set(sunRocket,"launched",true); sunRocket.Step();
        if(!sunRocket.State.IsLanded || !sunRocket.State.Status.Contains("Sun")) throw new Exception("Sun impact not detected");
        var boundaryRocket=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        var initialVelocity=moonV+new Vector3d(1000,0,0);
        Set(boundaryRocket,"position",moon+new Vector3d(LunarEphemeris.MoonSoi-10,0,0));
        Set(boundaryRocket,"velocity",initialVelocity); Set(boundaryRocket,"commanded",true); Set(boundaryRocket,"launched",true);
        if(boundaryRocket.Primary!=CelestialPrimary.Moon) throw new Exception("Expected Moon before boundary");
        boundaryRocket.Step();
        if(boundaryRocket.Primary!=CelestialPrimary.Earth) throw new Exception("Expected Earth after lunar escape");
        if((boundaryRocket.State.Velocity-initialVelocity).Magnitude()>.1) throw new Exception("Lunar SOI velocity discontinuity");
        var solarBoundary=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,0,0,0,null,epoch);
        var solarStartVelocity=new Vector3d(1000,0,0);
        Set(solarBoundary,"position",new Vector3d(LunarEphemeris.EarthSoi-10,0,0));
        Set(solarBoundary,"velocity",solarStartVelocity); Set(solarBoundary,"commanded",true); Set(solarBoundary,"launched",true);
        solarBoundary.Step();
        if(solarBoundary.Primary!=CelestialPrimary.Sun || (solarBoundary.State.Velocity-solarStartVelocity).Magnitude()>.1)
            throw new Exception("Earth SOI velocity continuity");
        var steering=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,.5,-1,0,null,epoch);
        steering.PitchDegrees=-45;
        if(steering.PitchDegrees!=-45) throw new Exception("Manual negative pitch rejected");
        var legacy=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,.5,-1);
        var celestial=new RocketSimulation(.02,StageDefinition.DemoStages(),atmosphere,.5,-1,0,null,epoch);
        legacy.Throttle=celestial.Throttle=1; legacy.Launch(); celestial.Launch();
        for(int n=0;n<1500;n++) { legacy.Step(); celestial.Step(); }
        if((legacy.State.Position-celestial.State.Position).Magnitude()>.001 ||
           (legacy.State.Velocity-celestial.State.Velocity).Magnitude()>.0001)
            throw new Exception("Earth launch changed with celestial model enabled");
        var lunarPrediction=PatchedConicProjection.Build(e,moon+new Vector3d(LunarEphemeris.MoonSoi+10000,0,0),moonV+new Vector3d(-1000,0,0),0,1,128);
        bool foundMoon=false;
        foreach(var point in lunarPrediction) if(point.Primary==CelestialPrimary.Moon) { foundMoon=true; break; }
        if(!foundMoon) throw new Exception("No projected lunar encounter");
        var p=new Vector3d(LunarEphemeris.EarthSoi-2000,0,0);
        var projection=PatchedConicProjection.Build(e,p,new Vector3d(3000,0,0),0,1,32);
        bool foundSun=false;
        foreach(var point in projection) if(point.Primary==CelestialPrimary.Sun) foundSun=true;
        if(!foundSun) throw new Exception("No projected Earth-to-Sun transition");
        Console.WriteLine("Lunar and solar checks passed.");
    }
    static void Set(RocketSimulation rocket,string name,object value)
    {
        typeof(RocketSimulation).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rocket,value);
    }
}
