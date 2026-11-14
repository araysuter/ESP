using System;

namespace FlightCore
{
    /// <summary>Atmospheric state in SI units; wind components are east, north, up.</summary>
    public readonly struct AtmosphereSample
    {
        public double TemperatureKelvin { get; }
        public double PressurePascals { get; }
        public double DensityKgPerCubicMeter { get; }
        public Vector3d WindEastNorthUpMetersPerSecond { get; }

        public AtmosphereSample(double temperatureKelvin, double pressurePascals, Vector3d windEastNorthUpMetersPerSecond)
        {
            if (double.IsNaN(temperatureKelvin) || double.IsInfinity(temperatureKelvin) || temperatureKelvin <= 0)
                throw new ArgumentOutOfRangeException(nameof(temperatureKelvin));
            if (double.IsNaN(pressurePascals) || double.IsInfinity(pressurePascals) || pressurePascals < 0)
                throw new ArgumentOutOfRangeException(nameof(pressurePascals));
            TemperatureKelvin = temperatureKelvin;
            PressurePascals = pressurePascals;
            DensityKgPerCubicMeter = pressurePascals / (287.05287 * temperatureKelvin);
            if (double.IsNaN(DensityKgPerCubicMeter) || double.IsInfinity(DensityKgPerCubicMeter))
                throw new ArgumentOutOfRangeException(nameof(pressurePascals), "Density exceeds finite numeric range.");
            WindEastNorthUpMetersPerSecond = windEastNorthUpMetersPerSecond;
        }
    }
}
