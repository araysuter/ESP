using System;
using FlightCore;

// Run on qualified VM: dotnet run --project OrbitProjection.Tests/OrbitProjection.Tests.csproj
static class Program
{
    static void Main()
    {
        double mu = Gravity.EarthGravitationalParameter;
        double radius = 7e6, speed = Math.Sqrt(mu / radius);
        var r = new Vector3d(radius, 0, 0);
        var v = new Vector3d(0, speed, 0);
        var circle = OrbitProjection.Build(r, v, 0);
        Check(circle.IsValid && circle.IsClosed && !circle.HasImpact, "circular closure");
        Check(circle.EcefPoints.Count <= 512, "bounded vertex count");
        foreach (var p in circle.EcefPoints) Near(p.Magnitude(), radius, .001, "circular radius");
        Near((circle.EcefPoints[0] - circle.EcefPoints[circle.EcefPoints.Count - 1]).Magnitude(), 0, .001, "closed endpoint");
        Check(!circle.Apoapsis.IsAvailable && !circle.Periapsis.IsAvailable, "circular apsis ambiguity suppressed");

        // Analytic equatorial ellipse: rp=7 Mm, ra=10 Mm.
        double ra = 1e7, a = (radius + ra) / 2;
        var ellipse = OrbitProjection.Build(r, new Vector3d(0, Math.Sqrt(mu * (2 / radius - 1 / a)), 0), 0);
        Check(ellipse.IsClosed && ellipse.Apoapsis.IsAvailable && ellipse.Periapsis.IsAvailable, "elliptic markers");
        Near(ellipse.Apoapsis.EcefPosition.Magnitude(), ra, .01, "apoapsis radius");
        Near(ellipse.Periapsis.Wgs84AltitudeMeters, radius - EarthFrames.SemiMajorAxisMeters, .01, "WGS84 apsis height");

        // Low-energy horizontal coast intersects Earth before its subterranean periapsis.
        var impact = OrbitProjection.Build(r, new Vector3d(0, speed * .75, 0), 0);
        Check(impact.IsValid && !impact.IsClosed && impact.HasImpact, "suborbital impact");
        Check(!impact.Periapsis.IsAvailable, "hide subterranean periapsis");
        Near(impact.Impact.Wgs84AltitudeMeters, 0, .02, "surface intersection");
        Check(impact.Impact.EcefPosition.Y > 0, "first forward intersection");
        foreach (var p in impact.EcefPoints) Check(EarthFrames.EcefToGeodetic(p).AltitudeMeters > -.02, "no vertices through Earth");

        // An incoming hyperbolic orbit has an impact, while an outbound branch escapes.
        var escape = OrbitProjection.Build(r, new Vector3d(1500, 12000, 1000), .2);
        Check(escape.IsValid && !escape.IsClosed && !escape.HasImpact && !escape.Apoapsis.IsAvailable, "open escape");
        Check(escape.EcefPoints.Count <= 512, "open conic vertex bound");
        var incoming = OrbitProjection.Build(r, new Vector3d(-11000, 2000, 0), 0);
        Check(incoming.IsValid && incoming.HasImpact && !incoming.Apoapsis.IsAvailable, "incoming escape impact");

        // Every vertex uses one current Earth epoch, not each future propagation time.
        const double angle = 1.234;
        var rotated = OrbitProjection.Build(r, v, angle);
        for (int i = 0; i < circle.EcefPoints.Count; i++)
            Near((rotated.EcefPoints[i] - EarthFrames.InertialToEcef(circle.EcefPoints[i], angle)).Magnitude(), 0, .001, "single current frame");
        Near((rotated.EcefPoints[0] - EarthFrames.InertialToEcef(r, angle)).Magnitude(), 0, .001, "vehicle anchor");

        // Oblique suborbital arc clips against the oblate surface, not an equatorial sphere.
        var polarR = new Vector3d(0, 0, radius);
        var polarImpact = OrbitProjection.Build(polarR, new Vector3d(speed * .75, 0, 0), 1.1);
        Check(polarImpact.HasImpact, "polar impact");
        Near(polarImpact.Impact.Wgs84AltitudeMeters, 0, .02, "polar WGS84 intersection");

        // Near-grazing perigee lies between ordinary line vertices; quartic still finds it.
        double rp = EarthFrames.SemiMajorAxisMeters - .1;
        a = (radius + rp) / 2;
        var grazing = OrbitProjection.Build(r, new Vector3d(0, Math.Sqrt(mu * (2 / radius - 1 / a)), 0), 0, 16);
        Check(grazing.HasImpact && !grazing.IsClosed, "sub-meter grazing intersection at low vertex count");
        Near(grazing.Impact.Wgs84AltitudeMeters, 0, .02, "grazing altitude");

        // Surface-start inward/tangent/outward states, including angular rounding at an arbitrary epoch.
        double surfaceRadius = EarthFrames.SemiMajorAxisMeters;
        foreach (double offset in new[] { -.00001, 0.0, .00001 })
        {
            double phi = .7654321;
            var er = new Vector3d(Math.Cos(phi), Math.Sin(phi), 0);
            var et = new Vector3d(-Math.Sin(phi), Math.Cos(phi), 0);
            var surfaceR = er * (surfaceRadius + offset);
            var inward = OrbitProjection.Build(surfaceR, er * -100 + et * 1000, 0);
            Check(inward.HasImpact && !inward.IsClosed, "surface inward contact");
            Near((inward.Impact.EcefPosition - surfaceR).Magnitude(), 0, .02, "surface impact at epoch");
            var tangent = OrbitProjection.Build(surfaceR, et * 1000, 0);
            Check(tangent.HasImpact && !tangent.IsClosed, "surface tangent contact");
            var outward = OrbitProjection.Build(surfaceR, er * 1000 + et * 1000, 0);
            Check(outward.HasImpact && !outward.IsClosed, "surface outward returns");
            Check((outward.Impact.EcefPosition - surfaceR).Magnitude() > 1000, "surface outgoing skips initial root");
            foreach (var point in outward.EcefPoints) Check(EarthFrames.EcefToGeodetic(point).AltitudeMeters > -.02, "outgoing arc above surface");
        }

        Check(!OrbitProjection.Build(r, new Vector3d(1000, 0, 0), 0).IsValid, "radial fallback");
        var nearlyRadial = OrbitProjection.Build(r, new Vector3d(-1000, .00001, 0), 0);
        if (nearlyRadial.IsValid)
            foreach (var point in nearlyRadial.EcefPoints) Check(!double.IsInfinity(point.Magnitude()) && !double.IsNaN(point.Magnitude()), "nearly radial finite");
        Check(!OrbitProjection.Build(Vector3d.Zero, Vector3d.Zero, 0).IsValid, "origin invalid");
        Check(!OrbitProjection.Build(r, v, double.NaN).IsValid, "invalid epoch");
        Check(!OrbitProjection.Build(new Vector3d(1e308, 1e308, 0), v, 0).IsValid, "overflow invalid");
        Check(!OrbitProjection.Build(new Vector3d(6e6, 0, 0), v, 0).IsValid, "below surface invalid");
        Console.WriteLine("PASS orbit projection: circular, ellipse, suborbital/oblate/grazing impact, escape, fixed epoch, invalid states");
    }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    static void Near(double value, double expected, double tolerance, string name) { Check(Math.Abs(value - expected) <= tolerance, name + ": " + value); }
}
