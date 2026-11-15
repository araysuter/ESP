# Near-craft surface imagery

`LocalSurfaceTiles` streams a bounded area around the spacecraft, including the
selected pad before launch. It does not download a high-resolution world texture.
The continuous bundled Blue Marble Earth remains underneath the optional tiles.

## Sources and attribution

Display `LocalSurfaceTiles.Attribution` in the mission's imagery information:

> Ground: USDA / USGS The National Map orthoimagery; NASA / USGS Landsat WELD (2000 annual composite)

- **USDA / USGS The National Map, USGSImageryOnly:**
  [official service metadata](https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer).
  EPSG:3857, 256-pixel cached tiles. Its CONUS aerial imagery is primarily NAIP;
  collection dates and source resolution vary. The service currently credits
  USDA and USGS, with the cache described as refreshed June 2024. It is historical
  imagery, not a live view. Zoom 16 was verified at Cape Canaveral by the VM;
  its rendered ground sample spacing there is approximately 2.1 m per pixel.
  Advertised zooms 17 and 18 returned 404 there, so the implementation caps at 16.
  A missing center tile lowers the selected zoom; missing offshore neighbors do
  not degrade the land tiles. No claim of submeter detail is made.
- **NASA GIBS, Landsat WELD corrected-reflectance true-color global annual:**
  `Landsat_WELD_CorrectedReflectance_TrueColor_Global_Annual`, date `2000-12-01`,
  `GoogleMapsCompatible_Level12`, `.jpeg` format. The live capabilities and a
  Cape tile were checked in the VM. This is a historical annual composite with
  a 30 m source product, not an observation made on December 1. Tile resolution
  and resampling do not imply finer source detail. See
  [GIBS service documentation](https://nasa-gibs.github.io/gibs-api-docs/access-basics/)
  and the
  [NASA product metadata](https://gibs.earthdata.nasa.gov/layer-metadata/v1.0/Landsat_WELD_CorrectedReflectance_TrueColor_Global_Annual.json).

[USGS terms](https://www.usgs.gov/faqs/what-are-terms-uselicensing-map-services-and-data-national-map)
describe National Map services/data as free and public domain. The imagery
service itself identifies licensing exceptions for Alaska imagery. Accordingly,
the high-detail provider is deliberately restricted to a CONUS bounding region
(24–49.5°N, 125–66°W); it is not enabled in Alaska, Hawaii or globally. This
rectangle is only a request eligibility filter, not a promise of data coverage.
NASA remains the global source. NASA imagery use follows
[NASA imagery and media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/).
No provider credentials or commercial map tiles are used.

## Bounds, lifetime and fallback

- At most 25 USGS tiles and 9 NASA tiles are desired at one time. Their zoom
  decreases with altitude. USGS fades away between 14–18 km; all optional
  tiles fade between 45–60 km and stop requesting above 60 km.
- The cache contains at most 48 decoded 256×256 textures (about 12 MiB at RGBA8,
  excluding Unity resource overhead). Two requests may be active, with a 15 s
  timeout and at most one new request per 0.15 s. No disk cache or background
  global prefetch exists. Old requests are aborted as the footprint moves.
- Site/environment replacement, simulation replacement/reset or mission time
  rollback clears textures, meshes, materials and requests. Disable/destruction
  performs the same cleanup. HTTP/decode failures are rate limited for retry.
- If no optional imagery succeeds, existing bundled imagery remains usable.
  The old cropped 2 km plane is hidden only when a loaded broad NASA tile covers
  the pad. Authored pad, mount, road, buildings and tower remain unchanged.
- Missing imagery cells and feathered outer boundaries reveal the underlying
  Earth. Tiles fade in, and the detail layer draws above the broad layer.
  A zoom-level change can briefly reveal the lower-detail Earth while its new
  tiles arrive; it is not a seamless multi-level terrain renderer.

## Geometry and integration

Each tile uses a 16×16 subdivided WGS84 ellipsoid mesh. Web Mercator image rows
run north to south; mesh texture V runs south to north. ECEF vertex positions
are computed in doubles, converted into the selected pad's fixed east/up/north
frame, and only then stored in floats relative to each tile's center. Each frame,
that center is translated relative to the simulated spacecraft ECEF position,
or to the pad ECEF origin before launch. Longitude wraps at the antimeridian.
Mercator detail stops at its latitude limit; the globe still covers the poles.

The mesh uses constant selected-site elevation minus 4 cm. This is an explicit
visual approximation, **not terrain elevation or collision data**. The flight
core and collision assumptions are unchanged. No artificial texture detail is
added. Surface lighting uses the actual launch sun and a modest presentation-only
distance haze matching the continuous Earth backdrop's ground-haze contract.

Attach the component to an identity-transform root, assign `director`, `driver`,
`surfaceSun` (the local launch directional light), and serialized `surfaceShader`
from `Assets/ESP/Presentation/LocalSurface.shader`. Preserve that shader reference
in the built scene to avoid shader stripping. The component runs at execution
order 220 and renders on the main local-meter overlay camera, never layer 31.
Expose `Status` alongside the persistent attribution for network/source feedback.

## Validation status

Endpoint and image-dimension checks were performed in the shared VM by the
coordinator. All compilation, execution and rendered checks must also occur
there. `dotnet run --project SurfaceTiles.Tests` checks addressing, north-up UV
orientation, antimeridian wrapping, polar limits, ellipsoid vertices and triangle
winding. This source handoff has not itself executed that suite or Unity.

Still required in the integrated VM player: clear high-detail pad imagery at both
sites, movement following the craft, tile/cache caps, reset/site switching during
an active request, network-disabled fallback, and no floating rectangular edge at
low altitude. Updating this record after actual checks avoids treating source
implementation as a verified rendering result.
