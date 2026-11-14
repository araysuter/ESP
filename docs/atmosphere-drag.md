# Atmosphere, weather, and drag contract

This module is independent of Unity, networking, and the orbit integrator. It adds no thrust, propellant use, staging, terrain collision, or orbit-success logic. It is supplied as separate files so the flight team can integrate it without replacing their simulator.

## Units and frames

All quantities are double precision and SI. `StandardAtmosphere.Sample` and `WeatherAtmosphere.Sample` take geometric height in meters above the model sea level. The caller must compute this from its geodetic/frame model; subtracting an arbitrary spherical radius from inertial position is not a WGS84 altitude. Geoid-vs-ellipsoid height differences are currently an explicit approximation at the atmosphere boundary.

`AtmosphereSample` supplies kelvin, pascals, dry-air density in kg/m³, and local **east, north, up** wind in X, Y, Z. Unity's local east/up/north ordering differs: convert deliberately. `WindToInertial` takes geodetic latitude and the site's longitude in the inertial frame, both in radians. With simplified Earth rotation, inertial longitude is Earth-fixed longitude plus Earth rotation angle at the simulation time. Inertial +Z is the north rotation pole. Epoch/rotation angle remain the flight core's responsibility.

For inertial state, `CorotatingAirVelocity(position, wind)` adds omega × r to the inertial wind. Do not use it on Earth-fixed velocity or add Earth rotation twice. Earth angular speed is the conventional WGS84 7.292115e-5 rad/s.

```csharp
// Profile built from validated external data before the mission, or null offline.
var atmosphere = new WeatherAtmosphere(profile, launchUtc, TimeSpan.FromHours(2));
// Evaluate every simulation force stage with the corresponding state/time.
var sample = atmosphere.Sample(geometricAltitudeMeters);
var wind = Aerodynamics.WindToInertial(sample.WindEastNorthUpMetersPerSecond,
    geodeticLatitudeRadians, longitudeInertialRadians);
var airVelocity = Aerodynamics.CorotatingAirVelocity(inertialPosition, wind);
var drag = Aerodynamics.Compute(inertialVelocity, airVelocity,
    sample.DensityKgPerCubicMeter, dragCoefficient, referenceAreaSquareMeters, currentMassKilograms);
// Add drag.AccelerationMetersPerSecondSquared to other accelerations.
// Track max Q from drag.DynamicPressurePascals and expose the atmosphere source.
```

Drag opposes velocity relative to the air. Its magnitude is `0.5 * density * airSpeed² * Cd * referenceArea`; force is newtons, acceleration is m/s², and dynamic pressure is pascals. The caller supplies a compatible Cd/reference-area pair; no empirical vehicle coefficients or Mach curve are invented. Lift, aerodynamic torque, gusts, turbulence, humidity and heating are omitted. The formula follows [NASA Glenn's drag equation](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/drag-equation/).

**Integrator handoff:** drag depends on velocity, unlike point-mass gravity. Do not assume the gravity-only velocity-Verlet implementation remains second-order when a drag acceleration is dropped into it unchanged. Use a method suitable for velocity-dependent acceleration (or a verified split drag update), evaluate forces at its required stages, and run full-trajectory timestep-convergence tests. Ground contact must cancel appropriate forces while a vehicle remains on the pad. A free vehicle initially moving with the rotating ground has zero baseline air-relative speed, before wind is added.

## Offline atmosphere

`StandardAtmosphere` implements the seven lower layers of the [U.S. Standard Atmosphere, 1976](https://ntrs.nasa.gov/citations/19770009539), converting geometric altitude to geopotential altitude with reference radius 6,356,766 m. Sea-level temperature is 288.15 K, pressure 101325 Pa, gravity 9.80665 m/s², and the dry-air gas constant 287.05287 J/(kg K). Layer boundaries are geopotential heights 0, 11, 20, 32, 47, 51, 71 and 84.852 km.

The supported lower limit is -500 m. Above the last layer (about 86 km geometric), a **game approximation** extends pressure exponentially at constant top temperature and tapers it smoothly to vacuum at 120 km. This is not the 1976 high-altitude model. It cannot model thermospheric variability, solar-storm density changes, or long-term satellite drag. No forces are inferred from NASA imagery or DONKI event labels.

## Weather snapshots

`WeatherProfile` copies at least two strictly ascending `WeatherLevel`s, with provider, UTC validity time, and source column latitude/longitude. Numbers must already be SI with geometric heights; parse fill values and units in the provider adapter. For Open-Meteo, multiply hPa by 100 and add 273.15 to Celsius; its geopotential height requires a documented conversion/approximation. `WeatherLevel.FromMeteorological` converts wind **from** degrees clockwise from north into east/north components. It does not infer vertical wind. The existing Unity adapter already converts wind and units; avoid converting them twice.

`WeatherAtmosphere` accepts a profile only when its validity time is no later than launch and no older than the caller's maximum age. Missing, future, and stale data select standard atmosphere with an explicit `SourceDescription`. The chosen data are frozen for the mission; there are no network calls or wall-clock reads in force evaluation. Preserve provider/time/grid metadata with recorded missions. A single column does not represent weather along a global trajectory; restrict its geographic use in the integration layer.

Within the supplied levels, temperature and wind interpolate linearly; pressure interpolates logarithmically. Outside them, boundary pressure/temperature ratios and wind taper into standard atmosphere over 500 m below and 2 km above. This is an explicitly approximate extension, useful where grid elevation differs slightly from the provisional pad elevation. It is not weather observed outside the profile. Density always follows p/(R T), maintaining internal consistency. Invalid numeric inputs fail rather than becoming zero-valued weather.

NASA GIBS provides imagery, not the numerical wind profile used here. Existing current numerical weather is Open-Meteo modeled data. An experimental NASA GEOS-FP adapter and a real-data force probe are included; see nasa-weather-provider.md for cadence, freshness policy and the height-datum approximation. Provider identity must remain visible whichever data source is used.

## Verification

Run `dotnet run --project Atmosphere.Tests -c Release` with .NET 9, plus the existing `FlightCore.Tests`. The independent console tests cover published atmosphere layer values, layer/taper continuity, validation, known drag magnitude, force direction, rotating-air/ENU transforms, density changes, immutable weather, freshness selection, interpolation, and weather blend continuity. Tests use synthetic weather; they do not validate a provider's forecast accuracy or a full rocket flight.

The added workflow runs both projects for PRs targeting `flight-core` or `main`, and pushes to `flight-core`. Existing flight/core files and test runner are unchanged.
