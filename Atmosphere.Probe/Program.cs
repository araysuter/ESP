using System.Globalization;
using System.Text.Json;
using FlightCore;

try
{
    if (args.Length != 1 && !(args.Length == 3 && args[1] == "--at"))
        throw new ArgumentException("Usage: Atmosphere.Probe <profile.json> [--at YYYY-MM-DDTHH:mm:ssZ]");
    DateTime at = DateTime.UtcNow;
    bool historical = args.Length == 3;
    if (historical && (!args[2].EndsWith("Z", StringComparison.Ordinal) || !DateTime.TryParse(args[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out at)))
        throw new ArgumentException("--at requires an explicit ISO UTC time ending in Z.");
    using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
    var root = doc.RootElement;
    string source = root.GetProperty("provider").GetString() ?? throw new FormatException("Missing provider");
    string timeText = root.GetProperty("validTimeUtc").GetString() ?? throw new FormatException("Missing valid time");
    if (!timeText.EndsWith("Z", StringComparison.Ordinal) || !DateTime.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var valid)) throw new FormatException("Valid time must be UTC Z.");
    double latitude = root.TryGetProperty("gridLatitudeDegrees", out var gridLat) ? gridLat.GetDouble() : root.GetProperty("latitudeDegrees").GetDouble();
    double longitude = root.TryGetProperty("gridLongitudeDegrees", out var gridLon) ? gridLon.GetDouble() : root.GetProperty("longitudeDegrees").GetDouble();
    var levels = new List<WeatherLevel>();
    foreach (var level in root.GetProperty("levels").EnumerateArray())
    {
        var wind = new Vector3d(level.GetProperty("windEastMetersPerSecond").GetDouble(), level.GetProperty("windNorthMetersPerSecond").GetDouble(), 0);
        levels.Add(new WeatherLevel(level.GetProperty("geometricAltitudeMeters").GetDouble(), new AtmosphereSample(level.GetProperty("temperatureKelvin").GetDouble(), level.GetProperty("pressurePascals").GetDouble(), wind)));
    }
    var profile = new WeatherProfile(levels, source, valid, latitude, longitude);
    var selected = new WeatherAtmosphere(profile, at, TimeSpan.FromHours(12));
    var velocity = new Vector3d(0, 0, 300);
    var points = new List<object>();
    foreach (double altitude in new double[] { 0, 1000, 10000, 30000 })
    {
        var baseline = StandardAtmosphere.Sample(altitude);
        var atmosphere = selected.Sample(altitude);
        var drag = Aerodynamics.Compute(velocity, atmosphere.WindEastNorthUpMetersPerSecond, atmosphere.DensityKgPerCubicMeter, 0.5, 4, 10000);
        var baseDrag = Aerodynamics.Compute(velocity, baseline.WindEastNorthUpMetersPerSecond, baseline.DensityKgPerCubicMeter, 0.5, 4, 10000);
        points.Add(new { altitudeMeters = altitude, densityKgPerCubicMeter = atmosphere.DensityKgPerCubicMeter, airSpeedMetersPerSecond = drag.AirSpeedMetersPerSecond, dynamicPressurePascals = drag.DynamicPressurePascals, dragForceNewtons = Vec(drag.ForceNewtons), dragAccelerationMetersPerSecondSquared = Vec(drag.AccelerationMetersPerSecondSquared), baselineForceNewtons = Vec(baseDrag.ForceNewtons), baselineDensityKgPerCubicMeter = baseline.DensityKgPerCubicMeter });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { purpose = "Synthetic local ENU force probe; not a simulated trajectory", mode = historical ? "historical" : "current UTC", provider = source, sourceValidTimeUtc = valid, evaluatedAtUtc = at, ageMinutes = (at - valid).TotalMinutes, latitudeDegrees = latitude, longitudeDegrees = longitude, usesWeather = selected.UsesWeather, fallbackReason = selected.UsesWeather ? null : selected.SourceDescription, vehicleVelocityEnuMetersPerSecond = new[] { 0, 0, 300 }, dragCoefficient = 0.5, referenceAreaSquareMeters = 4, massKilograms = 10000, points }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("Invalid profile or arguments: " + e.Message);
    return 1;
}
static double[] Vec(Vector3d v) => new[] { v.X, v.Y, v.Z };
