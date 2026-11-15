using System;
using System.Collections.Generic;
using System.Globalization;

namespace ESP.Presentation
{
    // The selected MODIS WMS layers advertise date-only daily domains. Never guess
    // dates across gaps, or mistake subdaily/unsupported intervals for daily data.
    public static class NasaImageryTime
    {
        public static string FormatDay(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        static bool Day(string value, out DateTime day) => DateTime.TryParseExact(value,
            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

        static IEnumerable<Tuple<DateTime, DateTime, int>> Spans(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) yield break;
            foreach (string entry in domain.Split(','))
            {
                string[] p = entry.Trim().Split('/');
                if (!Day(p[0], out var first)) continue;
                if (p.Length == 1) { yield return Tuple.Create(first, first, 1); continue; }
                if (p.Length != 3 || !Day(p[1], out var last) || last < first) continue;
                string step = p[2];
                if (step.Length < 3 || step[0] != 'P' || step[step.Length - 1] != 'D' ||
                    !int.TryParse(step.Substring(1, step.Length - 2), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int days) || days <= 0) continue;
                yield return Tuple.Create(first, last, days);
            }
        }

        public static bool Contains(string domain, DateTime day)
        {
            foreach (var span in Spans(domain))
                if (day.Date >= span.Item1 && day.Date <= span.Item2 &&
                    (day.Date - span.Item1).Days % span.Item3 == 0) return true;
            return false;
        }

        public static DateTime? LatestDay(string domain, DateTime nowUtc)
        {
            DateTime? latest = null;
            foreach (var span in Spans(domain))
            {
                DateTime end = span.Item2 < nowUtc.Date ? span.Item2 : nowUtc.Date;
                if (end < span.Item1) continue;
                int days = (end - span.Item1).Days;
                DateTime candidate = span.Item1.AddDays(days - days % span.Item3);
                if (!latest.HasValue || candidate > latest.Value) latest = candidate;
            }
            return latest;
        }

        public static string Describe(DateTime first, DateTime last, DateTime nowUtc)
        {
            int newestDays = Math.Max(0, (nowUtc.Date - last.Date).Days);
            int oldestDays = Math.Max(newestDays, (nowUtc.Date - first.Date).Days);
            string age = "Daily observations: newest " + newestDays + "d / oldest " + oldestDays + "d ago · not live";
            // Application display policy, not a NASA timeliness guarantee. The
            // precise acquisition time is unavailable in these daily WMS rasters.
            return newestDays > 2 ? "STALE — " + age : age;
        }
    }
}
