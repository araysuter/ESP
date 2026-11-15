using System;
using System.Collections.Generic;

namespace FlightCore
{
    /// <summary>An observed authoritative position, not a propagated or reconstructed point.</summary>
    public readonly struct FlightTrailSample
    {
        public double MissionTimeSeconds { get; }
        public Vector3d InertialPositionMeters { get; }
        public FlightTrailSample(double missionTimeSeconds, Vector3d inertialPositionMeters)
        {
            MissionTimeSeconds = missionTimeSeconds;
            InertialPositionMeters = inertialPositionMeters;
        }
    }

    /// <summary>
    /// Bounded actual-flight history. Keeps its first observed position and exact latest
    /// endpoint. On capacity, removes alternating interior samples and increases time/
    /// distance spacing; retained points always originate in observations. No interpolation
    /// or trajectory prediction is performed. Reset when the owning simulation changes.
    /// </summary>
    public sealed class FlightTrail
    {
        public const int MaximumSamples = 1024;
        private readonly List<FlightTrailSample> retained = new List<FlightTrailSample>(MaximumSamples);
        private FlightTrailSample latest;
        private double timeSpacing = .25, distanceSpacing = 200;
        private bool HasSeparateEndpoint => retained.Count > 0 &&
            (latest.MissionTimeSeconds != retained[retained.Count - 1].MissionTimeSeconds ||
             !SamePosition(latest.InertialPositionMeters, retained[retained.Count - 1].InertialPositionMeters));
        public int Count => retained.Count + (HasSeparateEndpoint ? 1 : 0);
        public FlightTrailSample this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return index < retained.Count ? retained[index] : latest;
            }
        }

        public void Record(double missionTimeSeconds, Vector3d inertialPositionMeters)
        {
            if (double.IsNaN(missionTimeSeconds) || double.IsInfinity(missionTimeSeconds) || missionTimeSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(missionTimeSeconds));
            // Rewind is a new history; never draw a line between separate mission epochs.
            if (retained.Count > 0 && missionTimeSeconds < latest.MissionTimeSeconds) Reset();
            latest = new FlightTrailSample(missionTimeSeconds, inertialPositionMeters);
            if (retained.Count == 0) { retained.Add(latest); return; }
            var anchor = retained[retained.Count - 1];
            if (missionTimeSeconds - anchor.MissionTimeSeconds < timeSpacing &&
                (inertialPositionMeters - anchor.InertialPositionMeters).Magnitude() < distanceSpacing) return;
            // Reserve a slot for a provisional current endpoint between committed samples.
            if (retained.Count >= MaximumSamples - 1)
            {
                int write = 1;
                for (int read = 2; read < retained.Count; read += 2) retained[write++] = retained[read];
                retained.RemoveRange(write, retained.Count - write);
                timeSpacing = Math.Min(1e12, timeSpacing * 2);
                distanceSpacing = Math.Min(1e12, distanceSpacing * 2);
            }
            retained.Add(latest);
        }

        /// <summary>All history is rotated at one current Earth epoch, not at sample times.</summary>
        public Vector3d EcefPosition(int index, double currentEarthAngleRadians) =>
            EarthFrames.InertialToEcef(this[index].InertialPositionMeters, currentEarthAngleRadians);

        public void Reset()
        {
            retained.Clear(); latest = default; timeSpacing = .25; distanceSpacing = 200;
        }
        private static bool SamePosition(Vector3d a, Vector3d b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }
}
