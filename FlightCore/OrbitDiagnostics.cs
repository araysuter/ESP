using System;

namespace FlightCore
{
    /// <summary>Instantaneous point-mass osculating elements, not a mission-success decision.</summary>
    public readonly struct OrbitElements
    {
        public bool IsBound { get; }
        public double PerigeeRadiusMeters { get; }
        public double ApogeeRadiusMeters { get; }
        /// <summary>Conservative lower bound, not exact geodetic minimum altitude.</summary>
        public double MinimumWgs84AltitudeBoundMeters { get; }
        public double SpecificEnergyJoulesPerKg { get; }
        public double Eccentricity { get; }
        public double PeriodSeconds { get; }
        internal OrbitElements(bool bound, double perigee, double apogee, double energy, double eccentricity, double period)
        {
            IsBound = bound; PerigeeRadiusMeters = perigee; ApogeeRadiusMeters = apogee;
            MinimumWgs84AltitudeBoundMeters = perigee - EarthFrames.SemiMajorAxisMeters;
            SpecificEnergyJoulesPerKg = energy; Eccentricity = eccentricity; PeriodSeconds = period;
        }
    }

    public static class OrbitDiagnostics
    {
        /// <summary>State is Earth-centered inertial meters and meters per second; uses point-mass Earth gravity.</summary>
        public static OrbitElements Compute(Vector3d positionMeters, Vector3d velocityMetersPerSecond)
        {
            double r = positionMeters.Magnitude(), v = velocityMetersPerSecond.Magnitude();
            if (!Finite(r) || r <= 0 || !Finite(v)) throw new ArgumentOutOfRangeException(nameof(positionMeters), "State must be finite and position nonzero.");
            double mu = Gravity.EarthGravitationalParameter;
            double hx = positionMeters.Y * velocityMetersPerSecond.Z - positionMeters.Z * velocityMetersPerSecond.Y;
            double hy = positionMeters.Z * velocityMetersPerSecond.X - positionMeters.X * velocityMetersPerSecond.Z;
            double hz = positionMeters.X * velocityMetersPerSecond.Y - positionMeters.Y * velocityMetersPerSecond.X;
            double h2 = hx * hx + hy * hy + hz * hz;
            double energy = v * v / 2 - mu / r;
            if (!Finite(h2) || !Finite(energy)) throw new ArgumentOutOfRangeException(nameof(velocityMetersPerSecond), "State exceeds numerical range.");
            // Eccentricity vector remains stable for near-circular orbits.
            double ex = (velocityMetersPerSecond.Y * hz - velocityMetersPerSecond.Z * hy) / mu - positionMeters.X / r;
            double ey = (velocityMetersPerSecond.Z * hx - velocityMetersPerSecond.X * hz) / mu - positionMeters.Y / r;
            double ez = (velocityMetersPerSecond.X * hy - velocityMetersPerSecond.Y * hx) / mu - positionMeters.Z / r;
            double eccentricity = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (!Finite(eccentricity)) throw new ArgumentOutOfRangeException(nameof(velocityMetersPerSecond));
            double perigee = h2 / (mu * (1 + eccentricity));
            // A radial trajectory intersects the center and is not a valid bound orbit.
            bool bound = energy < 0 && h2 > 0 && eccentricity < 1;
            if (!bound) return new OrbitElements(false, perigee, double.PositiveInfinity, energy, eccentricity, double.PositiveInfinity);
            double a = -mu / (2 * energy);
            double apogee = a * (1 + eccentricity);
            double period = 2 * Math.PI * Math.Sqrt(a * a * a / mu);
            if (!Finite(apogee) || !Finite(period)) throw new ArgumentOutOfRangeException(nameof(velocityMetersPerSecond), "Orbit exceeds numerical range.");
            return new OrbitElements(true, perigee, apogee, energy, eccentricity, period);
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
