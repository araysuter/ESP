using System;

namespace FlightCore
{
    /// <summary>Aggregate of parallel, identical solid boosters. Constant axial thrust and Isp are an approximation, not a certified motor curve.</summary>
    public sealed class SolidBoosterDefinition
    {
        public double DryMassKg { get; }
        public double PropellantKg { get; }
        public double ThrustNewtons { get; }
        public double IspSeconds { get; }
        public double MassFlowKgPerSecond { get; }
        public double BurnSeconds => PropellantKg / MassFlowKgPerSecond;

        public SolidBoosterDefinition(double dryMassKg, double propellantKg, double thrustNewtons, double ispSeconds)
        {
            Positive(dryMassKg, nameof(dryMassKg));
            Positive(propellantKg, nameof(propellantKg));
            Positive(thrustNewtons, nameof(thrustNewtons));
            Positive(ispSeconds, nameof(ispSeconds));
            double flow = thrustNewtons / (ispSeconds * 9.80665);
            Positive(flow, nameof(ispSeconds));
            Positive(propellantKg / flow, nameof(propellantKg));
            if (double.IsInfinity(dryMassKg + propellantKg)) throw new ArgumentOutOfRangeException(nameof(propellantKg));
            DryMassKg = dryMassKg; PropellantKg = propellantKg; ThrustNewtons = thrustNewtons; IspSeconds = ispSeconds; MassFlowKgPerSecond = flow;
        }

        private static void Positive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(name);
        }
    }
}
