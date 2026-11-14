using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Core = FlightCore;

namespace ESP.Presentation
{
    // GEOS-FP native mid-layer H is treated as geopotential height; spherical conversion is approximate.
    public sealed class NasaWeatherProfile : MonoBehaviour
    {
        const string BaseUrl = "https://opendap.nccs.nasa.gov/dods/GEOS-5/fp/0.25_deg/assim/inst3_3d_asm_Nv";
        readonly string[] names = { "h", "pl", "t", "u", "v", "ps" };
        int generation;
        UnityWebRequest active;
        public string Status { get; private set; } = "NASA weather not requested";
        public Core.WeatherProfile Snapshot { get; private set; }
        public double RequestedLatitudeDegrees { get; private set; }
        public double RequestedLongitudeDegrees { get; private set; }
        public void Fetch(double lat, double lon)
        {
            generation++; if (active != null) active.Abort(); Snapshot = null;
            RequestedLatitudeDegrees = lat; RequestedLongitudeDegrees = lon;
            if (!NasaWeatherParser.Finite(lat) || !NasaWeatherParser.Finite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180) { Status = "NASA weather: invalid location"; return; }
            Status = "Loading NASA GEOS-FP analysis"; StartCoroutine(Load(lat, lon, generation));
        }
        IEnumerator Load(double lat, double lon, int id)
        {
            string body = null, error = null;
            yield return Request(BaseUrl + ".das", id, (s, e) => { body = s; error = e; });
            if (id != generation) yield break;
            if (error != null) { Status = "NASA weather unavailable: " + error; yield break; }
            DateTime valid; int index, xi, yi; double glat, glon;
            try { NasaWeatherParser.Metadata(body, DateTime.UtcNow, out valid, out index); yi = Math.Max(0, Math.Min(720, (int)Math.Round((lat + 90) / .25))); xi = (int)Math.Round(((lon + 180) % 360) / .3125) % 1152; glat = -90 + yi * .25; glon = -180 + xi * .3125; }
            catch (Exception e) { Status = "NASA metadata invalid: " + e.Message; yield break; }
            var data = new Dictionary<string, double[]>();
            foreach (string name in names)
            {
                string query = name == "ps" ? $"ps[{index}][{yi}][{xi}]" : $"{name}[{index}][0:1:71][{yi}][{xi}]";
                body = null; error = null;
                yield return Request(BaseUrl + ".ascii?" + query, id, (s, e) => { body = s; error = e; });
                if (id != generation) yield break;
                if (error != null) { Status = "NASA weather unavailable: " + error; yield break; }
                try { data[name] = NasaWeatherParser.Parse(body, name, valid, glat, glon); }
                catch (Exception e) { Status = "NASA response invalid: " + e.Message; yield break; }
            }
            try {
                if (valid > DateTime.UtcNow || DateTime.UtcNow - valid > TimeSpan.FromHours(12)) throw new FormatException("analysis is future dated or stale");
                Snapshot = NasaWeatherParser.Build(data, valid, glat, glon);
                Status = "NASA GEOS-FP " + valid.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) + "; approximate geopotential height conversion";
            } catch (Exception e) { Status = "NASA profile invalid: " + e.Message; }
        }
        IEnumerator Request(string url, int id, Action<string,string> done)
        {
            using (var req = UnityWebRequest.Get(url)) {
                active = req; req.timeout = 15; yield return req.SendWebRequest();
                if (id != generation) yield break;
                active = null;
                if (req.result != UnityWebRequest.Result.Success) { done(null, req.error); yield break; }
                if (req.downloadedBytes > 500000) { done(null, "response too large"); yield break; }
                done(req.downloadHandler.text, null);
            }
        }
        public void Cancel() { generation++; if (active != null) active.Abort(); active = null; Snapshot = null; Status = "NASA weather canceled"; }
        void OnDestroy() { Cancel(); }
    }

    public static class NasaWeatherParser
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
        static double Number(string s) { if (!double.TryParse(s, NumberStyles.Float, Inv, out double n) || !Finite(n)) throw new FormatException("invalid number"); return n; }
        static string Attribute(string block, string name) { var m = Regex.Match(block, @"\b" + name + "\\s+\"([^\"]+)\""); if (!m.Success) throw new FormatException("missing " + name); return m.Groups[1].Value; }
        public static void Metadata(string text, DateTime now, out DateTime valid, out int index)
        {
            var block = Regex.Match(text, @"\btime\s*\{([^}]*)\}"); if (!block.Success) throw new FormatException("missing time metadata");
            var start = DateTime.ParseExact(Attribute(block.Value, "minimum").ToUpperInvariant(), "HH'Z'ddMMMyyyy", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var end = DateTime.ParseExact(Attribute(block.Value, "maximum").ToUpperInvariant(), "HH'Z'ddMMMyyyy", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var stepMatch = Regex.Match(Attribute(block.Value, "grads_step"), @"^(\d+)mn$"); if (!stepMatch.Success) throw new FormatException("invalid step");
            long step = TimeSpan.FromMinutes(int.Parse(stepMatch.Groups[1].Value, Inv)).Ticks;
            if (step <= 0 || end < start || now < start || (end.Ticks - start.Ticks) % step != 0) throw new FormatException("invalid time range");
            long i = Math.Min((end.Ticks - start.Ticks) / step, (now.Ticks - start.Ticks) / step);
            if (i > int.MaxValue) throw new FormatException("invalid index"); index = (int)i; valid = start.AddTicks(i * step);
            if (valid > now || now - valid > TimeSpan.FromHours(12)) throw new FormatException("stale or future analysis");
        }
        public static double[] Parse(string text, string name, DateTime time, double lat, double lon)
        {
            bool surface = name == "ps"; string shape = surface ? "[1][1][1]" : "[1][72][1][1]";
            if (!text.StartsWith(name + ", " + shape, StringComparison.Ordinal)) throw new FormatException("wrong variable or shape");
            string[] parts = Regex.Split(text, @"(?m)^(?=(?:time|lev|lat|lon), \[)");
            var rows = Regex.Matches(parts[0], @"(?m)^\[([^\n]*?)\],\s*([-+0-9.eE]+)"); int count = surface ? 1 : 72;
            if (rows.Count != count) throw new FormatException("incomplete values"); var values = new double[count];
            for (int i = 0; i < count; i++) { if (rows[i].Groups[1].Value != (surface ? "0][0" : $"0][{i}][0")) throw new FormatException("bad data index"); values[i] = Number(rows[i].Groups[2].Value); }
            var maps = new Dictionary<string,double[]>();
            for (int i = 1; i < parts.Length; i++) {
                var m = Regex.Match(parts[i], @"^(time|lev|lat|lon), \[(\d+)\]\s*\n([^\n]+)"); if (!m.Success || maps.ContainsKey(m.Groups[1].Value)) throw new FormatException("bad coordinate map");
                string[] fields = m.Groups[3].Value.Split(','); if (fields.Length != int.Parse(m.Groups[2].Value, Inv)) throw new FormatException("bad coordinate count");
                var v = new double[fields.Length]; for (int j = 0; j < fields.Length; j++) v[j] = Number(fields[j]); maps.Add(m.Groups[1].Value, v);
            }
            Check(maps, "time", (time - new DateTime(1,1,1,0,0,0,DateTimeKind.Utc)).TotalDays + 2); Check(maps, "lat", lat); Check(maps, "lon", lon);
            if (surface) { if (maps.ContainsKey("lev")) throw new FormatException("unexpected levels"); }
            else { if (!maps.TryGetValue("lev", out var levels) || levels.Length != 72) throw new FormatException("missing levels"); for (int i = 0; i < 72; i++) if (levels[i] != i+1) throw new FormatException("mismatched levels"); }
            return values;
        }
        static void Check(Dictionary<string,double[]> maps, string name, double expected) { if (!maps.TryGetValue(name, out var a) || a.Length != 1 || Math.Abs(a[0] - expected) > 1e-5) throw new FormatException("mismatched " + name); }
        public static Core.WeatherProfile Build(Dictionary<string,double[]> d, DateTime valid, double lat, double lon)
        {
            double ps = d["ps"][0]; if (!Finite(ps) || ps <= 10000 || ps >= 120000) throw new FormatException("invalid surface pressure");
            var list = new List<Core.WeatherLevel>();
            for (int i = 0; i < 72; i++) {
                double h=d["h"][i], p=d["pl"][i], t=d["t"][i], u=d["u"][i], v=d["v"][i];
                if (!Finite(h)||!Finite(p)||!Finite(t)||!Finite(u)||!Finite(v)||Math.Abs(h)>=1e14||Math.Abs(p)>=1e14||Math.Abs(t)>=1e14||Math.Abs(u)>=1e14||Math.Abs(v)>=1e14||p<=0||p>ps||t<=100||t>=400||h < -500||h>120000) continue;
                double z=6371000*h/(6371000-h); if (z < -500 || z > Core.StandardAtmosphere.ModelTopGeometricAltitudeMeters) continue;
                list.Add(new Core.WeatherLevel(z, new Core.AtmosphereSample(t,p,new Core.Vector3d(u,v,0))));
            }
            list.Sort((a,b)=>a.GeometricAltitudeMeters.CompareTo(b.GeometricAltitudeMeters));
            if (list.Count < 36) throw new FormatException("insufficient native levels");
            for(int i=1;i<list.Count;i++) if(list[i].GeometricAltitudeMeters<=list[i-1].GeometricAltitudeMeters || list[i].Atmosphere.PressurePascals>=list[i-1].Atmosphere.PressurePascals) throw new FormatException("nonmonotonic profile");
            return new Core.WeatherProfile(list,"NASA GEOS-FP analysis (approximate geopotential height conversion)",valid,lat,lon);
        }
    }
}
