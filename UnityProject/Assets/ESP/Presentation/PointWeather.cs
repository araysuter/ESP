using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    /// <summary>Display-only modeled weather at a user-selected geographic point.</summary>
    public sealed class PointWeather : MonoBehaviour
    {
        public string Status { get; private set; } = "Select a point for modeled weather (Open-Meteo).";
        public string Details { get; private set; } = "";
        public string validtime { get; private set; } = "";

        [Serializable] private sealed class Response { public Current current; public Units current_units; public float latitude, longitude; }
        [Serializable] private sealed class Current
        {
            public string time; public int interval;
            public float temperature_2m, relative_humidity_2m, precipitation, pressure_msl, wind_speed_10m, wind_direction_10m;
        }
        [Serializable] private sealed class Units
        {
            public string temperature_2m, relative_humidity_2m, precipitation, pressure_msl, wind_speed_10m, wind_direction_10m;
        }
        private sealed class Entry { public string details, time; public DateTime fetched; }
        private readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        private int generation;
        private float nextRequestTime;

        public void Fetch(double latitude, double longitude)
        {
            int requestGeneration = ++generation;
            Details = validtime = "";
            if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude < -90 || latitude > 90 ||
                double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude < -180 || longitude > 180)
            {
                Status = "Invalid geographic coordinates.";
                return;
            }
            string lat = latitude.ToString("F2", CultureInfo.InvariantCulture);
            string lon = longitude.ToString("F2", CultureInfo.InvariantCulture);
            string key = lat + "," + lon;
            if (cache.TryGetValue(key, out Entry entry) && DateTime.UtcNow - entry.fetched < TimeSpan.FromMinutes(10))
            {
                Details = entry.details; validtime = entry.time;
                Status = "Modeled weather — Open-Meteo (cached).";
                return;
            }
            Status = "Loading modeled weather — Open-Meteo…";
            StartCoroutine(Load(lat, lon, key, requestGeneration));
        }

        private IEnumerator Load(string lat, string lon, string key, int requestGeneration)
        {
            while (Time.realtimeSinceStartup < nextRequestTime)
            {
                if (requestGeneration != generation) yield break;
                yield return null;
            }
            if (requestGeneration != generation) yield break;
            nextRequestTime = Time.realtimeSinceStartup + 2f;
            string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat + "&longitude=" + lon +
                "&current=temperature_2m,relative_humidity_2m,precipitation,pressure_msl,wind_speed_10m,wind_direction_10m&wind_speed_unit=ms&timezone=GMT&cell_selection=nearest";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (requestGeneration != generation) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Status = "Modeled weather unavailable — Open-Meteo.";
                    yield break;
                }
                Response response;
                try { response = JsonUtility.FromJson<Response>(request.downloadHandler.text); }
                catch (Exception) { response = null; }
                if (response == null || response.current == null || response.current_units == null || string.IsNullOrEmpty(response.current.time))
                {
                    Status = "Modeled weather response unavailable — Open-Meteo.";
                    yield break;
                }
                var currentJson=System.Text.RegularExpressions.Regex.Match(request.downloadHandler.text,"\"current\"\\s*:\\s*\\{([^}]*)\\}").Groups[1].Value;
                bool complete=true;
                foreach(var field in new[]{"temperature_2m","relative_humidity_2m","precipitation","pressure_msl","wind_speed_10m","wind_direction_10m"})
                    complete &= System.Text.RegularExpressions.Regex.IsMatch(currentJson,"\""+field+"\"\\s*:\\s*-?[0-9]");
                if(!complete){Status="Weather fields missing — no zero values assumed";yield break;}
                Current c = response.current; Units u = response.current_units;
                validtime = c.time + " UTC ("+c.interval+"s interval)";
                Details = string.Format(CultureInfo.InvariantCulture,
                    "{0:F1} {1} · humidity {2:F0} {3} · interval precip {4:F1} {5}\nPressure {6:F1} {7} · wind {8:F1} {9} from {10:F0}{11}",
                    c.temperature_2m, u.temperature_2m, c.relative_humidity_2m, u.relative_humidity_2m,
                    c.precipitation, u.precipitation, c.pressure_msl, u.pressure_msl,
                    c.wind_speed_10m, u.wind_speed_10m, c.wind_direction_10m, u.wind_direction_10m);
                if(cache.Count>=128)cache.Clear();
                Debug.Log("ESP_POINT_WEATHER_OK "+key+" "+validtime);
                Details+="\nModel grid "+response.latitude.ToString("F2",CultureInfo.InvariantCulture)+", "+response.longitude.ToString("F2",CultureInfo.InvariantCulture);
                cache[key] = new Entry { details = Details, time = validtime, fetched = DateTime.UtcNow };
                Status = "Modeled weather — Open-Meteo.";
            }
        }
    }
}
