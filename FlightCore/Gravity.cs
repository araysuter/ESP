using System;

namespace FlightCore
{
    /// <summary>
    /// Provides Earth gravity calculations using a point-mass approximation.
    /// </summary>
    /// <remarks>
    /// Uses the standard gravitational parameter for Earth: μ = 3.986004418 × 10^14 m³/s²
    /// Source: https://earth-info.nga.mil/?action=wgs84&dir=wgs84
    /// 
    /// The acceleration is computed using the formula: a = -μr / |r|³
    /// where r is the Earth-centered position vector in meters.
    /// </remarks>
    public static class Gravity
    {
        /// <summary>
        /// Standard gravitational parameter for Earth (m³/s²).
        /// Source: https://earth-info.nga.mil/?action=wgs84&dir=wgs84
        /// </summary>
        public const double EarthGravitationalParameter = 3.986004418e14;

        /// <summary>
        /// Computes the Earth-centered gravitational acceleration at the specified position.
        /// </summary>
        /// <param name="position">The Earth-centered position in meters.</param>
        /// <returns>The gravitational acceleration in m/s².</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the position vector is zero, NaN, or infinity.
        /// </exception>
        public static Vector3d Acceleration(Vector3d position)
        {
            // Validate input
            if (position.X == 0.0 && position.Y == 0.0 && position.Z == 0.0)
                throw new ArgumentException("Position vector cannot be zero (division by zero).", nameof(position));
            
            if (double.IsNaN(position.X) || double.IsNaN(position.Y) || double.IsNaN(position.Z))
                throw new ArgumentException("Position vector components must be finite (not NaN).", nameof(position));
            
            if (double.IsInfinity(position.X) || double.IsInfinity(position.Y) || double.IsInfinity(position.Z))
                throw new ArgumentException("Position vector components must be finite (not infinity).", nameof(position));

            // Compute r = |position|
            double r = position.Magnitude();
            
            // Compute acceleration using a = -μr / |r|³
            // This simplifies to a = -μ / |r|² * (position / |position|)
            // Which is equivalent to a = -μ * position / |position|³
            double scaleFactor = -EarthGravitationalParameter / (r * r * r);
            
            return scaleFactor * position;
        }
    }
}