# Day and night

`SolarLighting` computes one offline, double-precision Sun direction in ECEF.
It uses the [USNO approximate solar coordinates](https://aa.usno.navy.mil/faq/sun_approx)
and a Greenwich mean sidereal angle. UTC approximates UT1 and terrestrial time;
nutation, atmospheric refraction, solar angular size and penumbra are omitted.
This is visual lighting, not a navigation ephemeris or a weather measurement.

Before launch the clock follows UTC. `RocketFlightDriver.LaunchUtc` freezes the
launch epoch; thereafter lighting uses that epoch plus actual simulated mission
seconds, including time acceleration and simulation backlog. Reset returns to
live UTC. Camera/view changes do not reset the clock. The flight telemetry labels
the UTC clock LIVE or SIMULATED. Historical satellite imagery retains its own date.

The map transforms ECEF into Unity X,Z,Y. The pad and orbital backdrop transform
the same vector into the fixed launch-site east,up,north basis. Light rays point
opposite the vector toward the Sun. The backdrop light remains full strength while
a forward-ray WGS84 intersection suppresses direct light on an eclipsed vehicle.
The analytic shadow includes altitude: a vehicle above the night-side limb may
still receive sunlight. Low ambient fill preserves readability at night; sky
color blends through approximate twilight and fades to space with altitude.
Ground imagery now uses a lit runtime material; the orbit-line template stays unlit.
Cloud fraction remains observational data and is not changed by the lighting.

Numerical checks are part of `dotnet run --project FlightCore.Tests -c Release`:
equinox noon/midnight, opposite longitude, both solstices, polar day/night, westward
solar motion and surface/orbital/oblate-Earth shadow cases. Run builds and Unity
review on the shared compute machine, with an explicit editor lease. Review map,
both pads, and the high-altitude backdrop, including reset and view switches.

## Verification — November 14, 2026

The Mac Studio's Apple Silicon macOS VM passed the solar and gravity/orbit suite
using the Unity-bundled .NET 8 SDK (only the isolated test copy targeted net8;
repository tests remain net9). All three GitHub numerical CI workflows passed the
implementation commit. Unity 6000.6.9f1 built the macOS ARM development player.

Run that player with `-espDayNightSmoke` to capture deterministic equinox day/night
map and both-pad views, then fly the demo vehicle through the actual simulator to
150 km and capture the high-altitude Earth view. The harness checks accelerated
mission UTC, pause freezing and reset to live UTC. Its lighting-only timestamp
overrides are visibly labeled; they are not historical flight checkpoints.
Windows and Intel player validation remain unperformed. The VM's UI clipping at
1440x900 is tracked separately from lighting; review captures also use 3000x1800.
