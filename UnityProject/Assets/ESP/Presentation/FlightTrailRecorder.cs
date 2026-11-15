using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Lives on the always-active director, so flight-view map deactivation cannot lose history.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FlightTrailRecorder : MonoBehaviour
    {
        public FlightTrail History { get; } = new FlightTrail();
        private MissionDirector director;
        private RocketSimulation recordedSimulation;
        public void Initialize(MissionDirector owner) { director = owner; }

        private void Update()
        {
            var driver = director != null ? director.FlightDriver as RocketFlightDriver : null;
            RecordSimulation(driver != null ? driver.Simulation : null);
        }

        // The launch adapter calls this immediately after constructing its simulation,
        // before any Step, to retain the exact authoritative initial pad state.
        public void RecordSimulation(RocketSimulation simulation)
        {
            if (!ReferenceEquals(recordedSimulation, simulation))
            {
                History.Reset();
                recordedSimulation = simulation;
            }
            if (simulation == null) return;
            var state = simulation.State;
            History.Record(state.MissionTime, state.Position);
        }
    }
}
