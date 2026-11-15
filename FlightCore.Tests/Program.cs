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
            SolarLightingTests.Run();
            CelestialFramesTests.Run();
            TestAtEarthSurface();
            TestAt100KmAltitude();
            TestSignedAxes();
            TestOffAxisPosition();
            TestDistanceDoubling();
            TestInvalidInputs();
            TestRegressionOriginHit();
            TestCircularOrbit();

            Console.WriteLine("All test cases passed successfully.");
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
            
            AssertNear(acceleration.X, expectedX, tolerance, "Surface X");
            AssertNear(acceleration.Y, 0.0, tolerance, "Surface Y");
            AssertNear(acceleration.Z, 0.0, tolerance, "Surface Z");

            Console.WriteLine($"  ✓ At Earth surface: acceleration = {acceleration}");
        }

        /// <summary>
        /// Test case: At an off-axis position, acceleration points directly toward Earth's center.
        /// </summary>
        static void TestOffAxisPosition()
        {
            Console.WriteLine("Testing off-axis position...");
            
            // Position with three unequal, nonzero components to make the test more robust
            var position = new Vector3d(6378137.0 + 100000.0, 6378137.0 + 50000.0, 6378137.0 + 200000.0);
            var acceleration = Gravity.Acceleration(position);
            
            // The acceleration should point directly toward the origin (0,0,0)
            // This means acceleration should be proportional to -position
            double positionMagnitude = position.Magnitude();
            double accelerationMagnitude = acceleration.Magnitude();
            
            // Check that acceleration points in the opposite direction of position
            double dotProduct = acceleration.X * position.X + acceleration.Y * position.Y + acceleration.Z * position.Z;
            if (double.IsNaN(dotProduct) || double.IsInfinity(dotProduct) || dotProduct >= 0)
            {
                throw new InvalidOperationException($"Acceleration and position vectors should point in opposite directions. Dot product: {dotProduct}");
            }
            
            // Check that the magnitudes are correct (inverse square law)
            double expectedMagnitude = Gravity.EarthGravitationalParameter / (positionMagnitude * positionMagnitude);
            double tolerance = 1e-10; // Tolerance for numerical precision
            
            AssertNear(accelerationMagnitude, expectedMagnitude, tolerance, "Off-axis magnitude");

            // Strengthen test: Compare normalized vectors
            // The normalized acceleration vector should be the negative of the normalized position vector
            var normalizedPosition = position * (1.0 / positionMagnitude);
            var normalizedAcceleration = acceleration * (1.0 / accelerationMagnitude);
            
            // Check that normalized acceleration equals -normalized position within tolerance
            double normalizationTolerance = 1e-12; // Very strict tolerance for normalization
            
            AssertNear(normalizedAcceleration.X, -normalizedPosition.X, normalizationTolerance, "Off-axis direction X");
            AssertNear(normalizedAcceleration.Y, -normalizedPosition.Y, normalizationTolerance, "Off-axis direction Y");
            AssertNear(normalizedAcceleration.Z, -normalizedPosition.Z, normalizationTolerance, "Off-axis direction Z");

            Console.WriteLine($"  ✓ Off-axis position: acceleration = {acceleration}, magnitude = {accelerationMagnitude:F10}");
        }

        /// <summary>
        /// Test case: Distance-doubling verification - acceleration should decrease by a factor of 4.
        /// </summary>
        static void TestDistanceDoubling()
        {
            Console.WriteLine("Testing distance-doubling rule...");
            
            // Earth's equatorial radius in meters
            const double earthRadius = 6378137.0;
            
            // Test at Earth surface
            var position1 = new Vector3d(earthRadius, 0.0, 0.0);
            var acceleration1 = Gravity.Acceleration(position1);
            
            // Test at twice the distance from Earth center
            var position2 = new Vector3d(2 * earthRadius, 0.0, 0.0);
            var acceleration2 = Gravity.Acceleration(position2);
            
            // Acceleration should decrease by a factor of 4 (inverse square law)
            double expectedRatio = 0.25; // 1/4
            double actualRatio = acceleration2.X / acceleration1.X;
            double tolerance = 1e-12;
            
            AssertNear(actualRatio, expectedRatio, tolerance, "Distance-doubling ratio");

            Console.WriteLine($"  ✓ Distance-doubling test: ratio = {actualRatio:F12} (expected ~0.25)");
        }

        static void TestAt100KmAltitude()
        {
            var acceleration = Gravity.Acceleration(new Vector3d(6478137.0, 0.0, 0.0));
            // Independent numeric reference for the documented point-mass example.
            AssertNear(acceleration.X, -9.498117091820768, 1e-12, "100 km X");
            AssertNear(acceleration.Y, 0.0, 1e-12, "100 km Y");
            AssertNear(acceleration.Z, 0.0, 1e-12, "100 km Z");
        }

        static void TestSignedAxes()
        {
            const double radius = 6378137.0;
            const double expectedMagnitude = 9.7982854791873;
            for (int axis = 0; axis < 3; axis++)
            {
                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    var position = new Vector3d(axis == 0 ? sign * radius : 0.0,
                        axis == 1 ? sign * radius : 0.0, axis == 2 ? sign * radius : 0.0);
                    var acceleration = Gravity.Acceleration(position);
                    AssertNear(acceleration.X, axis == 0 ? -sign * expectedMagnitude : 0.0, 1e-12, "Signed axis X");
                    AssertNear(acceleration.Y, axis == 1 ? -sign * expectedMagnitude : 0.0, 1e-12, "Signed axis Y");
                    AssertNear(acceleration.Z, axis == 2 ? -sign * expectedMagnitude : 0.0, 1e-12, "Signed axis Z");
                }
            }
        }

        static void AssertNear(double actual, double expected, double tolerance, string label)
        {
            if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }

        static void AssertArgumentException(Action action, string label)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new InvalidOperationException($"{label}: expected ArgumentException");
        }

        static void TestInvalidInputs()
        {
            // Finite zero vectors are valid vectors, but undefined positions for gravity.
            AssertArgumentException(() => Gravity.Acceleration(Vector3d.Zero), "Gravity zero position");
            AssertArgumentException(() => Gravity.Acceleration(default(Vector3d)), "Gravity default position");

            // Nonfinite components are rejected by construction, before gravity is called.
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                AssertArgumentException(() => new Vector3d(invalid, 1.0, 1.0), "Vector X");
                AssertArgumentException(() => new Vector3d(1.0, invalid, 1.0), "Vector Y");
                AssertArgumentException(() => new Vector3d(1.0, 1.0, invalid), "Vector Z");
            }
        }

        static void TestCircularOrbit()
        {
            const double radius = 7_000_000.0; // m
            const double elapsedTime = 5_820.0; // s, just under the analytic period
            const double mu = Gravity.EarthGravitationalParameter;
            double speed = Math.Sqrt(mu / radius);
            double phase = elapsedTime * speed / radius;
            var expectedPosition = new Vector3d(radius * Math.Cos(phase), radius * Math.Sin(phase), 0.0);
            var expectedVelocity = new Vector3d(-speed * Math.Sin(phase), speed * Math.Cos(phase), 0.0);

            // Rounded bounds above the reference errors of 1,705 m / 1.836 m/s
            // at 10 s and 426 m / 0.459 m/s at 5 s.
            double coarseError = RunCircularOrbit(10.0, 1_750.0, 1.9);
            double fineError = RunCircularOrbit(5.0, 450.0, 0.5);

            // Velocity Verlet is second order, so halving the step should quarter the error.
            double convergence = coarseError / fineError;
            Console.WriteLine($"  Coarse-to-fine position error ratio: {convergence:F4}");
            if (!(convergence >= 3.8 && convergence <= 4.2))
                throw new InvalidOperationException($"Expected convergence ratio in [3.8, 4.2], got {convergence:G17}.");

            double RunCircularOrbit(double timeStep, double positionTolerance, double velocityTolerance)
            {
                // Relative drift limits allow small integration error in energy and rounding in angular momentum.
                const double energyTolerance = 1e-8;
                const double momentumTolerance = 1e-12;
                var simulator = new OrbitSimulator(timeStep);
                simulator.Initialize(new Vector3d(radius, 0.0, 0.0), new Vector3d(0.0, speed, 0.0));
                double initialEnergy = SpecificEnergy(simulator.State);
                Vector3d initialMomentum = SpecificAngularMomentum(simulator.State);
                double initialMomentumMagnitude = initialMomentum.Magnitude();
                double maxEnergyDrift = 0.0;
                double maxMomentumDrift = 0.0;

                int steps = (int)(elapsedTime / timeStep);
                for (int i = 0; i < steps; i++)
                {
                    simulator.Step();
                    var state = simulator.State;
                    double energyDrift = Math.Abs((SpecificEnergy(state) - initialEnergy) / initialEnergy);
                    double momentumDrift = (SpecificAngularMomentum(state) - initialMomentum).Magnitude()
                        / initialMomentumMagnitude;
                    maxEnergyDrift = Math.Max(maxEnergyDrift, energyDrift);
                    maxMomentumDrift = Math.Max(maxMomentumDrift, momentumDrift);
                }

                var finalState = simulator.State;
                if (finalState.MissionTime != elapsedTime)
                    throw new InvalidOperationException($"Expected mission time {elapsedTime} s, got {finalState.MissionTime} s.");

                double positionError = (finalState.Position - expectedPosition).Magnitude();
                double velocityError = (finalState.Velocity - expectedVelocity).Magnitude();
                Console.WriteLine($"  {timeStep:F0} s step: {positionError:F2} m position error, {velocityError:F6} m/s velocity error");
                Console.WriteLine($"    Max relative drift: energy {maxEnergyDrift:E3}, angular momentum {maxMomentumDrift:E3}");
                AssertAtMost(positionError, positionTolerance, "Position error");
                AssertAtMost(velocityError, velocityTolerance, "Velocity error");
                AssertAtMost(maxEnergyDrift, energyTolerance, "Relative specific-energy drift");
                AssertAtMost(maxMomentumDrift, momentumTolerance, "Relative specific angular-momentum drift");
                return positionError;
            }
        }

        static double SpecificEnergy(OrbitStateSnapshot state)
        {
            var velocity = state.Velocity;
            double speedSquared = velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z;
            return 0.5 * speedSquared - Gravity.EarthGravitationalParameter / state.Position.Magnitude();
        }

        static Vector3d SpecificAngularMomentum(OrbitStateSnapshot state)
        {
            var position = state.Position;
            var velocity = state.Velocity;
            return new Vector3d(
                position.Y * velocity.Z - position.Z * velocity.Y,
                position.Z * velocity.X - position.X * velocity.Z,
                position.X * velocity.Y - position.Y * velocity.X);
        }

        static void AssertAtMost(double value, double limit, string measurement)
        {
            if (!(value <= limit))
                throw new InvalidOperationException($"{measurement} {value:G17} exceeds limit {limit:G17}.");
        }

        static void TestRegressionOriginHit()
        {
            var position = new Vector3d(1.0, 0.0, 0.0);
            // After the first half-kick, velocity is exactly -1 m/s along X.
            // The full drift then lands at the origin, where gravity throws.
            double velocityX = -1.0 - 0.5 * Gravity.Acceleration(position).X;
            var simulator = new OrbitSimulator(1.0);
            simulator.Initialize(position, new Vector3d(velocityX, 0.0, 0.0));
            var before = simulator.State;

            try
            {
                simulator.Step();
                throw new InvalidOperationException("Expected the second gravity evaluation to throw at the origin.");
            }
            catch (ArgumentException exception) when (exception.ParamName == "position")
            {
                var after = simulator.State;
                if (!after.Position.Equals(before.Position) || !after.Velocity.Equals(before.Velocity)
                    || after.MissionTime != before.MissionTime)
                    throw new InvalidOperationException("Failed step changed the simulator snapshot.");
            }

            Console.WriteLine("  ✓ Failed step preserved position, velocity, and mission time.");
        }
    }
}
