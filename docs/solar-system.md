# Solar-system sandbox

The map and flight backdrop include Mercury, Venus, Mars, Jupiter, Saturn, Uranus,
Neptune, Pluto, Ceres, Eris, Haumea and Makemake. Use SOLAR SYSTEM and scroll outward
for the outer planets. Bodies use real SI radii, geocentric positions and primary-relative
flight telemetry. This is a patched-gravity sandbox, not an interplanetary mission planner.

## Data and approximations

The bundled daily position/velocity tables are geometric Earth-centered J2000/ICRF
NASA/JPL Horizons vectors in SI units, covering 2020-01-01 through 2037-01-01 inclusive.
Cubic Hermite interpolation provides position, velocity and moving-primary acceleration.
See [Horizons API](https://ssd-api.jpl.nasa.gov/doc/horizons.html) and the regeneration
script. Planet tables have no invented out-of-range ephemeris: unavailable bodies are
hidden and omitted from gravity/collision; the map displays an availability notice.
Earth and Moon retain their existing approximate fallback. Flights crossing the table
endpoint are outside the supported planetary simulation period.

Daily tables resolve sandbox encounters, not precision navigation. An independent
half-day Horizons check at 2026-11-15 12:00 UTC measured position errors from about
50 m to 17.385 km and velocity errors below 0.059 m/s. Mercury was worst in position;
Jupiter and Pluto also exceed 5 km. The 25 km / 1 m/s regression bounds in the verifier
are deliberately loose corruption/regression checks at this one epoch, **not global
accuracy guarantees**. Raw reference states and request URLs are in
`data/planet-reference-check.json`. Higher-cadence tables are needed for precision flybys.

SOIs use the existing Earth/Moon limits and the Laplace mass-ratio approximation for
planets. Position/velocity are continuous at primary switches; acceleration is piecewise,
not N-body dynamics. No planetary atmospheres, winds, terrain or rings collision are
added. Gas/ice giants have spherical collision boundaries at the configured mean radius;
this is an explicit gameplay approximation, not a solid surface. Body masses, radii,
rotations and pole models are approximate; no landing accuracy is claimed.

Textures are user-supplied NASA imagery: the user confirmed nasa.gov origin and permission
to use on November 15, 2026. Exact upstream item URLs were not supplied. The manifest
records source hashes, resize sizes and that confirmation; it does not claim independent
per-image license verification. Credit NASA; see [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/).
Ceres, Eris, Haumea and Makemake maps are illustrative and labeled as such in the UI.
Venus uses the cloud image with the surface image retained as a fallback. Saturn's
original annular mesh uses the supplied transparent radial strip and an illustrative
27-degree tilt, not a measured pole. Rings are unlit and do not cast shadows.
The Sun image retains the existing bright material.

Planet flight backdrops are camera-relative, with distant centers compressed to a
500-unit shell and radius scaled identically to preserve angular size. Nearby bodies
retain true scale. Earth/Moon rendering is unchanged. Map orbit lines show available
arcs, not fabricated complete outer-system orbits. Texture orientation and distant
occlusion remain visual approximations requiring rendered review.

## Verification

Studio CLI: all 14 C# suites passed (13 existing plus Planet.Tests); temporary test
copies use the installed .NET 8 SDK without changing repository net9 targets. Planet
checks cover interpolation, table bounds, all primary boundaries, moving encounters,
finite interior trial gravity and swept collisions. Independent online comparisons
passed the documented one-epoch bounds for all 12 tables. The original 5 km candidate
bound failed for Mercury, Jupiter and Pluto; the table accuracy limitation is retained
explicitly above rather than described as fixed.

The pure core and presentation compile using Unity's installed C# compiler/references.
`-espPlanetSmoke` verifies packaged resources and ring/backdrop geometry; the same checks
are wired into `-espReleaseSmoke` so future package rehearsals fail if assets are missing.
These new packaged checks, shader compilation and rendered appearance still require a
fresh Unity package build. No locked-screen GUI session was operated during this work.
