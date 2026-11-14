using System;

namespace FlightCore
{
    /// <summary>
    /// U.S. Standard Atmosphere 1976 lower atmosphere, with zero wind. Uses
    /// geopotential layers through 84,852 m (NASA-TM-X-74335 / NASA 19770009539).
    /// Above that boundary an illustrative exponential pressure extension is
    /// smoothly tapered to zero at 120 km; this is not a thermosphere model.
    /// Input is geometric altitude in meters relative to the model's sea level.
    /// </summary>
    public static class StandardAtmosphere
    {
        private const double Radius = 6356766.0;
        private const double G = 9.80665;
        private const double R = 287.05287;
        private const double TopGeopotential = 84852.0;
        public const double ModelTopGeometricAltitudeMeters = Radius * TopGeopotential / (Radius - TopGeopotential);
        public const double VacuumGeometricAltitudeMeters = 120000.0;
        public const double MinimumGeometricAltitudeMeters = -500.0;
        private static readonly double[] Heights = { 0, 11000, 20000, 32000, 47000, 51000, 71000, TopGeopotential };
        private static readonly double[] Lapses = { -0.0065, 0, 0.001, 0.0028, 0, -0.0028, -0.002 };

        public static AtmosphereSample Sample(double geometricAltitudeMeters)
        {
            if (double.IsNaN(geometricAltitudeMeters) || double.IsInfinity(geometricAltitudeMeters) || geometricAltitudeMeters < MinimumGeometricAltitudeMeters)
                throw new ArgumentOutOfRangeException(nameof(geometricAltitudeMeters));
            double h = geometricAltitudeMeters < ModelTopGeometricAltitudeMeters
                ? Radius * geometricAltitudeMeters / (Radius + geometricAltitudeMeters) : TopGeopotential;
            double temperature = 288.15;
            double pressure = 101325.0;
            for (int i = 0; i < Lapses.Length; i++)
            {
                double end = Math.Min(h, Heights[i + 1]);
                double delta = end - Heights[i];
                double nextTemperature = temperature + Lapses[i] * delta;
                pressure *= Lapses[i] == 0
                    ? Math.Exp(-G * delta / (R * temperature))
                    : Math.Pow(nextTemperature / temperature, -G / (R * Lapses[i]));
                temperature = nextTemperature;
                if (h <= Heights[i + 1]) break;
            }
            if (geometricAltitudeMeters >= VacuumGeometricAltitudeMeters)
                pressure = 0;
            else if (geometricAltitudeMeters > ModelTopGeometricAltitudeMeters)
            {
                double delta = geometricAltitudeMeters - ModelTopGeometricAltitudeMeters;
                // Isothermal scale height evaluated at the top of the lower model.
                double scaleHeight = R * temperature / G;
                double x = delta / (VacuumGeometricAltitudeMeters - ModelTopGeometricAltitudeMeters);
                double taper = 1 - x * x * (3 - 2 * x);
                pressure *= Math.Exp(-delta / scaleHeight) * taper;
            }
            return new AtmosphereSample(temperature, pressure, Vector3d.Zero);
        }
    }
}
