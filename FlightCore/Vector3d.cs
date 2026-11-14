using System;
using System.Globalization;

namespace FlightCore
{
    /// <summary>
    /// An immutable three-component double vector.
    /// </summary>
    public readonly struct Vector3d
    {
        /// <summary>
        /// The X component of the vector.
        /// </summary>
        public readonly double X;

        /// <summary>
        /// The Y component of the vector.
        /// </summary>
        public readonly double Y;

        /// <summary>
        /// The Z component of the vector.
        /// </summary>
        public readonly double Z;

        /// <summary>
        /// A zero vector (0, 0, 0).
        /// </summary>
        public static readonly Vector3d Zero = new Vector3d(0.0, 0.0, 0.0);

        /// <summary>
        /// Creates a new vector with the specified components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Vector3d(double x, double y, double z)
        {
            // Validate that components are finite (not NaN or infinity)
            if (double.IsNaN(x) || double.IsInfinity(x))
                throw new ArgumentException("Vector component X must be finite.", nameof(x));
            if (double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentException("Vector component Y must be finite.", nameof(y));
            if (double.IsNaN(z) || double.IsInfinity(z))
                throw new ArgumentException("Vector component Z must be finite.", nameof(z));

            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Computes the magnitude (length) of this vector.
        /// </summary>
        /// <returns>The magnitude of the vector.</returns>
        public double Magnitude()
        {
            return Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        /// <summary>
        /// Returns a normalized version of this vector.
        /// </summary>
        /// <returns>A unit vector in the same direction as this vector.</returns>
        public Vector3d Normalize()
        {
            double magnitude = Magnitude();
            if (magnitude == 0.0)
                throw new InvalidOperationException("Cannot normalize a zero vector.");
            
            return new Vector3d(X / magnitude, Y / magnitude, Z / magnitude);
        }

        /// <summary>
        /// Adds two vectors.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The sum of the two vectors.</returns>
        public static Vector3d operator +(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        /// <summary>
        /// Subtracts the second vector from the first.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The difference of the two vectors.</returns>
        public static Vector3d operator -(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        /// <param name="scalar">The scalar value.</param>
        /// <param name="vector">The vector to multiply.</param>
        /// <returns>The scaled vector.</returns>
        public static Vector3d operator *(double scalar, Vector3d vector)
        {
            return new Vector3d(scalar * vector.X, scalar * vector.Y, scalar * vector.Z);
        }

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        /// <param name="vector">The vector to multiply.</param>
        /// <param name="scalar">The scalar value.</param>
        /// <returns>The scaled vector.</returns>
        public static Vector3d operator *(Vector3d vector, double scalar)
        {
            return scalar * vector;
        }

        /// <summary>
        /// Returns a string representation of this vector.
        /// </summary>
        /// <returns>A string representation of the vector.</returns>
        public override string ToString()
        {
            return $"({X:G17}, {Y:G17}, {Z:G17})";
        }
    }
}