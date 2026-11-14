using System;
using FlightCore;

static class Program
{
    static void Near(double a, double b, double tolerance, string name)
    { if (Math.Abs(a - b) > tolerance || double.IsNaN(a)) throw new Exception($"{name}: {a} != {b}"); }
    static void Main()
    {
        foreach (var c in new[] { (25.997, -97.156, 0.0), (28.608422, -80.604133, 0.0), (0.0, 0.0, 0.0),
            (90.0, 0.0, 0.0), (-90.0, 0.0, 0.0), (30.0, 45.0, -500.0), (45.0, 70.0, 1000000.0) })
        {
            double lat = c.Item1 * Math.PI / 180, lon = c.Item2 * Math.PI / 180;
            Vector3d ecef = EarthFrames.GeodeticToEcef(lat, lon, c.Item3);
            GeodeticPosition result = EarthFrames.EcefToGeodetic(ecef);
            Near(result.LatitudeRadians, lat, 1e-11, "latitude");
            Near(result.LongitudeRadians, lon, 1e-11, "longitude");
            Near(result.AltitudeMeters, c.Item3, 1e-4, "altitude");
            Vector3d inertial = EarthFrames.EcefToInertial(ecef, 1.2);
            Vector3d back = EarthFrames.InertialToEcef(inertial, 1.2);
            Near((back - ecef).Magnitude(), 0, 1e-8, "rotation");
            Vector3d surfaceVelocity = Aerodynamics.CorotatingAirVelocity(inertial, Vector3d.Zero);
            Near(Aerodynamics.Compute(surfaceVelocity, Aerodynamics.CorotatingAirVelocity(inertial, Vector3d.Zero), 1.2, 0.5, 1, 1).AirSpeedMetersPerSecond, 0, 1e-12, "stationary surface airspeed");
        }
        try { EarthFrames.EcefToGeodetic(Vector3d.Zero); throw new Exception("center accepted"); }
        catch (ArgumentOutOfRangeException) { }
        double mu = Gravity.EarthGravitationalParameter, radius = 7000000;
        var circular = OrbitDiagnostics.Compute(new Vector3d(radius, 0, 0), new Vector3d(0, Math.Sqrt(mu / radius), 0));
        if (!circular.IsBound) throw new Exception("circular orbit not bound");
        Near(circular.Eccentricity, 0, 1e-12, "circular eccentricity");
        Near(circular.PerigeeRadiusMeters, radius, 1e-6, "circular perigee");
        Near(circular.ApogeeRadiusMeters, radius, 1e-6, "circular apogee");
        double apo = 10000000, semi = (radius + apo) / 2;
        var elliptical = OrbitDiagnostics.Compute(new Vector3d(radius, 0, 0), new Vector3d(0, Math.Sqrt(mu * (2 / radius - 1 / semi)), 0));
        if (!elliptical.IsBound) throw new Exception("elliptical orbit not bound");
        Near(elliptical.PerigeeRadiusMeters, radius, 1e-6, "elliptical perigee");
        Near(elliptical.ApogeeRadiusMeters, apo, 1e-5, "elliptical apogee");
        Near(elliptical.MinimumWgs84AltitudeBoundMeters, radius - EarthFrames.SemiMajorAxisMeters, 1e-6, "clearance bound");
        var escape = OrbitDiagnostics.Compute(new Vector3d(radius, 0, 0), new Vector3d(0, Math.Sqrt(2 * mu / radius) * 1.01, 0));
        if (escape.IsBound || !double.IsPositiveInfinity(escape.ApogeeRadiusMeters)) throw new Exception("escape classified bound");
        var radial = OrbitDiagnostics.Compute(new Vector3d(radius, 0, 0), Vector3d.Zero);
        if (radial.IsBound || radial.PerigeeRadiusMeters != 0) throw new Exception("radial classified safe");
        Console.WriteLine("Earth frame and orbit diagnostic checks passed.");
    }
}
