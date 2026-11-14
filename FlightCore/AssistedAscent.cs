using System;

namespace FlightCore
{
    /// <summary>Approximate ascent guidance using ordinary vehicle controls; create a new instance for each mission.</summary>
    public sealed class AssistedAscent
    {
        private readonly double flattenAltitudeMeters, pitchExponent, targetPerigee;
        public bool HasCutOff { get; private set; }
        public double CutoffMissionTimeSeconds { get; private set; }

        public AssistedAscent(double flattenAltitudeMeters = 160000, double pitchExponent = 2, double targetPerigee = 200000)
        {
            if (!Finite(flattenAltitudeMeters) || flattenAltitudeMeters <= 1000) throw new ArgumentOutOfRangeException(nameof(flattenAltitudeMeters));
            if (!Finite(pitchExponent) || pitchExponent <= 0) throw new ArgumentOutOfRangeException(nameof(pitchExponent));
            if (!Finite(targetPerigee) || targetPerigee < 0) throw new ArgumentOutOfRangeException(nameof(targetPerigee));
            this.flattenAltitudeMeters = flattenAltitudeMeters; this.pitchExponent = pitchExponent; this.targetPerigee = targetPerigee;
        }
        public void Update(RocketSimulation simulation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (HasCutOff) { simulation.Throttle = 0; return; }
            var state = simulation.State;
            if (simulation.BoosterAttached && simulation.BoosterPropellantKg <= 0) simulation.JettisonBoosters();
            var orbit = OrbitDiagnostics.Compute(state.Position, state.Velocity);
            if (!simulation.BoosterBurning && state.AltitudeMeters > 120000 && orbit.IsBound && orbit.MinimumWgs84AltitudeBoundMeters >= targetPerigee)
            {
                HasCutOff = true; CutoffMissionTimeSeconds = state.MissionTime; simulation.Throttle = 0; return;
            }
            double fraction = Math.Max(0, Math.Min(1, 1 - (state.AltitudeMeters - 1000) / (flattenAltitudeMeters - 1000)));
            simulation.PitchDegrees = 90 * Math.Pow(fraction, pitchExponent);
            simulation.HeadingDegrees = 90; simulation.Throttle = 1;
            if (state.PropellantKg <= 0) simulation.Stage();
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
