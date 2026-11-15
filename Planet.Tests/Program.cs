using System;
using System.IO;
using System.Reflection;
using System.Text;
using FlightCore;

static class Program
{
    static readonly DateTime Epoch = new DateTime(2020,1,1,0,0,0,DateTimeKind.Utc);
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name); }
    static void Near(double actual,double expected,double tolerance,string name) => Check(!double.IsNaN(actual) && Math.Abs(actual-expected)<=tolerance,$"{name}: {actual} != {expected}");
    static byte[] Table(Vector3d p,Vector3d v,Vector3d a=default)
    {
        using var stream=new MemoryStream();
        using(var writer=new BinaryWriter(stream,Encoding.ASCII,true))
        {
            writer.Write(Encoding.ASCII.GetBytes("ESP1"));writer.Write(2020);writer.Write(24);writer.Write(3);
            for(int i=0;i<3;i++)
            {
                double t=i*86400.0;var q=p+v*t+a*(t*t*.5);var w=v+a*t;
                foreach(double x in new[]{q.X,q.Y,q.Z,w.X,w.Y,w.Z})writer.Write(x);
            }
        }
        return stream.ToArray();
    }
    static void Set(object target,string field,object value) => typeof(RocketSimulation).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(target,value);
    static double Dot(Vector3d a,Vector3d b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    static Vector3d Cross(Vector3d a,Vector3d b) => new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    static void OrbitDisplayChecks()
    {
        const double radius=4.5e12; // Neptune scale, beyond the Earth projection's range.
        var p=new Vector3d(radius,0,0);
        var circular=new Vector3d(0,Math.Sqrt(LunarEphemeris.SunMu/radius),0);
        var circle=ESP.Presentation.PlanetOrbitProjection.Build(p,circular);
        foreach(var point in circle) Near(point.Magnitude(),radius,.01,"large circular orbit radius");
        Check(circle[64].Y>radius*.99 && circle[128].X < -radius*.99 && circle[192].Y < -radius*.99,"all four orbit quadrants");
        Near((circle[0]-circle[256]).Magnitude(),0,0,"exact circular closure");
        // Known inclined eccentric ellipse, sampled at perihelion.
        const double e=.6;
        var v=new Vector3d(0,Math.Sqrt(LunarEphemeris.SunMu*(1+e)/radius)/Math.Sqrt(2),Math.Sqrt(LunarEphemeris.SunMu*(1+e)/radius)/Math.Sqrt(2));
        var ellipse=ESP.Presentation.PlanetOrbitProjection.Build(p,v);
        Near(ellipse[128].Magnitude(),radius*(1+e)/(1-e),.1,"eccentric aphelion");
        Check(ESP.Presentation.PlanetOrbitProjection.Build(p,circular*2).Length==0,"unbound state rejected");
        Check(ESP.Presentation.PlanetOrbitProjection.Build(p,p*1e-9).Length==0,"radial state rejected");
        Check(ESP.Presentation.PlanetOrbitProjection.Build(Vector3d.Zero,circular).Length==0,"zero radius rejected");
    }
    static void Main(string[] args)
    {
        OrbitDisplayChecks();
        var ep=new LunarEphemeris(Epoch);
        var p=new Vector3d(1e11,2e10,3e9);var v=new Vector3d(10,-20,30);var a=new Vector3d(.001,-.002,.003);
        LunarEphemeris.ConfigurePlanet(CelestialPrimary.Mercury,Table(p,v,a));
        const double t=43200;
        Check(ep.TryPlanet(CelestialPrimary.Mercury,t,out var q,out var w,out var acc),"interior available");
        Near((q-CelestialFrames.J2000ToEcef(p+v*t+a*(t*t*.5),Epoch)).Magnitude(),0,.001,"quadratic position");
        Near((w-CelestialFrames.J2000ToEcef(v+a*t,Epoch)).Magnitude(),0,1e-7,"quadratic velocity");
        Near((acc-CelestialFrames.J2000ToEcef(a,Epoch)).Magnitude(),0,1e-10,"quadratic acceleration");
        Check(ep.TryPlanet(CelestialPrimary.Mercury,0,out _,out _,out _) && ep.TryPlanet(CelestialPrimary.Mercury,172800,out _,out _,out _),"inclusive endpoints");
        Check(!ep.TryPlanet(CelestialPrimary.Mercury,-.01,out _,out _,out _) && !ep.TryPlanet(CelestialPrimary.Mercury,172800.01,out _,out _,out _),"outside dates rejected");
        try { ep.TryPlanet(CelestialPrimary.Mercury,double.NaN,out _,out _,out _);throw new Exception("NaN time accepted"); }catch(ArgumentOutOfRangeException){}
        try { LunarEphemeris.ConfigurePlanet(CelestialPrimary.Earth,Table(p,v));throw new Exception("Earth planet override accepted"); }catch(ArgumentOutOfRangeException){}
        Near(ep.Soi(CelestialPrimary.Moon,0),LunarEphemeris.MoonSoi,0,"Moon SOI preserved");
        Near(ep.Soi(CelestialPrimary.Earth,0),LunarEphemeris.EarthSoi,0,"Earth SOI preserved");
        Check(ep.Primary(new Vector3d(7000000,0,0),0)==CelestialPrimary.Earth,"Earth primary preserved");
        Check(ep.Primary(ep.MoonPosition(0),0)==CelestialPrimary.Moon,"Moon primary preserved");
        double soi=ep.Soi(CelestialPrimary.Mercury,t);
        Near(soi,(q-ep.SunPosition(t)).Magnitude()*Math.Pow(LunarEphemeris.Mu(CelestialPrimary.Mercury)/LunarEphemeris.SunMu,.4),.001,"heliocentric SOI distance");
        Check(ep.Primary(q,t)==CelestialPrimary.Mercury,"planet primary");
        var radial=new Vector3d(1e7,0,0);
        Near((ep.Acceleration(q+radial,t)-acc-radial*(-LunarEphemeris.Mu(CelestialPrimary.Mercury)/1e21)).Magnitude(),0,1e-9,"moving primary acceleration");

        // Trial integrator stages can hit the center before swept collision
        // truncates the step. Check finite interior gravity at normal gravity,
        // its linear uniform-sphere continuation and surface continuity.
        double radius=LunarEphemeris.Radius(CelestialPrimary.Mercury), mu=LunarEphemeris.Mu(CelestialPrimary.Mercury);
        Near((ep.Acceleration(q,t)-acc).Magnitude(),0,1e-10,"finite planet center gravity");
        var halfRadius=new Vector3d(radius*.5,0,0);
        Near((ep.Acceleration(q+halfRadius,t)-acc-halfRadius*(-mu/(radius*radius*radius))).Magnitude(),0,1e-9,"interior trial gravity");
        var surface=new Vector3d(radius,0,0);
        Near((ep.Acceleration(q+surface,t)-acc-surface*(-mu/(radius*radius*radius))).Magnitude(),0,1e-9,"surface gravity continuity");

        // A rapidly moving body overtakes a nearly stationary vessel. A solar
        // step chosen using only vessel speed used to skip the whole encounter.
        p=new Vector3d(1e11,0,0);v=new Vector3d(1e6,0,0);
        LunarEphemeris.ConfigurePlanet(CelestialPrimary.Mercury,Table(p,v));
        var vessel=CelestialFrames.J2000ToEcef(p+v*1000,Epoch);
        var path=PatchedConicProjection.Build(ep,vessel,Vector3d.Zero,0,0,2048);
        bool encounter=false;
        foreach(var point in path)if(point.Primary==CelestialPrimary.Mercury)encounter=true;
        Check(encounter,"moving planet encounter not skipped");
        var last=path[path.Count-1];
        Check((last.Position-ep.Position(CelestialPrimary.Mercury,last.Time)).Magnitude()<=LunarEphemeris.Radius(CelestialPrimary.Mercury),"projection stops at planet surface");

        // Exercise flight's swept-body impact, including a complete pass through
        // the sphere within one 10-second step (both endpoints outside).
        vessel=CelestialFrames.J2000ToEcef(p+v*5,Epoch);
        var atmosphere=new WeatherAtmosphere(null,Epoch,TimeSpan.FromHours(3));
        var rocket=new RocketSimulation(10,new[]{new StageDefinition(1000,0,0,0,200,200,0,0)},atmosphere,0,0,0,null,Epoch);
        Set(rocket,"position",vessel);Set(rocket,"velocity",Vector3d.Zero);Set(rocket,"launched",true);
        rocket.GravityMultiplier=0;rocket.Launch();rocket.Step();
        Check(rocket.State.IsLanded && rocket.IsDestroyed,"swept planetary collision");
        Near(rocket.ImpactSpeedMetersPerSecond,1e6,.01,"planet-relative impact speed");
        Near(rocket.State.MissionTime,5-LunarEphemeris.Radius(CelestialPrimary.Mercury)/1e6,1e-5,"impact time");

        // Pass the repository root explicitly when running from another directory.
        string root=args.Length>0?args[0]:Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
        LunarEphemeris.ConfigureReferenceData(File.ReadAllBytes(Path.Combine(root,"UnityProject/Assets/ESP/Resources/MoonEphemeris.bytes")),File.ReadAllBytes(Path.Combine(root,"UnityProject/Assets/ESP/Resources/SunEphemeris.bytes")));
        foreach(var body in LunarEphemeris.InnerPlanets)
        {
            string file=Path.Combine(root,"UnityProject/Assets/ESP/Resources/SolarSystem",body+"Ephemeris.bytes");
            LunarEphemeris.ConfigurePlanet(body,File.ReadAllBytes(file));
            var launch=new LunarEphemeris(new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc));
            Check(launch.TryPlanet(body,0,out var bp,out var bv,out _),body+" bundled current date");
            var solarPosition = bp - launch.SunPosition(0);
            var solarVelocity = bv - launch.SunVelocity(0);
            var ellipse = ESP.Presentation.PlanetOrbitProjection.Build(solarPosition, solarVelocity);
            Check(ellipse.Length == 257, body + " full orbit vertex count");
            Near((ellipse[0] - solarPosition).Magnitude(), 0, 0, body + " current body anchor");
            Near((ellipse[0] - ellipse[256]).Magnitude(), 0, 0, body + " closed seam");
            var normal = Cross(solarPosition, solarVelocity).Normalize();
            double conicP = Math.Pow(Cross(solarPosition, solarVelocity).Magnitude(), 2) / LunarEphemeris.SunMu;
            var eccentricity = Cross(solarVelocity, Cross(solarPosition, solarVelocity)) * (1 / LunarEphemeris.SunMu) - solarPosition.Normalize();
            foreach (var vertex in ellipse)
            {
                Check(vertex.Magnitude() > 1e9, body + " no origin spikes");
                Near(Dot(vertex, normal), 0, .1, body + " orbital plane");
                Near(vertex.Magnitude() + Dot(eccentricity, vertex), conicP, 1, body + " conic geometry");
            }
            Console.WriteLine(body + " complete display ellipse checked");
            Check(bp.Magnitude()>1e9 && bv.Magnitude()>0,body+" SI-scale state");
            Check(launch.Primary(bp,0)==body,body+" bundled primary");
            var outward=(bp-launch.SunPosition(0)).Normalize();
            double boundary=launch.Soi(body,0);
            Check(launch.Primary(bp+outward*(boundary*.99),0)==body,body+" SOI inside");
            Check(launch.Primary(bp+outward*(boundary*1.01),0)==CelestialPrimary.Sun,body+" SOI outside");
            Check(launch.Primary(bp+outward*(boundary*.99),0)==body,body+" SOI reentry");
            Check(!new LunarEphemeris(new DateTime(2038,1,1,0,0,0,DateTimeKind.Utc)).TryPlanet(body,0,out _,out _,out _),body+" expired date unavailable");
        }
        Console.WriteLine("PASS planetary interpolation, dates, SOIs, moving encounters, swept impacts and bundled tables");
    }
}
