using System;

namespace FlightCore
{
    public readonly struct GeodeticPosition
    {
        public double LatitudeRadians { get; }
        public double LongitudeRadians { get; }
        public double AltitudeMeters { get; }
        public GeodeticPosition(double latitudeRadians, double longitudeRadians, double altitudeMeters)
        { LatitudeRadians = latitudeRadians; LongitudeRadians = longitudeRadians; AltitudeMeters = altitudeMeters; }
    }

    /// <summary>WGS84 ECEF and a simplified mission-time inertial frame, in meters and radians.</summary>
    public static class EarthFrames
    {
        public const double SemiMajorAxisMeters = 6378137.0;
        public const double Flattening = 1.0 / 298.257223563;
        private const double EccentricitySquared = Flattening * (2 - Flattening);
        private const double SemiMinorAxisMeters = SemiMajorAxisMeters * (1 - Flattening);

        public static Vector3d GeodeticToEcef(double latitudeRadians, double longitudeRadians, double altitudeMeters)
        {
            if (!Finite(latitudeRadians) || Math.Abs(latitudeRadians) > Math.PI / 2) throw new ArgumentOutOfRangeException(nameof(latitudeRadians));
            if (!Finite(longitudeRadians)) throw new ArgumentOutOfRangeException(nameof(longitudeRadians));
            // Deep interior coordinates are ambiguous and outside the intended flight domain.
            if (!Finite(altitudeMeters) || altitudeMeters <= -SemiMinorAxisMeters + 1000) throw new ArgumentOutOfRangeException(nameof(altitudeMeters));
            double sin = Math.Sin(latitudeRadians), cos = Math.Cos(latitudeRadians);
            double n = SemiMajorAxisMeters / Math.Sqrt(1 - EccentricitySquared * sin * sin);
            return new Vector3d((n + altitudeMeters) * cos * Math.Cos(longitudeRadians),
                (n + altitudeMeters) * cos * Math.Sin(longitudeRadians),
                (n * (1 - EccentricitySquared) + altitudeMeters) * sin);
        }

        public static GeodeticPosition EcefToGeodetic(Vector3d position)
        {
            double p = Math.Sqrt(position.X * position.X + position.Y * position.Y);
            double radius = Math.Sqrt(p * p + position.Z * position.Z);
            // Inside the ellipsoid evolute geodetic coordinates can be non-unique.
            if (!Finite(radius) || radius < 100000) throw new ArgumentOutOfRangeException(nameof(position));
            if (p < 1e-8)
                return new GeodeticPosition(position.Z >= 0 ? Math.PI / 2 : -Math.PI / 2, 0, Math.Abs(position.Z) - SemiMinorAxisMeters);
            double lon = Math.Atan2(position.Y, position.X);
            double lat = Math.Atan2(position.Z, p * (1 - EccentricitySquared));
            for (int i = 0; i < 20; i++)
            {
                double sin = Math.Sin(lat);
                double n = SemiMajorAxisMeters / Math.Sqrt(1 - EccentricitySquared * sin * sin);
                double next = Math.Atan2(position.Z + EccentricitySquared * n * sin, p);
                if (Math.Abs(next - lat) < 1e-14) { lat = next; break; }
                lat = next;
            }
            double s = Math.Sin(lat), c = Math.Cos(lat);
            double normal = SemiMajorAxisMeters / Math.Sqrt(1 - EccentricitySquared * s * s);
            double altitude = Math.Abs(c) > 0.5 ? p / c - normal : position.Z / s - normal * (1 - EccentricitySquared);
            return new GeodeticPosition(lat, lon, altitude);
        }

        public static Vector3d EcefToInertial(Vector3d vector, double earthAngleRadians) => Rotate(vector, earthAngleRadians);
        public static Vector3d InertialToEcef(Vector3d vector, double earthAngleRadians) => Rotate(vector, -earthAngleRadians);
        private static Vector3d Rotate(Vector3d v, double angle)
        {
            if (!Finite(angle)) throw new ArgumentOutOfRangeException(nameof(angle));
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Vector3d(c * v.X - s * v.Y, s * v.X + c * v.Y, v.Z);
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
