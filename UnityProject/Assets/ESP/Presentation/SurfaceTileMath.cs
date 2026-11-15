using System;

namespace ESP.Presentation
{
    /// <summary>Pure EPSG:3857 tile addressing. Lat/lon input is degrees; mesh outputs are radians.</summary>
    public static class SurfaceTileMath
    {
        public const double MaximumLatitudeDegrees = 85.05112878;
        public static int ColumnAtLongitude(double longitudeDegrees, int count)
        {
            int x = (int)Math.Floor((longitudeDegrees + 180) / 360 * count);
            return (x % count + count) % count;
        }
        public static int RowAtLatitude(double latitudeDegrees, int count)
        {
            double lat = Math.Max(-MaximumLatitudeDegrees, Math.Min(MaximumLatitudeDegrees, latitudeDegrees)) * Math.PI / 180;
            int y = (int)Math.Floor((1 - Math.Log(Math.Tan(lat) + 1 / Math.Cos(lat)) / Math.PI) * .5 * count);
            return Math.Max(0, Math.Min(count - 1, y));
        }
        public static double LatitudeAtRow(double row, double count) => Math.Atan(Math.Sinh(Math.PI * (1 - 2 * row / count)));
        public static double LongitudeAtColumn(double column, double count) => column / count * 2 * Math.PI - Math.PI;
        public static int WrappedDelta(int x, int center, int count)
        {
            int delta = x - center;
            return delta > count / 2 ? delta - count : delta < -count / 2 ? delta + count : delta;
        }
    }
}
