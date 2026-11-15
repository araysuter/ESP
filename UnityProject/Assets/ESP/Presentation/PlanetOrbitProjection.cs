using System;
using FlightCore;

namespace ESP.Presentation
{
    /// <summary>Display-only instantaneous heliocentric ellipse, not a future ephemeris.
    /// Input and output use the same inertial frame in SI units.</summary>
    public static class PlanetOrbitProjection
    {
        public static Vector3d[] Build(Vector3d position, Vector3d velocity, int segments = 256)
        {
            if (segments < 16 || segments > 512) throw new ArgumentOutOfRangeException(nameof(segments));
            double radius = position.Magnitude(), speed = velocity.Magnitude();
            if (radius <= 0) return Array.Empty<Vector3d>();
            var momentum = Cross(position, velocity);
            double h = momentum.Magnitude();
            if (h <= radius * Math.Max(1, speed) * 1e-10) return Array.Empty<Vector3d>();
            double inverseAxis = 2 / radius - speed * speed / LunarEphemeris.SunMu;
            if (inverseAxis <= 0) return Array.Empty<Vector3d>();
            var eccentricity = Cross(velocity, momentum) * (1 / LunarEphemeris.SunMu) - position * (1 / radius);
            double e = eccentricity.Magnitude();
            if (e >= 1) return Array.Empty<Vector3d>();
            double a = 1 / inverseAxis;
            var x = e > 1e-10 ? eccentricity * (1 / e) : position * (1 / radius);
            var y = Cross(momentum * (1 / h), x);
            double minorAxis = a * Math.Sqrt(1 - e * e);
            // Start at the current body, so the line always meets its marker.
            double start = Math.Atan2(Dot(position, y) / minorAxis, Dot(position, x) / a + e);
            var points = new Vector3d[segments + 1];
            points[0] = position;
            for (int i = 1; i < segments; i++)
            {
                double anomaly = start + 2 * Math.PI * i / segments;
                points[i] = x * (a * (Math.Cos(anomaly) - e)) + y * (minorAxis * Math.Sin(anomaly));
            }
            points[segments] = points[0];
            return points;
        }

        private static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(
            a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
