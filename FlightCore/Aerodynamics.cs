using System;

namespace FlightCore
{
    public readonly struct DragResult
    {
        public Vector3d ForceNewtons { get; }
        public Vector3d AccelerationMetersPerSecondSquared { get; }
        public double DynamicPressurePascals { get; }
        public double AirSpeedMetersPerSecond { get; }
        internal DragResult(Vector3d force, Vector3d acceleration, double q, double speed)
        { ForceNewtons = force; AccelerationMetersPerSecondSquared = acceleration; DynamicPressurePascals = q; AirSpeedMetersPerSecond = speed; }
    }

    /// <summary>Drag-only force model. All velocity arguments must share the same Cartesian frame.</summary>
    public static class Aerodynamics
    {
        // WGS84 conventional angular velocity; simplified rotation about inertial +Z.
        public const double EarthAngularVelocityRadiansPerSecond = 7.292115e-5;

        /// <summary>NASA Glenn drag equation: F = -0.5 rho Cd A |v_rel| v_rel.</summary>
        public static DragResult Compute(Vector3d vehicleVelocity, Vector3d airVelocity,
            double densityKgPerCubicMeter, double dragCoefficient, double referenceAreaSquareMeters, double massKilograms)
        {
            Nonnegative(densityKgPerCubicMeter, nameof(densityKgPerCubicMeter));
            Nonnegative(dragCoefficient, nameof(dragCoefficient));
            Nonnegative(referenceAreaSquareMeters, nameof(referenceAreaSquareMeters));
            if (!Finite(massKilograms) || massKilograms <= 0) throw new ArgumentOutOfRangeException(nameof(massKilograms));
            var relative = vehicleVelocity - airVelocity;
            double speed = relative.Magnitude();
            double q = 0.5 * densityKgPerCubicMeter * speed * speed;
            if (!Finite(speed) || !Finite(q)) throw new ArgumentOutOfRangeException(nameof(vehicleVelocity), "Aerodynamic calculation exceeds finite numeric range.");
            if (speed == 0 || densityKgPerCubicMeter == 0 || dragCoefficient == 0 || referenceAreaSquareMeters == 0)
                return new DragResult(Vector3d.Zero, Vector3d.Zero, q, speed);
            double magnitude = q * dragCoefficient * referenceAreaSquareMeters;
            if (!Finite(magnitude)) throw new ArgumentOutOfRangeException(nameof(dragCoefficient));
            var force = relative * (-magnitude / speed);
            return new DragResult(force, force * (1.0 / massKilograms), q, speed);
        }

        /// <summary>For inertial state: omega cross r plus already-transformed inertial wind.</summary>
        public static Vector3d CorotatingAirVelocity(Vector3d inertialPosition, Vector3d inertialWind)
            => new Vector3d(-EarthAngularVelocityRadiansPerSecond * inertialPosition.Y,
                EarthAngularVelocityRadiansPerSecond * inertialPosition.X, 0) + inertialWind;

        /// <summary>
        /// ENU X=east,Y=north,Z=up to inertial axes Z=north pole.
        /// latitudeRadians is geodetic. longitudeInertialRadians = Earth-fixed longitude + Earth rotation angle at state time.
        /// This method does not compute an epoch or rotation angle for the caller.
        /// </summary>
        public static Vector3d WindToInertial(Vector3d eastNorthUp, double latitudeRadians, double longitudeInertialRadians)
        {
            if (!Finite(latitudeRadians) || Math.Abs(latitudeRadians) > Math.PI / 2) throw new ArgumentOutOfRangeException(nameof(latitudeRadians));
            if (!Finite(longitudeInertialRadians)) throw new ArgumentOutOfRangeException(nameof(longitudeInertialRadians));
            double s = Math.Sin(latitudeRadians), c = Math.Cos(latitudeRadians);
            double sl = Math.Sin(longitudeInertialRadians), cl = Math.Cos(longitudeInertialRadians);
            return new Vector3d(-sl * eastNorthUp.X - s * cl * eastNorthUp.Y + c * cl * eastNorthUp.Z,
                cl * eastNorthUp.X - s * sl * eastNorthUp.Y + c * sl * eastNorthUp.Z,
                c * eastNorthUp.Y + s * eastNorthUp.Z);
        }

        private static void Nonnegative(double value, string name) { if (!Finite(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
