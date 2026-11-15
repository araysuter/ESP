using System;
using FlightCore;

namespace ESP.Presentation
{
    /// <summary>
    /// Surface-navball math only; all vectors use local east, north, up, in doubles.
    /// Manual flight supplies a full attitude basis; assisted flight uses a
    /// deterministic zero-bank tangent toward local zenith. Never uses a camera.
    /// </summary>
    public readonly struct NavballProjection
    {
        public readonly double PitchDegrees, HeadingDegrees, SurfaceSpeed;
        public readonly Vector3d Forward, Right, Up, SurfacePrograde;
        public bool HasPrograde => SurfaceSpeed >= 1;

        private NavballProjection(double pitch, double heading, Vector3d velocity, Vector3d? manualForward = null, Vector3d? manualUp = null)
        {
            PitchDegrees = pitch;
            HeadingDegrees = (heading % 360 + 360) % 360;
            double p = pitch * Math.PI / 180, h = heading * Math.PI / 180;
            Forward = manualForward ?? new Vector3d(Math.Cos(p) * Math.Sin(h), Math.Cos(p) * Math.Cos(h), Math.Sin(p));
            Up = manualUp ?? new Vector3d(-Math.Sin(p) * Math.Sin(h), -Math.Sin(p) * Math.Cos(h), Math.Cos(p));
            Right = manualForward.HasValue ? Cross(Forward, Up).Normalize() : new Vector3d(Math.Cos(h), -Math.Sin(h), 0);
            SurfaceSpeed = velocity.Magnitude();
            SurfacePrograde = SurfaceSpeed >= 1 ? velocity * (1 / SurfaceSpeed) : Vector3d.Zero;
        }

        public static NavballProjection Pad(double headingDegrees) => new NavballProjection(90, headingDegrees, Vector3d.Zero);

        public static NavballProjection FromFlight(Vector3d inertialPosition, Vector3d inertialVelocity,
            double missionTime, double actualPitchDegrees, double actualHeadingDegrees, Vector3d? manualAxisInertial = null, Vector3d? manualUpInertial = null)
        {
            double angle = Aerodynamics.EarthAngularVelocityRadiansPerSecond * missionTime;
            Vector3d ecef = EarthFrames.InertialToEcef(inertialPosition, angle);
            GeodeticPosition geo = EarthFrames.EcefToGeodetic(ecef);
            // Subtract rotating ground velocity in INERTIAL coordinates before rotating.
            double omega = Aerodynamics.EarthAngularVelocityRadiansPerSecond;
            Vector3d rotatingGround = new Vector3d(-omega * inertialPosition.Y, omega * inertialPosition.X, 0);
            Vector3d relative = EarthFrames.InertialToEcef(inertialVelocity - rotatingGround, angle);
            double sl = Math.Sin(geo.LongitudeRadians), cl = Math.Cos(geo.LongitudeRadians);
            double sp = Math.Sin(geo.LatitudeRadians), cp = Math.Cos(geo.LatitudeRadians);
            Vector3d enu = new Vector3d(-sl * relative.X + cl * relative.Y,
                -sp * cl * relative.X - sp * sl * relative.Y + cp * relative.Z,
                cp * cl * relative.X + cp * sl * relative.Y + sp * relative.Z);
            Vector3d? axis = null, up = null;
            if (manualAxisInertial.HasValue && manualUpInertial.HasValue)
            {
                var a = EarthFrames.InertialToEcef(manualAxisInertial.Value, angle);
                var u = EarthFrames.InertialToEcef(manualUpInertial.Value, angle);
                axis = new Vector3d(-sl*a.X+cl*a.Y, -sp*cl*a.X-sp*sl*a.Y+cp*a.Z, cp*cl*a.X+cp*sl*a.Y+sp*a.Z);
                up = new Vector3d(-sl*u.X+cl*u.Y, -sp*cl*u.X-sp*sl*u.Y+cp*u.Z, cp*cl*u.X+cp*sl*u.Y+sp*u.Z);
            }
            return new NavballProjection(actualPitchDegrees, actualHeadingDegrees, enu, axis, up);
        }

        /// <summary>Orthographic sphere coordinates: x right, y up, z facing viewer.</summary>
        public Vector3d Project(Vector3d localDirection) => new Vector3d(Dot(localDirection, Right), Dot(localDirection, Up), Dot(localDirection, Forward));
        public Vector3d Unproject(double x, double y, double z) => Right * x + Up * y + Forward * z;
        public static Vector3d Direction(double pitchDegrees, double headingDegrees)
        {
            double p = pitchDegrees * Math.PI / 180, h = headingDegrees * Math.PI / 180;
            return new Vector3d(Math.Cos(p) * Math.Sin(h), Math.Cos(p) * Math.Cos(h), Math.Sin(p));
        }
        private static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    }
}
