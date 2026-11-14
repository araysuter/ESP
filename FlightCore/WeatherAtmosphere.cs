using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlightCore
{
    public readonly struct WeatherLevel
    {
        public double GeometricAltitudeMeters { get; }
        public AtmosphereSample Atmosphere { get; }
        public WeatherLevel(double geometricAltitudeMeters, AtmosphereSample atmosphere)
        {
            if (!Finite(geometricAltitudeMeters) || geometricAltitudeMeters < -500 || geometricAltitudeMeters > StandardAtmosphere.ModelTopGeometricAltitudeMeters)
                throw new ArgumentOutOfRangeException(nameof(geometricAltitudeMeters));
            if (!(atmosphere.TemperatureKelvin > 0) || !(atmosphere.PressurePascals > 0))
                throw new ArgumentException("Weather requires positive temperature and pressure; default samples are invalid.", nameof(atmosphere));
            GeometricAltitudeMeters = geometricAltitudeMeters; Atmosphere = atmosphere;
        }
        /// <summary>Wind direction is meteorological degrees FROM north, clockwise. No vertical wind is inferred.</summary>
        public static WeatherLevel FromMeteorological(double altitudeMeters, double temperatureKelvin, double pressurePascals,
            double windSpeedMetersPerSecond, double windFromDegrees)
        {
            if (!Finite(windSpeedMetersPerSecond) || windSpeedMetersPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(windSpeedMetersPerSecond));
            if (!Finite(windFromDegrees) || windFromDegrees < 0 || windFromDegrees > 360) throw new ArgumentOutOfRangeException(nameof(windFromDegrees));
            double angle = windFromDegrees * Math.PI / 180;
            return new WeatherLevel(altitudeMeters, new AtmosphereSample(temperatureKelvin, pressurePascals,
                new Vector3d(-windSpeedMetersPerSecond * Math.Sin(angle), -windSpeedMetersPerSecond * Math.Cos(angle), 0)));
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Immutable supplied data; fetching/parsing stays outside the flight library.</summary>
    public sealed class WeatherProfile
    {
        public IReadOnlyList<WeatherLevel> Levels { get; }
        public string Provider { get; }
        public DateTime ValidTimeUtc { get; }
        public double LatitudeDegrees { get; }
        public double LongitudeDegrees { get; }
        public WeatherProfile(IEnumerable<WeatherLevel> levels, string provider, DateTime validTimeUtc, double latitudeDegrees, double longitudeDegrees)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("A source label is required.", nameof(provider));
            if (validTimeUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Use an explicit UTC timestamp.", nameof(validTimeUtc));
            if (double.IsNaN(latitudeDegrees) || Math.Abs(latitudeDegrees) > 90 || double.IsNaN(longitudeDegrees) || Math.Abs(longitudeDegrees) > 180)
                throw new ArgumentOutOfRangeException(nameof(latitudeDegrees));
            var copy = new List<WeatherLevel>(levels);
            if (copy.Count < 2) throw new ArgumentException("At least two weather levels are required.", nameof(levels));
            for (int i = 0; i < copy.Count; i++)
            {
                // Revalidate even default(struct) inputs and preserve caller order.
                _ = new WeatherLevel(copy[i].GeometricAltitudeMeters, copy[i].Atmosphere);
                if (i > 0 && copy[i].GeometricAltitudeMeters <= copy[i - 1].GeometricAltitudeMeters)
                    throw new ArgumentException("Weather altitudes must be strictly increasing.", nameof(levels));
            }
            Levels = new ReadOnlyCollection<WeatherLevel>(copy); Provider = provider; ValidTimeUtc = validTimeUtc;
            LatitudeDegrees = latitudeDegrees; LongitudeDegrees = longitudeDegrees;
        }
    }

    /// <summary>
    /// Freezes one vertical column at launch. It is not a 4D weather field.
    /// Inside the profile: temperature/wind linear, pressure log-linear. Outside:
    /// boundary pressure/temperature ratios and wind taper into standard atmosphere.
    /// This extension is a game approximation, not measured weather outside coverage.
    /// </summary>
    public sealed class WeatherAtmosphere
    {
        private readonly WeatherProfile? profile;
        public bool UsesWeather => profile != null;
        public string SourceDescription { get; }
        public const double LowerBlendMeters = 500;
        public const double UpperBlendMeters = 2000;

        public WeatherAtmosphere(WeatherProfile? weather, DateTime launchTimeUtc, TimeSpan maximumAge)
        {
            if (launchTimeUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Use UTC launch time.", nameof(launchTimeUtc));
            if (maximumAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAge));
            if (weather == null) SourceDescription = "Standard atmosphere: weather unavailable";
            else if (weather.ValidTimeUtc > launchTimeUtc) SourceDescription = "Standard atmosphere: weather timestamp is in the future";
            else if (launchTimeUtc - weather.ValidTimeUtc > maximumAge) SourceDescription = "Standard atmosphere: weather expired at launch";
            else { profile = weather; SourceDescription = weather.Provider + " profile frozen at launch; valid " + weather.ValidTimeUtc.ToString("O"); }
        }

        public AtmosphereSample Sample(double geometricAltitudeMeters)
        {
            var baseline = StandardAtmosphere.Sample(geometricAltitudeMeters);
            if (profile == null || baseline.PressurePascals == 0) return baseline;
            var levels = profile.Levels;
            var low = levels[0]; var high = levels[levels.Count - 1];
            if (geometricAltitudeMeters < low.GeometricAltitudeMeters)
                return BlendBoundary(geometricAltitudeMeters, low, LowerBlendMeters, baseline);
            if (geometricAltitudeMeters > high.GeometricAltitudeMeters)
                return BlendBoundary(geometricAltitudeMeters, high, UpperBlendMeters, baseline);
            for (int i = 1; i < levels.Count; i++)
            {
                if (geometricAltitudeMeters > levels[i].GeometricAltitudeMeters) continue;
                var a = levels[i - 1]; var b = levels[i];
                double t = (geometricAltitudeMeters - a.GeometricAltitudeMeters) / (b.GeometricAltitudeMeters - a.GeometricAltitudeMeters);
                return new AtmosphereSample(a.Atmosphere.TemperatureKelvin + t * (b.Atmosphere.TemperatureKelvin - a.Atmosphere.TemperatureKelvin),
                    Math.Exp((1 - t) * Math.Log(a.Atmosphere.PressurePascals) + t * Math.Log(b.Atmosphere.PressurePascals)),
                    a.Atmosphere.WindEastNorthUpMetersPerSecond * (1 - t) + b.Atmosphere.WindEastNorthUpMetersPerSecond * t);
            }
            throw new InvalidOperationException("Weather interpolation bracket not found.");
        }

        private static AtmosphereSample BlendBoundary(double altitude, WeatherLevel boundary, double width, AtmosphereSample baseline)
        {
            double distance = Math.Abs(altitude - boundary.GeometricAltitudeMeters);
            if (distance >= width) return baseline;
            double t = distance / width, weight = 1 - t * t * (3 - 2 * t);
            var boundaryBaseline = StandardAtmosphere.Sample(boundary.GeometricAltitudeMeters);
            double pressure = baseline.PressurePascals * Math.Exp(weight * Math.Log(boundary.Atmosphere.PressurePascals / boundaryBaseline.PressurePascals));
            // Ratio blending remains positive even for unusually cold supplied profiles.
            double temperature = baseline.TemperatureKelvin * Math.Exp(weight * Math.Log(boundary.Atmosphere.TemperatureKelvin / boundaryBaseline.TemperatureKelvin));
            return new AtmosphereSample(temperature, pressure, boundary.Atmosphere.WindEastNorthUpMetersPerSecond * weight);
        }
    }
}
