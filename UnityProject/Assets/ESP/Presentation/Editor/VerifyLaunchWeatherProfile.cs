using System;
using ESP.Presentation;
using UnityEngine;

public static class VerifyLaunchWeatherProfile
{
    // Synthetic, local fixture: surface plus two levels, all at one UTC hour.
    private const string Fixture = "{\"elevation\":0,\"hourly\":{\"time\":[\"2026-11-14T12:00\"],\"temperature_2m\":[20],\"surface_pressure\":[1000],\"wind_speed_10m\":[10],\"wind_direction_10m\":[90],\"temperature_1000hPa\":[10],\"wind_speed_1000hPa\":[4],\"wind_direction_1000hPa\":[0],\"geopotential_height_1000hPa\":[100],\"temperature_925hPa\":[0],\"wind_speed_925hPa\":[2],\"wind_direction_925hPa\":[180],\"geopotential_height_925hPa\":[1000]}}";
    public static void Run()
    {
        var now = new DateTime(2026, 11, 14, 12, 30, 0, DateTimeKind.Utc);
        var profile = LaunchWeatherProfile.Parse(Fixture, now);
        Check(profile.Samples.Count == 3 && profile.ValidTimeUtc.Hour == 12, "time and omitted levels");
        Check(profile.TrySample(0, out var surface) && Near(surface.WindEastMps, -10) && Near(surface.WindNorthMps, 0) && Near(surface.PressurePa, 100000), "surface conversion");
        Check(profile.TrySample(100, out var north) && Near(north.WindNorthMps, -4), "north wind conversion");
        Check(profile.TrySample(550, out var mid) && Near(mid.TemperatureK, 278.15) && Near(mid.PressurePa, 96250), "interpolation");
        Check(!profile.TrySample(-1, out _) && !profile.TrySample(1001, out _), "bounded range");
        ExpectFailure(Fixture.Replace("\"surface_pressure\":[1000]", "\"surface_pressure\":[null]"), now, "missing surface");
        ExpectFailure(Fixture, now.AddHours(3), "stale profile");
        Debug.Log("ESP_WEATHER_VERIFICATION_OK");
    }
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("Weather verification failed: " + name); }
    private static void ExpectFailure(string input, DateTime now, string name) { try { LaunchWeatherProfile.Parse(input, now); } catch (FormatException) { return; } throw new Exception("Weather verification failed: " + name); }
}
