using System;
using System.Globalization;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    public static class NasaImageryTimeChecks
    {
        public static void Validate()
        {
            var now = new DateTime(2026, 11, 15, 3, 0, 0, DateTimeKind.Utc);
            string gaps = "2026-11-01/2026-11-10/P1D,2026-11-14,2026-11-16/2026-11-20/P1D";
            Require(NasaImageryTime.LatestDay(gaps, now) == now.Date.AddDays(-1), "Future interval or gap selected");
            Require(!NasaImageryTime.Contains(gaps, now.Date), "Unadvertised gap accepted");
            string cadence = "2026-11-01/2026-11-20/P3D";
            Require(NasaImageryTime.LatestDay(cadence, now) == new DateTime(2026, 11, 13), "Interval cadence ignored");
            Require(!NasaImageryTime.Contains(cadence, new DateTime(2026, 11, 14)), "Off-cadence date accepted");
            Require(NasaImageryTime.LatestDay("2026-11-14/2026-11-20/P1D", now) == now.Date, "UTC current day missing");
            foreach (string invalid in new[] { null, "", "garbage", "2026-11-15/2026-11-14/P1D", "2026-11-01/2026-11-20/P0D", "2026-11-01/2026-11-20/PT1H" })
                Require(!NasaImageryTime.LatestDay(invalid, now).HasValue, "Invalid/unsupported domain accepted");
            Require(!NasaImageryTime.Describe(now.Date.AddDays(-2), now.Date, now).StartsWith("STALE"), "Current observations marked stale");
            Require(NasaImageryTime.Describe(now.Date.AddDays(-2), now.Date, now.AddDays(3)).StartsWith("STALE"), "Retained data never ages");
            Require(NasaImageryTime.Describe(now.Date.AddDays(-2), now.Date, now).Contains("newest 0d / oldest 2d"), "Blended day range hidden");
            NonGregorianCulture(now);
            Debug.Log("ESP_NASA_IMAGERY_TIME_CHECKS_OK gaps future cadence malformed aging UTC dates nonGregorianCulture");
        }
        static void NonGregorianCulture(DateTime now)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
                Require(CultureInfo.CurrentCulture.DateTimeFormat.Calendar is ThaiBuddhistCalendar, "Fixture must use a non-Gregorian calendar");
                var latest = NasaImageryTime.LatestDay("2026-11-14/2026-11-20/P1D", now);
                Require(latest.HasValue && NasaImageryTime.FormatDay(latest.Value) == "2026-11-15", "NASA day formatted using local calendar");
                var parsed = DateTime.ParseExact(NasaImageryTime.FormatDay(latest.Value), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                Require(parsed == now.Date && NasaImageryTime.Contains("2026-11-14/2026-11-20/P1D", parsed), "Formatted selection must round-trip through Gregorian parsing");
                Require(NasaImageryTime.FormatDay(now.AddDays(-2)) == "2026-11-13", "Composite WMS day formatted using local calendar");
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
