using System;
using FlightCore;
using ESP.Presentation;

static void Near(double actual, double expected, string what, double tolerance = 1e-9)
{
    if (Math.Abs(actual - expected) > tolerance || !double.IsFinite(actual))
        throw new Exception($"{what}: expected {expected:G17}, got {actual:G17}");
}
static void VectorNear(Vector3d actual, Vector3d expected, string what, double tolerance = 1e-9)
{
    Near(actual.X, expected.X, what + " X", tolerance);
    Near(actual.Y, expected.Y, what + " Y", tolerance);
    Near(actual.Z, expected.Z, what + " Z", tolerance);
}

// A pad sits still relative to the surface, despite ~400 m/s inertial velocity.
// Check both real sites and an equatorial/polar location, across rotating epochs.
foreach (var location in new[] { (25.997, -97.156), (28.608422, -80.604133), (0.0, 0.0), (90.0, 33.0) })
foreach (double time in new[] { 0.0, 1000.0, 43200.0 })
{
    double lat = location.Item1 * Math.PI / 180, lon = location.Item2 * Math.PI / 180;
    Vector3d ecef = EarthFrames.GeodeticToEcef(lat, lon, 125);
    double omega = Aerodynamics.EarthAngularVelocityRadiansPerSecond;
    Vector3d position = EarthFrames.EcefToInertial(ecef, omega * time);
    Vector3d rotation = new Vector3d(-omega * position.Y, omega * position.X, 0);
    var still = NavballProjection.FromFlight(position, rotation, time, 90, 90);
    Near(still.SurfaceSpeed, 0, "stationary pad surface speed");
    if (still.HasPrograde) throw new Exception("Stationary pad has undefined prograde");

    // Synthesize known ENU velocity, transform it into ECI, and recover it.
    // Aerodynamics defines the same documented ENU->ECI transform as the core.
    Vector3d enu = new Vector3d(123, -47, 81);
    Vector3d relative = Aerodynamics.WindToInertial(enu, lat, lon + omega * time);
    var moving = NavballProjection.FromFlight(position, rotation + relative, time, 30, 75);
    // Longitude is intrinsically undefined at the exact pole; use a non-pole
    // fixture for ENU direction recovery, while still testing stationary speed above.
    if (Math.Abs(location.Item1) < 90)
        VectorNear(moving.SurfacePrograde * moving.SurfaceSpeed, enu, "rotating ECI->surface ENU", 1e-7);
}

// Instrument center is the ACTUAL nose direction, not target pitch or camera.
foreach (double pitch in new[] { -80.0, 0.0, 30.0, 89.999, 90.0 })
foreach (double heading in new[] { 0.0, 90.0, 180.0, 270.0, 359.9 })
{
    Vector3d position = EarthFrames.GeodeticToEcef(.5, -.9, 10000);
    var attitude = NavballProjection.FromFlight(position, Vector3d.Zero, 0, pitch, heading);
    VectorNear(attitude.Project(NavballProjection.Direction(pitch, heading)), new Vector3d(0, 0, 1), "nose centered");
    VectorNear(attitude.Project(attitude.Right), new Vector3d(1, 0, 0), "right-handed heading screen direction");
    VectorNear(attitude.Project(attitude.Up), new Vector3d(0, 1, 0), "zero-bank up screen direction");
    Vector3d sample = new Vector3d(.3, -.4, Math.Sqrt(.75));
    VectorNear(attitude.Project(attitude.Unproject(sample.X, sample.Y, sample.Z)), sample, "sphere projection round trip");
}
var vertical = NavballProjection.Pad(90);
VectorNear(vertical.Project(new Vector3d(0, 0, 1)), new Vector3d(0, 0, 1), "prelaunch zenith centered");
var north = NavballProjection.FromFlight(EarthFrames.GeodeticToEcef(0, 0, 0), Vector3d.Zero, 0, 0, 0);
VectorNear(north.Project(new Vector3d(1, 0, 0)), new Vector3d(1, 0, 0), "east is right when heading north");
VectorNear(north.Project(new Vector3d(0, 0, 1)), new Vector3d(0, 1, 0), "zenith above horizon");
Console.WriteLine("Navball frame, rotation, attitude, singularity and projection checks passed.");
