using System;
using FlightCore;

internal static class Program
{
    private static int Main()
    {
        try
        {
            InvalidGuidance();
            foreach (string vehicle in new[] { "Starship", "SLS" })
            foreach (var site in new[] { ("LC39A", 28.608422, -80.604133), ("LC39B", 28.627306, -80.620869), ("South Texas", 25.997, -97.156) })
            {
                var a = Run(vehicle, site.Item1, site.Item2, site.Item3, .02);
                var b = Run(vehicle, site.Item1, site.Item2, site.Item3, .01);
                Near(a.time, b.time, 1, vehicle + " " + site.Item1 + " cutoff time convergence");
                Near(a.bound, b.bound, 2000, vehicle + " " + site.Item1 + " bound convergence");
            }
            Console.WriteLine("Vehicle mission tests passed."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static (double time, double bound) Run(string vehicle, string site, double lat, double lon, double step)
    {
        var atmosphere = new WeatherAtmosphere(null, new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
        var stages = vehicle == "SLS" ? RealVehiclePresets.BuildSlsStages() : RealVehiclePresets.BuildStarshipStages();
        var booster = vehicle == "SLS" ? RealVehiclePresets.BuildSlsBoosterParameters() : null;
        var rocket = new RocketSimulation(step, stages, atmosphere, lat * Math.PI / 180, lon * Math.PI / 180, booster: booster);
        var guidance = new AssistedAscent(vehicle == "SLS" ? 120000 : 220000, vehicle == "SLS" ? 3 : 2);
        rocket.Launch(); double minimum = double.PositiveInfinity;
        for (int i = 0; i < 2500 / step; i++)
        {
            guidance.Update(rocket); var s = rocket.State;
            if (!double.IsFinite(s.MissionTime) || !double.IsFinite(s.AltitudeMeters) || !double.IsFinite(s.PropellantKg) || s.PropellantKg < 0 || s.IsLanded)
                throw new Exception($"{vehicle} {site} dt={step}: invalid state or landed at {s.MissionTime:F2}s, altitude={s.AltitudeMeters:F1}m, stage={s.StageIndex}, fuel={s.PropellantKg:F1}kg");
            if (guidance.HasCutOff)
            {
                var orbit = OrbitDiagnostics.Compute(s.Position, s.Velocity);
                if (!orbit.IsBound || !double.IsFinite(orbit.MinimumWgs84AltitudeBoundMeters) || orbit.MinimumWgs84AltitudeBoundMeters < 180000)
                    throw new Exception($"{vehicle} {site} dt={step}: coast perigee bound {orbit.MinimumWgs84AltitudeBoundMeters:F1}m");
                if (minimum == double.PositiveInfinity && orbit.MinimumWgs84AltitudeBoundMeters < 200000)
                    throw new Exception($"{vehicle} {site} dt={step}: cutoff below 200 km");
                minimum = Math.Min(minimum, orbit.MinimumWgs84AltitudeBoundMeters);
                if (s.MissionTime >= guidance.CutoffMissionTimeSeconds + 60)
                {
                    if (s.StageIndex != 1 || rocket.BoosterAttached) throw new Exception($"{vehicle} {site}: unexpected staging state");
                    Console.WriteLine($"{vehicle} {site} dt={step}: cutoff={guidance.CutoffMissionTimeSeconds:F2}s, coast minimum={minimum:F1}m");
                    return (guidance.CutoffMissionTimeSeconds, minimum);
                }
            }
            rocket.Step();
        }
        throw new Exception($"{vehicle} {site} dt={step}: no completed coast; time={rocket.State.MissionTime:F2}s, stage={rocket.State.StageIndex}, fuel={rocket.State.PropellantKg:F1}kg");
    }
    private static void InvalidGuidance()
    {
        Throws(() => new AssistedAscent(1000)); Throws(() => new AssistedAscent(double.NaN));
        Throws(() => new AssistedAscent(160000, 0)); Throws(() => new AssistedAscent(160000, double.PositiveInfinity));
        Throws(() => new AssistedAscent(160000, 1, -1));
    }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid guidance accepted"); }
    private static void Near(double a, double b, double tolerance, string name)
    { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a-b) > tolerance) throw new Exception($"{name}: {a:R} vs {b:R}"); }
}
