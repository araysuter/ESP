namespace FlightCore
{
    /// <summary>
    /// Immutable instantaneous SI telemetry from the simulation's shared force evaluation.
    /// Gravity, drag and net acceleration are magnitudes. Net acceleration is the
    /// free-flight gravity + thrust/mass + drag/mass sum, excluding pad/ground support.
    /// Atmosphere values are the actual frozen launch profile or its standard fallback.
    /// </summary>
    public readonly struct RocketPhysicsTelemetry
    {
        public double GravityMetersPerSecondSquared { get; }
        public double PressurePascals { get; }
        public double DensityKgPerCubicMeter { get; }
        public double DragForceNewtons { get; }
        public double ThrustNewtons { get; }
        public double NetAccelerationMetersPerSecondSquared { get; }
        public double AirSpeedMetersPerSecond { get; }
        /// <summary>Remaining active-stage, attached-solid and future-stage propellant; excludes discarded stages.</summary>
        public double TotalPropellantKg { get; }
        public double TemperatureKelvin { get; }
        public double DynamicPressurePascals { get; }

        internal RocketPhysicsTelemetry(double gravity,double pressure,double density,double drag,double thrust,
            double acceleration,double airSpeed,double totalPropellant,double temperature,double dynamicPressure)
        {
            GravityMetersPerSecondSquared=gravity;PressurePascals=pressure;DensityKgPerCubicMeter=density;
            DragForceNewtons=drag;ThrustNewtons=thrust;NetAccelerationMetersPerSecondSquared=acceleration;
            AirSpeedMetersPerSecond=airSpeed;TotalPropellantKg=totalPropellant;
            TemperatureKelvin=temperature;DynamicPressurePascals=dynamicPressure;
        }
    }
}
