using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    public readonly struct WeatherSample
    {
        public readonly double AltitudeMSL, TemperatureK, PressurePa, WindEastMps, WindNorthMps;
        public WeatherSample(double altitudeMSL, double temperatureK, double pressurePa, double windEastMps, double windNorthMps)
        { AltitudeMSL = altitudeMSL; TemperatureK = temperatureK; PressurePa = pressurePa; WindEastMps = windEastMps; WindNorthMps = windNorthMps; }
    }

    public sealed class WeatherSnapshot
    {
        private readonly ReadOnlyCollection<WeatherSample> samples;
        public IReadOnlyList<WeatherSample> Samples => samples;
        public DateTime ValidTimeUtc { get; }
        public bool IsCurrent(DateTime nowUtc) => nowUtc >= ValidTimeUtc && nowUtc - ValidTimeUtc <= TimeSpan.FromHours(2);
        internal WeatherSnapshot(DateTime time, List<WeatherSample> values) { ValidTimeUtc = time; samples = new List<WeatherSample>(values).AsReadOnly(); }
        public bool TrySample(double altitudeMSL, out WeatherSample sample)
        {
            sample = default;
            if (double.IsNaN(altitudeMSL) || double.IsInfinity(altitudeMSL) || samples.Count == 0 || altitudeMSL < samples[0].AltitudeMSL || altitudeMSL > samples[samples.Count - 1].AltitudeMSL) return false;
            for (int i = 0; i < samples.Count; i++)
            {
                if (altitudeMSL == samples[i].AltitudeMSL) { sample = samples[i]; return true; }
                if (samples[i].AltitudeMSL > altitudeMSL)
                {
                    var a = samples[i - 1]; var b = samples[i]; double t = (altitudeMSL - a.AltitudeMSL) / (b.AltitudeMSL - a.AltitudeMSL);
                    sample = new WeatherSample(altitudeMSL, a.TemperatureK + t * (b.TemperatureK - a.TemperatureK), a.PressurePa + t * (b.PressurePa - a.PressurePa), a.WindEastMps + t * (b.WindEastMps - a.WindEastMps), a.WindNorthMps + t * (b.WindNorthMps - a.WindNorthMps)); return true;
                }
            }
            return false;
        }
    }

    public sealed class LaunchWeatherProfile : MonoBehaviour
    {
        private static readonly int[] Levels = { 1000, 925, 850, 700, 500, 300, 200, 100, 50 };
        private int generation;
        private UnityWebRequest active;
        public string Status { get; private set; } = "No weather profile requested";
        public WeatherSnapshot Snapshot { get; private set; }
        public DateTime? ValidTimeUtc => Snapshot?.ValidTimeUtc;
        public bool TrySample(double altitudeMSL, out WeatherSample sample) { sample = default; return Snapshot != null && Snapshot.TrySample(altitudeMSL, out sample); }
        public void Fetch(double lat, double lon)
        {
            generation++; if (active != null) active.Abort();
            Snapshot = null;
            if (!Finite(lat) || !Finite(lon) || lat < -90 || lat > 90 || lon < -180 || lon > 180) { Status = "Invalid location; no profile available"; return; }
            Status = "Loading modeled weather profile"; StartCoroutine(Load(lat, lon, generation));
        }
        private IEnumerator Load(double lat, double lon, int id)
        {
            var fields = new List<string> { "temperature_2m", "surface_pressure", "wind_speed_10m", "wind_direction_10m" };
            foreach (int p in Levels) foreach (string name in new[] { "temperature", "wind_speed", "wind_direction", "geopotential_height" }) fields.Add(name + "_" + p + "hPa");
            string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(CultureInfo.InvariantCulture) + "&longitude=" + lon.ToString(CultureInfo.InvariantCulture) + "&hourly=" + string.Join(",", fields) + "&wind_speed_unit=ms&timezone=UTC&past_days=1&forecast_days=1";
            using (var req = UnityWebRequest.Get(url))
            {
                active = req; req.timeout = 20; yield return req.SendWebRequest();
                if (id != generation) yield break;
                active = null;
                if (req.result != UnityWebRequest.Result.Success) { Status = "Weather unavailable; no profile available"; yield break; }
                WeatherSnapshot result;
                try { result = Parse(req.downloadHandler.text, DateTime.UtcNow); }
                catch (Exception e) { Status = "Weather response invalid; no profile available"; Debug.LogWarning("Weather profile: " + e.Message); yield break; }
                Snapshot = result; Status = "Open-Meteo modeled profile valid " + result.ValidTimeUtc.ToString("yyyy-MM-dd HH:mm 'UTC'") + "; " + result.Samples.Count + " levels";
            }
        }
        public static WeatherSnapshot Parse(string json, DateTime now)
        {
            var root = JObject.Parse(json); var h = root["hourly"] as JObject; var times = h?["time"] as JArray;
            if (times == null) throw new FormatException("Missing times");
            int index = -1; DateTime chosen = DateTime.MinValue;
            for (int i = 0; i < times.Count; i++) if (DateTime.TryParseExact((string)times[i], "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) && t <= now && t > chosen) { chosen = t; index = i; }
            if (index < 0 || now - chosen > TimeSpan.FromHours(2)) throw new FormatException("No recent valid past hour");
            var values = new List<WeatherSample>();
            double elevation = Number(root["elevation"]);
            Add(values, elevation, NumberAt(h, "temperature_2m", index), NumberAt(h, "surface_pressure", index) * 100, NumberAt(h, "wind_speed_10m", index), NumberAt(h, "wind_direction_10m", index));
            foreach (int p in Levels)
            {
                string suffix = "_" + p + "hPa";
                try { double altitude = NumberAt(h, "geopotential_height" + suffix, index); if (altitude > elevation) Add(values, altitude, NumberAt(h, "temperature" + suffix, index), p * 100, NumberAt(h, "wind_speed" + suffix, index), NumberAt(h, "wind_direction" + suffix, index)); }
                catch (FormatException) { /* Missing level is omitted, never substituted with zero. */ }
            }
            values.Sort((a, b) => a.AltitudeMSL.CompareTo(b.AltitudeMSL));
            for (int i = 1; i < values.Count; i++) if (values[i].AltitudeMSL <= values[i - 1].AltitudeMSL) throw new FormatException("Duplicate altitude");
            if (values.Count < 2) throw new FormatException("Insufficient profile levels");
            return new WeatherSnapshot(chosen, values);
        }
        private static void Add(List<WeatherSample> values, double altitude, double celsius, double pressure, double speed, double direction)
        {
            if (!Finite(altitude) || !Finite(celsius) || !Finite(pressure) || !Finite(speed) || !Finite(direction) || celsius <= -273.15 || pressure <= 0 || speed < 0 || direction < 0 || direction > 360) throw new FormatException("Invalid sample");
            double radians = direction * Math.PI / 180; values.Add(new WeatherSample(altitude, celsius + 273.15, pressure, -speed * Math.Sin(radians), -speed * Math.Cos(radians)));
        }
        private static double NumberAt(JObject obj, string key, int index) { var arr = obj[key] as JArray; if (arr == null || index >= arr.Count) throw new FormatException("Missing " + key); return Number(arr[index]); }
        private static double Number(JToken value) { if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)) throw new FormatException("Missing number"); double n = (double)value; if (!Finite(n)) throw new FormatException("Nonfinite number"); return n; }
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private void OnDestroy() { generation++; if (active != null) active.Abort(); }
    }
}
