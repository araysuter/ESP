# Representative launch sites

`LaunchSites.All` offers approximate Starbase, Texas (25.997° N, 97.156° W) and Kennedy LC-39B (28.627306° N, 80.620869° W) locations. [NASA identifies LC-39B as the SLS launch site](https://www.nasa.gov/humans-in-space/exploration-ground-systems/mobile-launcher/). The Kennedy coordinates are NASA's [Apollo 10 postflight trajectory reference](https://www.nasa.gov/wp-content/uploads/static/history/afj/ap10fj/pdf/as-505-postflight-trajectory.pdf), which states the Fischer Ellipsoid 1960 datum; they are used without datum conversion as an approximate WGS84 display/flight location, not as a modern survey. Starbase coordinates are also approximate. Both elevations are provisional 0 m and both headings are provisional 90° clockwise from north. They do not establish a real launch trajectory or permission to use a facility.

The bundled historical NASA Cape image was centered on LC-39A, so its local
crop is shifted within the original 0.2-degree image footprint for LC-39B.
It is a coarse 2000-era visual composite, not modern pad geometry or terrain.

`LaunchEnvironment.Build(site, groundMaterial)` builds a coastal-pad scene in local meters. Its 2 km square visual ground lies at y=0. Starbase uses a fictional pad and mount with the rocket's assembly origin at 2.4 m. Kennedy uses a [simplified NASA-derived mobile launcher](nasa-mobile-launcher.md) and puts the rocket origin at approximately 15.9 m, aligning it visually with the normalized model's deck. These offsets are visual only and do not alter the simulation's geodetic launch state. Structure and site layouts are illustrative, not surveyed replicas.

An optional ground material can carry regional imagery, but texture orientation and scale must be independently verified. Imagery is visual only, not collision or elevation data; all generated primitive colliders are removed. The scene is not authoritative flight state or terrain. The representative scene has been rendered and packaged on macOS ARM64; imagery renders with an unlit material; flight integration is tracked in mission-integration.md.

## Imported Kennedy structure

An optional asset-review build instantiates NASA / Michael D. Carbajal's historical
LC-39 gantry beside the existing mount. The NASA geometry is preserved, with
three original material groups, a 105.7 m overall height normalization, and
an illustrative 75 m west offset. It is a historical shuttle-era structure,
not a model of today's SLS or commercial LC-39A infrastructure. Starbase retains
the earlier representative pad. Neither structure supplies collision geometry.

Source/hash/conversion details: [NASA gantry preparation](nasa-gantry-preparation.md).
The packaged macOS VM review passed model presence, height/base bounds,
map/reset persistence and isolation from Starbase (`-espGantryReview`).

The normal Kennedy build uses the approximate NASA mobile launcher instead of
this historical gantry. Set `ESP_HISTORICAL_GANTRY_REVIEW=1` only when building
the documented historical asset review. Do not describe that review as modern
SLS infrastructure.
