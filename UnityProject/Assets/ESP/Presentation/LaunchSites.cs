namespace ESP.Presentation
{
    /// <summary>Representative launch locations; not surveyed pad coordinates.</summary>
    public sealed class LaunchSite
    {
        public string Id { get; }
        public string Name { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public double ElevationMeters { get; }
        public double HeadingDegrees { get; }

        public LaunchSite(string id, string name, double latitude, double longitude, double elevationMeters, double headingDegrees)
        {
            Id = id; Name = name; Latitude = latitude; Longitude = longitude;
            ElevationMeters = elevationMeters; HeadingDegrees = headingDegrees;
        }
    }

    public static class LaunchSites
    {
        // Coordinates and elevation are approximate, heading is a provisional due-east launch direction.
        public static readonly LaunchSite[] All =
        {
            new LaunchSite("starbase", "Starbase, Texas (approximate)", 25.997, -97.156, 0, 90),
            new LaunchSite("kennedy", "Kennedy LC-39A (approximate)", 28.608422, -80.604133, 0, 90)
        };
    }
}
