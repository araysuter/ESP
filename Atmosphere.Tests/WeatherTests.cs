using System;
using FlightCore;

internal static class WeatherTests
{
    private static readonly DateTime Launch = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Age = TimeSpan.FromHours(3);

    public static void Run()
    {
        var levels = new[] {
            WeatherLevel.FromMeteorological(1000, 280, 90000, 10, 90),
            WeatherLevel.FromMeteorological(3000, 260, 40000, 20, 0)
        };
        var profile = new WeatherProfile(levels, "Test", Launch.AddHours(-1), 42, -83);
        var weather = new WeatherAtmosphere(profile, Launch, Age);
        if (!weather.UsesWeather) throw new Exception("Valid weather rejected");
        var midpoint = weather.Sample(2000);
        Near(midpoint.TemperatureKelvin, 270, 1e-10, "interpolated temperature");
        Near(midpoint.PressurePascals, 60000, 1e-7, "log pressure interpolation");
        Vector(midpoint.WindEastNorthUpMetersPerSecond, new Vector3d(-5, -10, 0), 1e-10, "meteorological wind");
        levels[0] = WeatherLevel.FromMeteorological(1000, 300, 100000, 0, 0);
        Near(weather.Sample(2000).PressurePascals, midpoint.PressurePascals, 0, "frozen copied profile");
        Near(profile.Levels[0].Atmosphere.TemperatureKelvin, 280, 0, "immutable copy");
        foreach (double edge in new[] { 500.0, 1000, 3000, 5000 }) Continuity(weather, edge);
        Fallback(null);
        Fallback(new WeatherProfile(profile.Levels, "Test", Launch.AddHours(-4), 42, -83));
        Fallback(new WeatherProfile(profile.Levels, "Test", Launch.AddSeconds(1), 42, -83));
        var exactAge = new WeatherAtmosphere(new WeatherProfile(profile.Levels, "Test", Launch - Age, 42, -83), Launch, Age);
        if (!exactAge.UsesWeather) throw new Exception("Exact maximum age should be accepted");
        Near(weather.Sample(1000).PressurePascals, 90000, 1e-7, "exact lower node");
        Near(weather.Sample(3000).PressurePascals, 40000, 1e-7, "exact upper node");
        Near(weather.Sample(5000).PressurePascals, StandardAtmosphere.Sample(5000).PressurePascals, 0, "upper blend end");
        Near(weather.Sample(500).PressurePascals, StandardAtmosphere.Sample(500).PressurePascals, 0, "lower blend end");
        Invalid(profile);
        DissipationAndDensity();
    }

    private static void Fallback(WeatherProfile? profile)
    {
        var weather = new WeatherAtmosphere(profile, Launch, Age);
        if (weather.UsesWeather) throw new Exception("Invalid or missing weather was accepted");
        var actual = weather.Sample(2000);
        var expected = StandardAtmosphere.Sample(2000);
        Near(actual.PressurePascals, expected.PressurePascals, 0, "fallback pressure");
        Near(actual.TemperatureKelvin, expected.TemperatureKelvin, 0, "fallback temperature");
        Vector(actual.WindEastNorthUpMetersPerSecond, expected.WindEastNorthUpMetersPerSecond, 0, "fallback wind");
    }

    private static void Invalid(WeatherProfile profile)
    {
        Throws(() => new AtmosphereSample(double.Epsilon, 101325, Vector3d.Zero), "derived density overflow");
        Throws(() => new WeatherProfile(new[] { profile.Levels[0], profile.Levels[0] }, "Test", Launch, 0, 0), "duplicate altitude");
        Throws(() => new WeatherProfile(new[] { default(WeatherLevel), profile.Levels[1] }, "Test", Launch, 0, 0), "default level");
        Throws(() => WeatherLevel.FromMeteorological(double.NaN, 280, 90000, 1, 0), "nonfinite altitude");
        Throws(() => WeatherLevel.FromMeteorological(1000, 280, 90000, double.PositiveInfinity, 0), "nonfinite wind");
        Throws(() => new WeatherProfile(profile.Levels, "Test", DateTime.SpecifyKind(Launch, DateTimeKind.Unspecified), 0, 0), "non UTC profile");
        Throws(() => new WeatherAtmosphere(profile, DateTime.SpecifyKind(Launch, DateTimeKind.Local), Age), "non UTC launch");
    }

    private static void Continuity(WeatherAtmosphere weather, double altitude)
    {
        var a = weather.Sample(altitude - 0.0001);
        var b = weather.Sample(altitude + 0.0001);
        Near(a.TemperatureKelvin, b.TemperatureKelvin, 0.01, "weather temperature continuity");
        Near(a.PressurePascals, b.PressurePascals, 0.1, "weather pressure continuity");
        Vector(a.WindEastNorthUpMetersPerSecond, b.WindEastNorthUpMetersPerSecond, 0.001, "weather wind continuity");
    }

    private static void DissipationAndDensity()
    {
        var position = new Vector3d(6378137, 0, 0);
        var wind = Aerodynamics.WindToInertial(new Vector3d(20, -15, 0), 0, 0);
        var air = Aerodynamics.CorotatingAirVelocity(position, wind);
        var vehicle = new Vector3d(100, 500, 50);
        var relative = vehicle - air;
        var coldDense = new AtmosphereSample(250, 100000, Vector3d.Zero);
        var warmThin = new AtmosphereSample(300, 80000, Vector3d.Zero);
        var denseDrag = Aerodynamics.Compute(vehicle, air, coldDense.DensityKgPerCubicMeter, 0.5, 4, 1000);
        var thinDrag = Aerodynamics.Compute(vehicle, air, warmThin.DensityKgPerCubicMeter, 0.5, 4, 1000);
        var f = denseDrag.ForceNewtons;
        if (f.X * relative.X + f.Y * relative.Y + f.Z * relative.Z > 0) throw new Exception("Drag added energy relative to air");
        if (denseDrag.ForceNewtons.Magnitude() <= thinDrag.ForceNewtons.Magnitude()) throw new Exception("Density did not affect drag");
        Near(coldDense.DensityKgPerCubicMeter / warmThin.DensityKgPerCubicMeter, 1.5, 1e-10, "dry-air density ratio");
    }

    private static void Throws(Action action, string name)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected argument exception: " + name);
    }
    private static void Near(double a, double b, double tolerance, string name)
    {
        if (!double.IsFinite(a) || Math.Abs(a - b) > tolerance) throw new Exception($"{name}: {a:R} vs {b:R}");
    }
    private static void Vector(Vector3d a, Vector3d b, double tolerance, string name)
    {
        Near(a.X, b.X, tolerance, name + " X"); Near(a.Y, b.Y, tolerance, name + " Y"); Near(a.Z, b.Z, tolerance, name + " Z");
    }
}
