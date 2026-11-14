using System;
using FlightCore;

internal static class Program
{
    private static int Main()
    {
        try
        {
            AtmosphereReferences();
            AtmosphereBoundaries();
            Validation();
            Drag();
            FramesAndWind();
            WeatherTests.Run();
            Console.WriteLine("Atmosphere and aerodynamic tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void AtmosphereReferences()
    {
        // US Standard Atmosphere 1976 layer-base values at geopotential heights.
        CheckAtmosphere(0, 288.15, 101325.0, 1.2250);
        CheckAtmosphere(11000, 216.65, 22632.1, 0.363918);
        CheckAtmosphere(20000, 216.65, 5474.89, 0.0880347);
        CheckAtmosphere(32000, 228.65, 868.019, 0.0132250);
        var belowSeaLevel = StandardAtmosphere.Sample(-500);
        if (belowSeaLevel.PressurePascals <= 101325 || belowSeaLevel.DensityKgPerCubicMeter <= 1.225)
            throw new Exception("Below-sea-level atmosphere should be denser than sea level.");
        var vacuum = StandardAtmosphere.Sample(120000);
        Near(vacuum.PressurePascals, 0, 1e-12, "vacuum pressure");
        Near(vacuum.DensityKgPerCubicMeter, 0, 1e-12, "vacuum density");
    }

    private static void CheckAtmosphere(double geopotential, double temperature, double pressure, double density)
    {
        const double radius = 6356766.0;
        double geometric = radius * geopotential / (radius - geopotential);
        var sample = StandardAtmosphere.Sample(geometric);
        Near(sample.TemperatureKelvin, temperature, 0.02, $"temperature at H={geopotential}");
        Near(sample.PressurePascals, pressure, Math.Max(0.05, pressure * 0.0005), $"pressure at H={geopotential}");
        Near(sample.DensityKgPerCubicMeter, density, Math.Max(0.00001, density * 0.001), $"density at H={geopotential}");
        VectorNear(sample.WindEastNorthUpMetersPerSecond, Vector3d.Zero, 0, "standard wind");
    }

    private static void AtmosphereBoundaries()
    {
        const double radius = 6356766.0;
        foreach (double h in new[] { 11000.0, 20000, 32000, 47000, 51000, 71000, 84852 })
        {
            double z = radius * h / (radius - h);
            Continuous(z);
        }
        Continuous(86000);
        Continuous(120000);
    }

    private static void Continuous(double altitude)
    {
        var a = StandardAtmosphere.Sample(altitude - 0.001);
        var b = StandardAtmosphere.Sample(altitude + 0.001);
        Near(a.TemperatureKelvin, b.TemperatureKelvin, 0.1, $"temperature continuity at {altitude}");
        Near(a.PressurePascals, b.PressurePascals, Math.Max(0.001, a.PressurePascals * 0.0001), $"pressure continuity at {altitude}");
        Near(a.DensityKgPerCubicMeter, b.DensityKgPerCubicMeter, Math.Max(1e-9, a.DensityKgPerCubicMeter * 0.0001), $"density continuity at {altitude}");
    }

    private static void Validation()
    {
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -501.0 })
            Throws(() => StandardAtmosphere.Sample(value), "invalid altitude");
        Throws(() => new AtmosphereSample(double.NaN, 101325, Vector3d.Zero), "invalid temperature");
        Throws(() => new AtmosphereSample(288, double.NaN, Vector3d.Zero), "invalid pressure");
        Throws(() => Aerodynamics.Compute(Vector3d.Zero, Vector3d.Zero, -1, 1, 1, 1), "negative density");
        Throws(() => Aerodynamics.Compute(Vector3d.Zero, Vector3d.Zero, 1, double.NaN, 1, 1), "invalid drag coefficient");
        Throws(() => Aerodynamics.Compute(Vector3d.Zero, Vector3d.Zero, 1, 1, 1, 0), "zero mass");
    }

    private static void Drag()
    {
        var result = Aerodynamics.Compute(new Vector3d(100, 0, 0), Vector3d.Zero, 1.225, 0.5, 4, 1000);
        Near(result.DynamicPressurePascals, 6125, 1e-9, "dynamic pressure");
        Near(result.AirSpeedMetersPerSecond, 100, 1e-12, "airspeed");
        VectorNear(result.ForceNewtons, new Vector3d(-12250, 0, 0), 1e-9, "drag force");
        VectorNear(result.AccelerationMetersPerSecondSquared, new Vector3d(-12.25, 0, 0), 1e-12, "drag acceleration");
        var zero = Aerodynamics.Compute(new Vector3d(10, 20, 30), new Vector3d(10, 20, 30), 1.225, 1, 1, 1000);
        VectorNear(zero.ForceNewtons, Vector3d.Zero, 0, "zero relative speed");
        var diagonal = Aerodynamics.Compute(new Vector3d(3, 4, 0), Vector3d.Zero, 1, 1, 1, 1);
        VectorNear(diagonal.ForceNewtons, new Vector3d(-7.5, -10, 0), 1e-12, "drag direction");
        var still = Aerodynamics.Compute(Vector3d.Zero, new Vector3d(10, 0, 0), 1, 1, 1, 1);
        VectorNear(still.ForceNewtons, new Vector3d(50, 0, 0), 1e-12, "wind on stationary vehicle");
        var tailwind = Aerodynamics.Compute(new Vector3d(100, 0, 0), new Vector3d(20, 0, 0), 1, 1, 1, 1);
        Near(tailwind.AirSpeedMetersPerSecond, 80, 1e-12, "tailwind airspeed");
        VectorNear(tailwind.ForceNewtons, new Vector3d(-3200, 0, 0), 1e-9, "tailwind drag");
    }

    private static void FramesAndWind()
    {
        VectorNear(Aerodynamics.CorotatingAirVelocity(new Vector3d(6378137, 0, 0), Vector3d.Zero), new Vector3d(0, 465.10108489755, 0), 1e-8, "equatorial rotation");
        VectorNear(Aerodynamics.CorotatingAirVelocity(new Vector3d(0, 0, 6356752), new Vector3d(1, 2, 3)), new Vector3d(1, 2, 3), 1e-12, "polar rotation and wind");
        VectorNear(Aerodynamics.WindToInertial(new Vector3d(1, 0, 0), 0, 0), new Vector3d(0, 1, 0), 1e-12, "east");
        VectorNear(Aerodynamics.WindToInertial(new Vector3d(0, 1, 0), 0, 0), new Vector3d(0, 0, 1), 1e-12, "north");
        VectorNear(Aerodynamics.WindToInertial(new Vector3d(0, 0, 1), 0, 0), new Vector3d(1, 0, 0), 1e-12, "up");
    }

    private static void Throws(Action action, string name)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception($"Expected argument exception: {name}");
    }

    private static void Near(double actual, double expected, double tolerance, string name)
    {
        if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception($"{name}: expected {expected:R} ± {tolerance:R}, got {actual:R}");
    }

    private static void VectorNear(Vector3d actual, Vector3d expected, double tolerance, string name)
    {
        Near(actual.X, expected.X, tolerance, name + " X");
        Near(actual.Y, expected.Y, tolerance, name + " Y");
        Near(actual.Z, expected.Z, tolerance, name + " Z");
    }
}
