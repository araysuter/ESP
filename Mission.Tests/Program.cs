using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FlightCore;

internal static class Program
{
    private static readonly DateTime Launch = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int Main()
    {
        try
        {
            Compare("site A offline", 25.997, -97.156, null);
            Compare("LC-39A offline", 28.608422, -80.604133, null);
            Compare("LC-39A historical weather", 28.608422, -80.604133, LoadWeather());
            Console.WriteLine("Mission tests passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static WeatherProfile LoadWeather()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "weather.json")));
        var root = doc.RootElement; var levels = new List<WeatherLevel>();
        foreach (var v in root.GetProperty("levels").EnumerateArray())
            levels.Add(new WeatherLevel(v.GetProperty("geometricAltitudeMeters").GetDouble(), new AtmosphereSample(
                v.GetProperty("temperatureKelvin").GetDouble(), v.GetProperty("pressurePascals").GetDouble(),
                new Vector3d(v.GetProperty("windEastMetersPerSecond").GetDouble(), v.GetProperty("windNorthMetersPerSecond").GetDouble(), 0))));
        return new WeatherProfile(levels, root.GetProperty("provider").GetString()!, root.GetProperty("validTimeUtc").GetDateTime(), root.GetProperty("latitudeDegrees").GetDouble(), root.GetProperty("longitudeDegrees").GetDouble());
    }
    private static void Compare(string name, double lat, double lon, WeatherProfile? profile)
    {
        var a = Run(name, lat, lon, profile, 0.02); var b = Run(name, lat, lon, profile, 0.01);
        // Timing differs by at most five coarse steps; a 2 km perigee tolerance covers guidance sampling differences.
        Near(a.time, b.time, 0.1, name + " cutoff convergence"); Near(a.bound, b.bound, 2000, name + " orbit convergence");
    }
    private static (double time, double bound) Run(string name, double lat, double lon, WeatherProfile? profile, double step)
    {
        var atmosphere = new WeatherAtmosphere(profile, Launch, TimeSpan.FromHours(4));
        if ((profile != null) != atmosphere.UsesWeather) throw new Exception(name + " weather selection");
        var rocket = new RocketSimulation(step, StageDefinition.DemoStages(), atmosphere, lat * Math.PI / 180, lon * Math.PI / 180);
        var guidance = new AssistedAscent(); rocket.Launch(); double minBound = double.PositiveInfinity;
        for (int i = 0; i < 1200 / step; i++)
        {
            guidance.Update(rocket); var s = rocket.State;
            if (!double.IsFinite(s.MissionTime) || !double.IsFinite(s.AltitudeMeters) || !double.IsFinite(s.PropellantKg) || s.PropellantKg < 0 || s.IsLanded)
                throw new Exception(name + " invalid flight state");
            if (guidance.HasCutOff)
            {
                var orbit = OrbitDiagnostics.Compute(s.Position, s.Velocity);
                if (!orbit.IsBound || !double.IsFinite(orbit.MinimumWgs84AltitudeBoundMeters) || orbit.MinimumWgs84AltitudeBoundMeters < 180000)
                    throw new Exception(name + " coast fell below 180 km bound");
                minBound = Math.Min(minBound, orbit.MinimumWgs84AltitudeBoundMeters);
                if (s.MissionTime >= guidance.CutoffMissionTimeSeconds + 60)
                {
                    if (s.StageIndex != 1) throw new Exception(name + " expected second stage");
                    rocket.Throttle = 1; guidance.Update(rocket);
                    if (rocket.Throttle != 0) throw new Exception(name + " cutoff not sticky");
                    Console.WriteLine($"{name} dt={step}: cutoff={guidance.CutoffMissionTimeSeconds:F3}s min bound={minBound:F1}m");
                    return (guidance.CutoffMissionTimeSeconds, minBound);
                }
            }
            rocket.Step();
        }
        throw new Exception(name + " did not complete the 60 second coast");
    }
    private static void Near(double a, double b, double tolerance, string name)
    { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a - b) > tolerance) throw new Exception($"{name}: {a:R} vs {b:R}"); }
}
