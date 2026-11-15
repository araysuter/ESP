using System;

namespace FlightCore
{
    /// <summary>Visual J2000 mean-equatorial to Earth-fixed rotation. IAU 1976 precession,
    /// GMST with UTC approximating UT1; excludes nutation, aberration and polar motion.</summary>
    public static class CelestialFrames
    {
        private static readonly DateTime Epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public static double GreenwichSiderealAngle(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Celestial time must be UTC.", nameof(utc));
            double days = (utc - Epoch).TotalDays, t = days / 36525;
            return (280.46061837 + 360.98564736629 * days + .000387933 * t * t - t * t * t / 38710000) % 360 * Math.PI / 180;
        }

        public static Vector3d J2000ToEcef(Vector3d direction, DateTime utc)
        {
            double sidereal = GreenwichSiderealAngle(utc);
            double t = (utc - Epoch).TotalDays / 36525;
            const double arcsecond = Math.PI / (180 * 3600);
            double zeta = (2306.2181 * t + .30188 * t * t + .017998 * t * t * t) * arcsecond;
            double z = (2306.2181 * t + 1.09468 * t * t + .018203 * t * t * t) * arcsecond;
            double theta = (2004.3109 * t - .42665 * t * t - .041833 * t * t * t) * arcsecond;
            Vector3d a = RotateZ(direction, zeta);
            var b = new Vector3d(Math.Cos(theta) * a.X - Math.Sin(theta) * a.Z, a.Y,
                Math.Sin(theta) * a.X + Math.Cos(theta) * a.Z);
            return RotateZ(b, z - sidereal);
        }

        private static Vector3d RotateZ(Vector3d v, double angle) => new Vector3d(
            Math.Cos(angle) * v.X - Math.Sin(angle) * v.Y,
            Math.Sin(angle) * v.X + Math.Cos(angle) * v.Y, v.Z);
    }
}
