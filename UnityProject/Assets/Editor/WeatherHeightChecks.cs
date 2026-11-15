using System;
using ESP.Presentation;
using UnityEngine;

public static class WeatherHeightChecks
{
    // Run in batch mode with -executeMethod WeatherHeightChecks.Validate.
    public static void Validate()
    {
        const string json = @"{
          'elevation':10,
          'hourly':{
            'time':['2026-11-14T12:00'],
            'temperature_2m':[20], 'surface_pressure':[1010],
            'wind_speed_10m':[2], 'wind_direction_10m':[90],
            'geopotential_height_50hPa':[20000], 'temperature_50hPa':[-50],
            'wind_speed_50hPa':[10], 'wind_direction_50hPa':[180]
          }
        }";
        var snapshot = LaunchWeatherProfile.Parse(json, new DateTime(2026, 11, 14, 12, 30, 0, DateTimeKind.Utc));
        if (snapshot.Samples.Count != 2 || Math.Abs(snapshot.Samples[0].AltitudeMSL - 10) > 1e-9 ||
            Math.Abs(snapshot.Samples[1].AltitudeMSL - 20062.982) > .01 ||
            Math.Abs(snapshot.Samples[1].PressurePa - 5000) > 1e-9)
            throw new Exception("Open-Meteo geopotential height conversion failed");
        Debug.Log("ESP_WEATHER_HEIGHT_CHECKS_OK");
    }
}
