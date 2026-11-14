using System;

namespace FlightCore
{
    /// <summary>Fictional demo guidance using ordinary vehicle controls; create a new instance for each mission.</summary>
    public sealed class AssistedAscent
    {
        public bool HasCutOff { get; private set; }
        public double CutoffMissionTimeSeconds { get; private set; }

        public void Update(RocketSimulation simulation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (HasCutOff) { simulation.Throttle = 0; return; }
            var state = simulation.State;
            var orbit = OrbitDiagnostics.Compute(state.Position, state.Velocity);
            if (state.AltitudeMeters > 120000 && orbit.IsBound && orbit.MinimumWgs84AltitudeBoundMeters >= 200000)
            {
                HasCutOff = true;
                CutoffMissionTimeSeconds = state.MissionTime;
                simulation.Throttle = 0;
                return;
            }
            double fraction = Math.Max(0, Math.Min(1, 1 - (state.AltitudeMeters - 1000) / 159000));
            simulation.PitchDegrees = 90 * fraction * fraction;
            simulation.HeadingDegrees = 90;
            simulation.Throttle = 1;
            if (state.PropellantKg <= 0) simulation.Stage();
        }
    }
}
