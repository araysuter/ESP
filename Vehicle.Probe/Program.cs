using System;
using System.Text.Json;
using FlightCore;

internal static class Program
{
    private static void Main(string[] args)
    {
        double step = args.Length > 0 ? double.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 0.1;
        foreach (string vehicle in new[] { "Starship", "SLS" })
        foreach (var site in new[] { ("LC39A", 28.608422, -80.604133), ("South Texas", 25.997, -97.156) })
        foreach (double flatten in new[] { 80000.0, 120000, 160000, 220000, 300000, 400000, 500000 })
        foreach (double exponent in new[] { 0.5, 1.0, 1.5, 2.0, 3.0 })
            Run(vehicle, site.Item1, site.Item2, site.Item3, step, flatten, exponent);
    }
    private static void Run(string vehicle, string site, double latitude, double longitude, double step, double flatten, double exponent)
    {
        var weather = new WeatherAtmosphere(null, new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
        var stages = vehicle == "SLS" ? RealVehiclePresets.BuildSlsStages() : RealVehiclePresets.BuildStarshipStages();
        var booster = vehicle == "SLS" ? RealVehiclePresets.BuildSlsBoosterParameters() : null;
        var rocket = new RocketSimulation(step, stages, weather, latitude * Math.PI / 180, longitude * Math.PI / 180, booster: booster);
        rocket.Throttle = 1; rocket.HeadingDegrees = 90; rocket.Launch();
        double maxAltitude = 0, bestBound = double.NegativeInfinity, coastMin = double.PositiveInfinity, cutoffTime = -1;
        bool cutoff = false, coastBound = true;
        for (int i = 0; i < 2500 / step; i++)
        {
            var s = rocket.State;
            maxAltitude = Math.Max(maxAltitude, s.AltitudeMeters);
            var orbit = OrbitDiagnostics.Compute(s.Position, s.Velocity);
            if (orbit.IsBound && double.IsFinite(orbit.MinimumWgs84AltitudeBoundMeters)) bestBound = Math.Max(bestBound, orbit.MinimumWgs84AltitudeBoundMeters);
            if (!cutoff && s.AltitudeMeters > 120000 && orbit.IsBound && orbit.MinimumWgs84AltitudeBoundMeters >= 200000)
            { cutoff = true; cutoffTime = s.MissionTime; rocket.Throttle = 0; }
            if (cutoff)
            {
                if (!orbit.IsBound || !double.IsFinite(orbit.MinimumWgs84AltitudeBoundMeters)) coastBound = false;
                else coastMin = Math.Min(coastMin, orbit.MinimumWgs84AltitudeBoundMeters);
                if (s.MissionTime >= cutoffTime + 60 || s.IsLanded) break;
            }
            else
            {
                double fraction = Math.Clamp(1 - (s.AltitudeMeters - 1000) / (flatten - 1000), 0, 1);
                rocket.PitchDegrees = 90 * Math.Pow(fraction, exponent);
                if (rocket.BoosterAttached && rocket.BoosterPropellantKg <= 0) rocket.JettisonBoosters();
                if (s.PropellantKg <= 0) rocket.Stage();
            }
            if (s.IsLanded) break;
            rocket.Step();
        }
        var end = rocket.State;
        bool success = cutoff && coastBound && end.MissionTime >= cutoffTime + 60 && coastMin >= 180000 && !end.IsLanded;
        Console.WriteLine(JsonSerializer.Serialize(new { vehicle, site, step, flatten, exponent, success, landed = end.IsLanded,
            elapsed = end.MissionTime, maxAltitude, stage = end.StageIndex, fuel = end.PropellantKg,
            boosterAttached = rocket.BoosterAttached, boosterFuel = rocket.BoosterPropellantKg,
            bestBound = double.IsFinite(bestBound) ? (double?)bestBound : null,
            coastMin = double.IsFinite(coastMin) ? (double?)coastMin : null, cutoffTime = cutoff ? (double?)cutoffTime : null }));
    }
}
