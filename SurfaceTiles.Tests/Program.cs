using System;
using ESP.Presentation;
using FlightCore;

static class Program
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Main()
    {
        const double rad = Math.PI / 180;
        const int n = 1 << 16;
        // Independent endpoint coordinates verified by the VM against USGS imagery.
        Require(SurfaceTileMath.ColumnAtLongitude(-80.604133, n) == 18094, "Cape longitude tile");
        Require(SurfaceTileMath.RowAtLatitude(28.608422, n) == 27329, "Cape latitude tile");
        foreach (int zoom in new[] { 8, 12, 16 })
        {
            int count = 1 << zoom;
            foreach (double latitude in new[] { -85.0, -45.0, 0.0, 25.997, 28.608422, 84.0 })
                foreach (double longitude in new[] { -179.999, -97.156, 0.0, 179.999 })
                {
                    int x = SurfaceTileMath.ColumnAtLongitude(longitude, count), y = SurfaceTileMath.RowAtLatitude(latitude, count);
                    double north = SurfaceTileMath.LatitudeAtRow(y, count), south = SurfaceTileMath.LatitudeAtRow(y + 1, count);
                    double west = SurfaceTileMath.LongitudeAtColumn(x, count), east = SurfaceTileMath.LongitudeAtColumn(x + 1, count);
                    Require(latitude * rad <= north + 1e-12 && latitude * rad >= south - 1e-12, "Latitude inside addressed tile");
                    Require(longitude * rad >= west - 1e-12 && longitude * rad <= east + 1e-12, "Longitude inside addressed tile");
                    Require(north > south && east > west, "North-up texture orientation");
                    var sw = EarthFrames.GeodeticToEcef(south, west, 0);
                    var nw = EarthFrames.GeodeticToEcef(north, west, 0);
                    var ne = EarthFrames.GeodeticToEcef(north, east, 0);
                    var up = new Vector3d(Math.Cos(south) * Math.Cos(west), Math.Cos(south) * Math.Sin(west), Math.Sin(south));
                    // Unity ENU -> east/up/north reverses handedness; SW,NW,NE must
                    // be inward in ECEF to become outward front faces in Unity.
                    var a = nw - sw; var b = ne - sw;
                    var cross = new Vector3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
                    Require(cross.X * up.X + cross.Y * up.Y + cross.Z * up.Z < 0, "Curved surface triangle winding");
                    Require(Math.Abs(EarthFrames.EcefToGeodetic(ne).AltitudeMeters) < .001, "Mesh vertex on WGS84");
                }
            Require(SurfaceTileMath.ColumnAtLongitude(180, count) == 0, "Dateline wraps east");
            Require(SurfaceTileMath.ColumnAtLongitude(-180, count) == 0, "Dateline wraps west");
            Require(SurfaceTileMath.WrappedDelta(0, count - 1, count) == 1, "Neighbor east across dateline");
            Require(SurfaceTileMath.WrappedDelta(count - 1, 0, count) == -1, "Neighbor west across dateline");
            Require(SurfaceTileMath.RowAtLatitude(90, count) == 0 && SurfaceTileMath.RowAtLatitude(-90, count) == count - 1, "Mercator polar limits");
        }
        Console.WriteLine("PASS surface tile addressing, orientation, dateline, WGS84 geometry and winding");
    }
}
