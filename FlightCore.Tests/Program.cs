using System;

namespace FlightCore.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                RunTests();
                Console.WriteLine("All tests passed!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Test failed: {ex.Message}");
                return 1;
            }
        }

        static void RunTests()
        {
            TestAtEarthSurface();
            TestOffAxisPosition();
            
            Console.WriteLine("Basic test cases passed successfully.");
        }

        /// <summary>
        /// Test case: At (6378137, 0, 0) meters, acceleration is approximately (-9.79828548, 0, 0) m/s².
        /// </summary>
        static void TestAtEarthSurface()
        {
            Console.WriteLine("Testing Earth surface position...");
            
            // Earth's equatorial radius in meters
            const double earthRadius = 6378137.0;
            
            var position = new Vector3d(earthRadius, 0.0, 0.0);
            var acceleration = Gravity.Acceleration(position);
            
            // Expected value from the specification
            const double expectedX = -9.79828548;
            const double tolerance = 1e-8; // Tolerance for numerical precision
            
            if (Math.Abs(acceleration.X - expectedX) > tolerance)
            {
                throw new InvalidOperationException($"Expected X acceleration of {expectedX}, but got {acceleration.X}");
            }
            
            if (Math.Abs(acceleration.Y) > tolerance)
            {
                throw new InvalidOperationException($"Expected Y acceleration to be near zero, but got {acceleration.Y}");
            }
            
            if (Math.Abs(acceleration.Z) > tolerance)
            {
                throw new InvalidOperationException($"Expected Z acceleration to be near zero, but got {acceleration.Z}");
            }
            
            Console.WriteLine($"  ✓ At Earth surface: acceleration = {acceleration}");
        }

        /// <summary>
        /// Test case: At an off-axis position, acceleration points directly toward Earth's center.
        /// </summary>
        static void TestOffAxisPosition()
        {
            Console.WriteLine("Testing off-axis position...");
            
            // Position at 100km above Earth surface in a diagonal direction
            const double earthRadius = 6378137.0;
            const double altitude = 100000.0; // 100 km
            
            var position = new Vector3d(earthRadius + altitude, earthRadius + altitude, 0.0);
            var acceleration = Gravity.Acceleration(position);
            
            // The acceleration should point directly toward the origin (0,0,0)
            // This means acceleration should be proportional to -position
            double positionMagnitude = position.Magnitude();
            double accelerationMagnitude = acceleration.Magnitude();
            
            // Check that acceleration points in the opposite direction of position
            double dotProduct = acceleration.X * position.X + acceleration.Y * position.Y + acceleration.Z * position.Z;
            if (dotProduct > 0)
            {
                throw new InvalidOperationException("Acceleration should point toward Earth center, but dot product is positive.");
            }
            
            // Check that acceleration magnitude is correct (using inverse square law)
            double expectedMagnitude = Gravity.EarthGravitationalParameter / (positionMagnitude * positionMagnitude);
            double tolerance = 1e-10;
            
            if (Math.Abs(accelerationMagnitude - expectedMagnitude) > tolerance)
            {
                throw new InvalidOperationException($"Acceleration magnitude {accelerationMagnitude} does not match expected {expectedMagnitude}");
            }
            
            Console.WriteLine($"  ✓ Off-axis position: acceleration = {acceleration}, magnitude = {accelerationMagnitude:F10}");
        }
    }
}