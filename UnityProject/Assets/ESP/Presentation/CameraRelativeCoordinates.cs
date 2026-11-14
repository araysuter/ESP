using System;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>
    /// Converts ECEF positions in meters to camera-relative Unity coordinates.
    /// ECEF is right-handed: X at latitude 0, longitude 0; Y at latitude 0,
    /// longitude 90 degrees east; Z toward the north pole. The local tangent
    /// basis is east, north, up (right-handed). Unity receives east as X, up as
    /// Y, and north as Z; this axis ordering reverses handedness.
    /// </summary>
    public static class CameraRelativeCoordinates
    {
        /// <summary>
        /// Subtracts the camera ECEF origin in double precision, rotates into
        /// its local tangent frame, and only then casts to Unity floats. Latitude
        /// is geodetic, longitude is east-positive, both in radians. The caller
        /// supplies the origin's latitude and longitude; no Earth model is assumed.
        /// The result is presentation-only and must not become flight state.
        /// </summary>
        public static Vector3 FromEcef(
            double positionX, double positionY, double positionZ,
            double originX, double originY, double originZ,
            double originLatitudeRadians, double originLongitudeRadians)
        {
            RequireFinite(positionX, nameof(positionX));
            RequireFinite(positionY, nameof(positionY));
            RequireFinite(positionZ, nameof(positionZ));
            RequireFinite(originX, nameof(originX));
            RequireFinite(originY, nameof(originY));
            RequireFinite(originZ, nameof(originZ));
            RequireFinite(originLatitudeRadians, nameof(originLatitudeRadians));
            RequireFinite(originLongitudeRadians, nameof(originLongitudeRadians));
            if (originLatitudeRadians < -Math.PI / 2 || originLatitudeRadians > Math.PI / 2)
                throw new ArgumentOutOfRangeException(nameof(originLatitudeRadians));

            double dx = positionX - originX;
            double dy = positionY - originY;
            double dz = positionZ - originZ;
            double sinLat = Math.Sin(originLatitudeRadians);
            double cosLat = Math.Cos(originLatitudeRadians);
            double sinLon = Math.Sin(originLongitudeRadians);
            double cosLon = Math.Cos(originLongitudeRadians);
            double east = -sinLon * dx + cosLon * dy;
            double north = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz;
            double up = cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;
            return new Vector3(ToFloat(east), ToFloat(up), ToFloat(north));
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Coordinate must be finite.");
        }

        private static float ToFloat(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value > float.MaxValue || value < -float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Relative coordinate must fit in a finite float.");
            return (float)value;
        }
    }
}
