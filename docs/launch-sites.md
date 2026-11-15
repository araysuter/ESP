# Representative launch sites

`LaunchSites.All` offers approximate Starbase, Texas (25.997° N, 97.156° W) and Kennedy LC-39A (28.608422° N, 80.604133° W) locations. The Kennedy coordinates are from [NASA’s Apollo launch information](https://www.nasa.gov/wp-content/uploads/static/history/SP-4029/Apollo_18-01_General_Background.htm); Starbase coordinates are approximate demo values. Neither establishes a current surveyed pad position. Both elevations are provisional 0 m and both headings are provisional 90° clockwise from north. They do not establish a real launch trajectory or permission to use a facility.

`LaunchEnvironment.Build(site, groundMaterial)` builds a fictional, reusable coastal-pad scene in local meters. Its 2 km square visual ground lies at y=0 and the main deck top at y=1 m. The small mount extends to y=1.2 m. `LaunchEnvironment.RocketBaseHeightMeters` is 2.4 m; the rocket's assembly origin should be placed there to put its engines' lowest point (1.2 m below the base origin) on that mount; verify the actual imported model and clearance. The camera, lighting and rocket are supplied by the caller. Structure and site layout are illustrative, not replicas.

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

The normal build deliberately excludes this historical gantry while the modern SLS mobile launcher is sourced. Set `ESP_HISTORICAL_GANTRY_REVIEW=1` only when building the documented historical asset review. Do not describe that review as modern SLS infrastructure.
