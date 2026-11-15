using System;

namespace FlightCore
{
    /// <summary>Approximate ascent guidance using ordinary vehicle controls; create a new instance for each mission.</summary>
    public sealed class AssistedAscent
    {
        private readonly double flattenAltitudeMeters, pitchExponent, targetPerigee;
        private readonly bool protectAscent;
        private bool upperBurnStarted;
        public bool HasCutOff { get; private set; }
        public bool IsCoasting { get; private set; }
        public double CutoffMissionTimeSeconds { get; private set; }

        public AssistedAscent(double flattenAltitudeMeters = 160000, double pitchExponent = 2, double targetPerigee = 200000, bool protectAscent = false)
        {
            if (!Finite(flattenAltitudeMeters) || flattenAltitudeMeters <= 1000) throw new ArgumentOutOfRangeException(nameof(flattenAltitudeMeters));
            if (!Finite(pitchExponent) || pitchExponent <= 0) throw new ArgumentOutOfRangeException(nameof(pitchExponent));
            if (!Finite(targetPerigee) || targetPerigee < 0) throw new ArgumentOutOfRangeException(nameof(targetPerigee));
            this.flattenAltitudeMeters = flattenAltitudeMeters; this.pitchExponent = pitchExponent; this.targetPerigee = targetPerigee; this.protectAscent = protectAscent;
        }
        public void Update(RocketSimulation simulation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            IsCoasting = false;
            if (HasCutOff) { simulation.Throttle = 0; return; }
            var state = simulation.State;
            if (simulation.BoosterAttached && simulation.BoosterPropellantKg <= 0) simulation.JettisonBoosters();
            if (!simulation.BoosterBurning && state.AltitudeMeters > 120000 && (!protectAscent || state.StageIndex > 0))
            {
                var orbit = OrbitDiagnostics.Compute(state.Position, state.Velocity);
                if (orbit.IsBound && orbit.MinimumWgs84AltitudeBoundMeters >= targetPerigee)
                {
                    HasCutOff = true;
                    CutoffMissionTimeSeconds = state.MissionTime;
                    simulation.Throttle = 0;
                    return;
                }
            }
            double fraction = Math.Max(0, Math.Min(1, 1 - (state.AltitudeMeters - 1000) / (flattenAltitudeMeters - 1000)));
            double commandedPitch = 90 * Math.Pow(fraction, pitchExponent);
            bool coastToApogee = false;
            if (protectAscent && state.AltitudeMeters >= 30000)
            {
                // Geocentric vertical feedback with centripetal feedforward.
                // The controller changes only the normal pitch, throttle and stage
                // commands, leaving the finite-slew flight dynamics authoritative.
                var p = state.Position; var v = state.Velocity;
                double radius = p.Magnitude();
                double radialSpeed = (p.X * v.X + p.Y * v.Y + p.Z * v.Z) / radius;
                double speedSquared = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
                double horizontalSpeed = Math.Sqrt(Math.Max(0, speedSquared - radialSpeed * radialSpeed));
                double targetVerticalSpeed = Math.Max(-200, Math.Min(300, (210000 - state.AltitudeMeters) / 300));
                double requiredAcceleration = Gravity.EarthGravitationalParameter / (radius * radius) - horizontalSpeed * horizontalSpeed / radius +
                    (targetVerticalSpeed - radialSpeed) / 60;
                simulation.Throttle = 1;
                double acceleration = simulation.Physics.ThrustNewtons / state.MassKg;
                if (acceleration > 1e-6)
                    commandedPitch = Math.Asin(Math.Max(-.26, Math.Min(.71, requiredAcceleration / acceleration))) * 180 / Math.PI;
                // Coast the upper stage toward its projected apogee before the
                // circularization burn. Once descending or near apogee, burn.
                if (state.StageIndex > 0 && !upperBurnStarted)
                {
                    var orbit = OrbitDiagnostics.Compute(p, v);
                    coastToApogee = radialSpeed > 40 && orbit.IsBound &&
                        orbit.ApogeeRadiusMeters - EarthFrames.SemiMajorAxisMeters >= 240000 &&
                        orbit.ApogeeRadiusMeters - radius > 20000;
                    if (!coastToApogee) upperBurnStarted = true;
                }
                // Only separate the core after the SRBs have gone and the state
                // has sufficient horizontal speed and a high enough projected apoapsis.
                // Stage() removes any remaining core propellant with that stage.
                if (state.StageIndex == 0 && !simulation.BoosterAttached && horizontalSpeed >= 7200)
                {
                    var projected = OrbitDiagnostics.Compute(p, v);
                    if (projected.IsBound && projected.ApogeeRadiusMeters - EarthFrames.SemiMajorAxisMeters >= 240000)
                        simulation.Stage();
                }
            }
            simulation.PitchDegrees = commandedPitch;
            IsCoasting = coastToApogee;
            simulation.HeadingDegrees = 90; simulation.Throttle = coastToApogee ? 0 : 1;
            if (state.PropellantKg <= 0) simulation.Stage();
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
