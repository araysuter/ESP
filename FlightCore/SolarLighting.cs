using System;

namespace FlightCore
{
    /// <summary>Offline approximate solar direction; ECEF X=Greenwich, Y=90E, Z=north.</summary>
    public static class SolarLighting
    {
        private const double Rad = Math.PI / 180;
        private static readonly DateTime J2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public static Vector3d DirectionEcef(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Solar time must be UTC.", nameof(utc));
            double days = (utc - J2000).TotalDays;
            // USNO approximate solar coordinates; UTC approximates UT1/TT for this visual model.
            double anomaly = (357.529 + .98560028 * days) % 360 * Rad;
            double longitude = ((280.459 + .98564736 * days) % 360 + 1.915 * Math.Sin(anomaly) + .020 * Math.Sin(2 * anomaly)) * Rad;
            double obliquity = (23.439 - .00000036 * days) * Rad;
            var equatorial = new Vector3d(Math.Cos(longitude), Math.Cos(obliquity) * Math.Sin(longitude), Math.Sin(obliquity) * Math.Sin(longitude));
            double sidereal = CelestialFrames.GreenwichSiderealAngle(utc);
            return EarthFrames.InertialToEcef(equatorial, sidereal).Normalize();
        }

        public static double ElevationDegrees(Vector3d sun, double latitudeRadians, double longitudeRadians)
        {
            var normal = new Vector3d(Math.Cos(latitudeRadians) * Math.Cos(longitudeRadians), Math.Cos(latitudeRadians) * Math.Sin(longitudeRadians), Math.Sin(latitudeRadians));
            double dot = normal.X * sun.X + normal.Y * sun.Y + normal.Z * sun.Z;
            return Math.Asin(Math.Max(-1, Math.Min(1, dot))) / Rad;
        }

        /// <summary>True if the forward ray to the Sun intersects the WGS84 ellipsoid.</summary>
        public static bool IsOccluded(Vector3d positionEcef, Vector3d sun)
        {
            double a = EarthFrames.SemiMajorAxisMeters, b = a * (1 - EarthFrames.Flattening);
            double px = positionEcef.X / a, py = positionEcef.Y / a, pz = positionEcef.Z / b;
            double sx = sun.X / a, sy = sun.Y / a, sz = sun.Z / b;
            double aa = sx * sx + sy * sy + sz * sz;
            if (aa == 0) throw new ArgumentException("Sun direction must be nonzero.", nameof(sun));
            double bb = px * sx + py * sy + pz * sz;
            double cc = px * px + py * py + pz * pz - 1;
            // Surface tolerance avoids roundoff turning a sun-facing pad into an eclipse.
            if (cc <= 1e-12) return bb < 0;
            return bb < 0 && bb * bb - aa * cc >= 0;
        }
    }
}
