using System;
using FlightCore;

namespace FlightCore.Tests
{
    internal static class SolarLightingTests
    {
        public static void Run()
        {
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            DateTime Utc(int month, int day, int hour) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc);
            var noon = SolarLighting.DirectionEcef(Utc(3, 20, 12));
            var midnight = SolarLighting.DirectionEcef(Utc(3, 20, 0));
            Check(Math.Abs(noon.Magnitude() - 1) < 1e-12, "Sun unit vector");
            Check(SolarLighting.ElevationDegrees(noon, 0, 0) > 87, "Equinox Greenwich noon");
            Check(SolarLighting.ElevationDegrees(midnight, 0, 0) < -87, "Equinox Greenwich midnight");
            Check(SolarLighting.ElevationDegrees(noon, 0, Math.PI) < -87, "Opposite longitude is night");
            var june = SolarLighting.DirectionEcef(Utc(6, 21, 12));
            var december = SolarLighting.DirectionEcef(Utc(12, 21, 12));
            Check(Math.Abs(Math.Asin(june.Z) * 180 / Math.PI - 23.44) < .1, "June declination");
            Check(Math.Abs(Math.Asin(december.Z) * 180 / Math.PI + 23.44) < .1, "December declination");
            Check(SolarLighting.ElevationDegrees(june, Math.PI / 2, 0) > 23, "Polar summer");
            Check(SolarLighting.ElevationDegrees(december, Math.PI / 2, 0) < -23, "Polar winter");
            var eastSun = SolarLighting.DirectionEcef(Utc(3, 20, 6));
            Check(eastSun.Y > .99, "Sun moves west in ECEF as time advances");
            var sun = new Vector3d(1, 0, 0);
            double radius = EarthFrames.SemiMajorAxisMeters;
            Check(!SolarLighting.IsOccluded(new Vector3d(radius, 0, 0), sun), "Surface daytime");
            Check(SolarLighting.IsOccluded(new Vector3d(-radius, 0, 0), sun), "Surface nighttime");
            Check(SolarLighting.IsOccluded(new Vector3d(-radius - 200000, 0, 0), sun), "Orbital eclipse");
            Check(!SolarLighting.IsOccluded(new Vector3d(-100000, radius + 200000, 0), sun), "Above-limb sunlight despite negative local elevation");
            Check(!SolarLighting.IsOccluded(new Vector3d(0, 0, 6365000), sun), "Polar ray clears oblate Earth");
            try { SolarLighting.DirectionEcef(DateTime.SpecifyKind(Utc(3, 20, 12), DateTimeKind.Unspecified)); throw new Exception("Accepted unspecified time"); }
            catch (ArgumentException) { }
            Console.WriteLine("Solar lighting: equinox, solstices, polar day/night, rotation, WGS84 eclipse passed.");
        }
    }
}
