# Pressure-responsive exhaust presentation

`RocketPlume.SetFlow` receives actual engine flow and ambient static pressure
from `RocketFlightDriver`; the existing atmosphere sample, fuel/staging rules,
thrust and simulation remain authoritative. Zero flow immediately disables the
exhaust renderer. Colors, opening angle, brightness and turbulence are cosmetic.

At equal throttle, lower ambient pressure produces an expanding translucent fan.
NASA MSFC's [Rocket Propulsion Fundamentals, slide 15](https://ntrs.nasa.gov/api/citations/20140002716/downloads/20140002716.pdf#page=16)
explains that exhaust continues expanding beyond the nozzle when exit pressure
exceeds ambient pressure. Our pressure response and roughly 26-degree vacuum
half-angle are an artistic approximation, not CFD or a measured Raptor plume.

The existing response `vacuum = 1 - clamp(pressure / 101325)^0.35` blends
continuously from a confined atmospheric jet to a widening vacuum envelope.
Spread uses plume length in meters, independent of nozzle diameter; the vacuum
tail fades optically instead of contracting. The longer vacuum fan retains
brighter short cores and a cooler diffuse tail. Conservative mesh bounds include
the maximum radial spread and ripple. Nozzle anchors retain their imported
positions, directions and measured opening radii.

Starship's manifest supplies 33 booster and six Ship nozzles, including three
larger vacuum bells. Since that importer creates separate components per nozzle,
the runtime counts all nozzles of the same stage/style to reduce overlapping
diffuse brightness by `1 / sqrt(count)`. Near-nozzle cores remain individually
visible. Other presets that group nozzles in one component use the same rule.

## VM verification and further calibration

The revised longer fan compiled and rendered in the VM during an actual
Starship orbital burn at 100% throttle near 237 km. Engine cutoff removed it;
native orbit-camera views retained the exhaust without geometry clipping.
No local compilation or rendering was performed. A controlled equal-camera
pressure sweep is additional calibration, not yet performed: compare the same Ship stage, camera, exposure
and `SetFlow(1, pressure)` at **101325 Pa** and **0 Pa**; use 1000 Pa as an
intermediate state. A presentation-only preview must be labeled; do not report
these pressure overrides as a simulated flight checkpoint.

- At 101325 Pa, expect a compact jet with short shock cells and tapered tail.
- At 0 Pa, expect a much wider translucent fan expanding downstream, with short
  bright cores, softened edges and no sustained opaque tube or laser.
- At 1000 Pa, expect a continuous intermediate fan (`vacuum` about 0.801).
- At full vacuum/full flow, a 12 m base plume has 26.4 m rendered length. The
  nominal radius at halfway is `nozzleRadius + 26.4 * .48 * 0.5^1.05`, roughly
  6.74 m for a .619 m bell or 7.31 m for a 1.192 m bell (before <=3% ripple).
- Inspect the six-nozzle Ship and 33-nozzle booster, side and oblique views, and
  the screen edge; there should be no fan clipping or additive white washout.
- Set flow to zero at each pressure, including after staging and fuel exhaustion;
  all exhaust must disappear. Rehearse a real ascent for continuous pressure
  changes, recording the actual atmosphere source and target build used.
