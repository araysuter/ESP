using System;
using System.Collections.Generic;

namespace FlightCore
{
    public readonly struct PatchedPoint
    {
        public Vector3d Position { get; }
        public double Time { get; }
        public CelestialPrimary Primary { get; }
        public PatchedPoint(Vector3d position, double time, CelestialPrimary primary) { Position=position; Time=time; Primary=primary; }
    }
    /// <summary>Finite-horizon, gravity-only prediction. These are numerical samples of
    /// the same moving-primary patched force model as flight, not maneuver planning.</summary>
    public static class PatchedConicProjection
    {
        public static IReadOnlyList<PatchedPoint> Build(LunarEphemeris ephemeris, Vector3d position, Vector3d velocity,
            double time, double gravityMultiplier = 1, int maxSamples = 2048)
        {
            if(ephemeris == null) throw new ArgumentNullException(nameof(ephemeris));
            var points = new List<PatchedPoint>();
            maxSamples = Math.Max(2,Math.Min(maxSamples,4096));
            double start=time;
            for(int i=0;i<maxSamples;i++)
            {
                var primary=ephemeris.Primary(position,time);
                points.Add(new PatchedPoint(position,time,primary));
                Vector3d center=ephemeris.Position(primary,time);
                Vector3d centerV=ephemeris.Velocity(primary,time);
                double r=(position-center).Magnitude(), speed=(velocity-centerV).Magnitude();
                double radius=LunarEphemeris.Radius(primary);
                if(r<=radius || time-start>365.25*86400) break;
                // Resolve rapid turns and keep a vessel from crossing an entire lunar
                // SOI or body in a single step. A prediction remains an estimate.
                double h=Math.Min(primary==CelestialPrimary.Sun ? 21600 : 3600, Math.Max(1,.025*r/Math.Max(1,speed)));
                h=Math.Min(h,LunarEphemeris.MoonSoi/(4*Math.Max(1,speed)));
                // Refine near either moving SOI and near the active surface so
                // displayed encounter/impact locations are not one coarse step away.
                var moonPosition=ephemeris.MoonPosition(time);
                var moonVelocity=ephemeris.MoonVelocity(time);
                double moonBoundary=Math.Abs((position-moonPosition).Magnitude()-LunarEphemeris.MoonSoi);
                double earthBoundary=Math.Abs(position.Magnitude()-LunarEphemeris.EarthSoi);
                h=Math.Min(h,Math.Max(1,.25*moonBoundary/Math.Max(1,(velocity-moonVelocity).Magnitude())));
                h=Math.Min(h,Math.Max(1,.25*earthBoundary/Math.Max(1,velocity.Magnitude())));
                h=Math.Min(h,Math.Max(1,.25*(r-radius)/Math.Max(1,speed)));
                // A solar step can otherwise cross a small planetary SOI without
                // sampling it at any RK stage. Use each moving body's relative
                // speed for both its SOI and surface approach.
                // Real planets cannot intersect the Earth/Moon neighborhood.
                if (primary != CelestialPrimary.Earth && primary != CelestialPrimary.Moon)
                foreach (var body in LunarEphemeris.InnerPlanets)
                {
                    if (!ephemeris.TryPlanet(body,time,out var p,out var v,out _)) continue;
                    double distance=(position-p).Magnitude();
                    double relativeSpeed=Math.Max(1,(velocity-v).Magnitude());
                    double boundary=Math.Abs(distance-ephemeris.Soi(body,time));
                    h=Math.Min(h,Math.Max(1,.25*boundary/relativeSpeed));
                    h=Math.Min(h,Math.Max(1,.25*Math.Max(0,distance-LunarEphemeris.Radius(body))/relativeSpeed));
                }
                var k1p=velocity; var k1v=ephemeris.Acceleration(position,time,gravityMultiplier);
                var k2p=velocity+k1v*(h*.5); var k2v=ephemeris.Acceleration(position+k1p*(h*.5),time+h*.5,gravityMultiplier);
                var k3p=velocity+k2v*(h*.5); var k3v=ephemeris.Acceleration(position+k2p*(h*.5),time+h*.5,gravityMultiplier);
                var k4p=velocity+k3v*h; var k4v=ephemeris.Acceleration(position+k3p*h,time+h,gravityMultiplier);
                position=position+(k1p+k2p*2+k3p*2+k4p)*(h/6);
                velocity=velocity+(k1v+k2v*2+k3v*2+k4v)*(h/6);
                time+=h;
            }
            return points.AsReadOnly();
        }
    }
}
