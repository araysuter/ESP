# Observed cloud shell

CLOUDS renders a **dated NASA cloud-fraction composite**, choosing each pixel's
newest valid observation from at most three advertised daily rasters. Clear-sky
zero is a valid observation: it prevents an older cloudy pixel from showing
through. Missing/fill pixels can use an older valid day within that bounded
window. Regions with no valid observation remain missing in the source coverage
mask; only a narrow display feather may extend into adjacent missing pixels.
This is not
instantaneous global weather, a forecast, measured cloud-top geometry or a
volumetric reconstruction. It does not affect flight forces.

The newest-valid selector replaces the earlier weighted temporal blend and
broad CPU smoothing, which could spread intermediate cloud fractions into a
blanket. The current visual treatment preserves these newest-valid interiors
and feathers only near date/missing-data boundaries. It does not reintroduce
averaging across the full daily fields. The Integration owner is qualifying the
combined border-feather revision; earlier passes are identified below.
One daytime Terra sensor's daily swaths still do not establish a simultaneous
global weather field. Adjacent swaths can have different acquisition days;
feathering softens their presentation without making them simultaneous.

## Data, decoding and provenance

- Source: NASA GIBS `MODIS_Terra_Cloud_Fraction_Day`, Terra/MODIS MOD06_L2
  6.1 / 6.1NRT daytime swaths. NASA's [layer metadata](https://gibs.earthdata.nasa.gov/layer-metadata/v1.0/MODIS_Terra_Cloud_Fraction_Day.json)
  identifies the daily period and underlying products.
- The [official MODIS cloud-fraction colormap](https://gibs.earthdata.nasa.gov/colormaps/v1.3/MODIS_Cloud_Fraction.xml)
  is inverted before display filtering. Encoded values 0–100 percent remain
  quantitative fractions; alpha-zero, fill RGB 192,192,192 and unknown palette
  colors remain missing. Cloudiness is never inferred from white land/satellite
  pixels, and missing values are never substituted with clear sky.
- WMS 1.3.0 EPSG:4326 requests use latitude/longitude BBOX
  `-90,-180,90,180`, with 4096×2048 transparent PNG for cloud fraction;
  Satellite remains 2048×1024. Up to three advertised UTC days are fetched newest
  first. Cloud requests sample approximately 0.088° (about 9.8 km at the equator),
  without promising native sensor resolution or new observations. The larger
  cloud raster increases download/decoding/memory work; combined QA must check
  its actual provider response, timing and target-memory behavior.
- The visible date range must describe contributing days. `ObservationBlend.Add`
  reports newly contributing pixels for each day, rather than every valid
  source pixel including those superseded by newer data. Logs report per-day
  validity/unknown colors and the final missing fraction. Coverage uses original
  observation support and cosine-of-latitude area weighting, not inferred data.
- The display texture carries fraction and validity support. Shader sampling
  normalizes fraction × support by support after filtering/mipmapping so missing
  samples cannot become measured clear sky. Display interpolation does not add
  observations. Transparent areas can mean clear sky or no observation; dates
  and missingness remain part of the provenance.

## Bounded display feather

After selecting the newest valid pixel, `ObservationBlend.Finish` detects
boundaries where a pixel's selected observation day differs from a neighbor,
including transitions to missing data. It filters fraction × support and
support together using separable running sums with radius eight pixels
(a 17×17 neighborhood). Boundary influence is calculated separately; pixels
away from those boundaries keep their original newest-valid values. Longitude
wraps across the antimeridian. Latitude sampling reflects over a pole and
shifts longitude by 180°, rather than clamping to an unrelated row.

This is local display interpolation across adjacent selected days or missing
support, not a broad temporal average of three daily rasters. The shader divides
the premultiplied fraction by filtered support after texture sampling. Missing
data is not substituted with zero cloud fraction. The feather can extend narrow
visual support into a neighboring no-data strip; broad gaps remain transparent.
The original per-pixel observation mask, contributing dates and reported
coverage remain unchanged. Feathered support is not counted as observed Earth
area or presented as a new NASA measurement. At the 4096×2048 request scale the
radius is approximately 0.70° in raster coordinates; its physical width varies
with latitude and is a visual choice, not an uncertainty estimate.

Cloud mode uses cloud-free Blue Marble underneath the shell. Satellite mode
has its own dated true-color mosaic, which already includes clouds and uses a
paired NASA no-data mask over historical Blue Marble gaps. Stacking this
satellite mosaic under an independently decoded cloud shell would double the
cloud field. The map and flight backdrop use the same decoded cloud texture.

Credit NASA GIBS / Terra MODIS and NASA Blue Marble. See NASA's
[GIBS service documentation](https://nasa-gibs.github.io/gibs-api-docs/access-basics/)
and [imagery usage guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/).
NASA does not endorse this game. No new external rendering library is included.

## Latest observations and switching

Normal startup selects CLOUDS automatically; Blue Marble remains usable while
loading or offline. Availability is checked at startup and every 15 minutes of
real time, independently of simulation time rate/pause. The refresh icon fetches
the current catalog and forces the latest selected imagery layer to reload.
SURFACE explicitly selects the offline surface; refreshing its catalog does
not silently enable clouds.

The date selector finds the newest advertised day at or before UTC today,
respecting catalog gaps and whole-day interval cadence. Unsupported formats
are rejected. An advertised current day may still be empty. Requests, selected
dates, composite date ranges and UTC timestamp labels use invariant Gregorian
formatting, including on systems using non-Gregorian calendars.

Each observed mode retains one cached texture together with its requested day,
contributing dates, provider identity, description and fetch status. Surface
hides observed layers without destroying their cache. Selecting a cached mode
restores visual and provenance immediately; repeated clicks on a loading mode
do not restart its request. Explicit and periodic refresh still force network
loads. If a newer day is available, the prior cached observations can remain
visible while the newer data is fetched. Failed/empty requests preserve the
actual displayed layer and its dates, with a retention message. The credit
distinguishes the mode requested from the layer actually displayed.

Observation age is recomputed from the wall clock during UI refresh, even after
network failure. A newest contributing day more than two UTC calendar days old
is marked STALE; this is an application policy, not a NASA timeliness guarantee.
Daily WMS metadata does not establish exact per-pixel acquisition times. The
credit tooltip and mission information expose fuller provenance/fetch status.

## Geometry and appearance

`CloudShell` clones the WGS84 display mesh, preserving UVs, normals, winding and
ECEF X,Z,Y → Unity X,Y,Z axes. It offsets along the geodetic normal by 10,000 m
at 1,000,000 m/unit. This is a representative visual altitude, not measured
cloud-top height or a terrain/collision surface. Cloud fraction controls visual
opacity, not measured optical depth. Default map daytime opacity is 0.38,
reduced from 0.68 so the surface remains more legible; close-flight uses its
existing 0.35 opacity. The existing night opacity/illumination treatment is
preserved. These opacity values are visual choices, not calibrated optical
properties. The shell has no procedural weather,
wind advection, ray marching or volumetric physics. Night transmission and
illumination are visual approximations. Scene builders serialize the cloud
shader so player stripping retains it; map and flight use the same texture.

## Verification and pending combined QA

Prior source ef4eae5 qualified Unity 6000.6.9f1 compilation and a macOS ARM
package: automatic cloud startup, explicit refresh, real invalid-layer WMS
failure retention and offline restoration passed. November 13–15 UTC data
produced 2,093,314 valid pixels, 3,838 missing pixels, 99.969% area coverage
and zero unknown palette colors. That run used the previous weighted blend,
not the newest-valid selector or cache-switch follow-up. The 1280×800 refresh
control/date credit was inspected. A separate live GEOS-FP request returned
72/72 native levels valid November 14 at 21:00 UTC; that is launch atmosphere,
not the visual cloud field.

`NasaImageryTimeChecks.Validate` covers catalog gaps, future dates, cadence,
malformed/unsupported domains and aging. Its `th-TH` Buddhist-calendar check
passed using the actual date source in standalone .NET 8, and restores culture
in `finally`. Builds run the date checks automatically. Python NASA profile
parser tests passed (four tests).

The expanded development-only `-espNasaFreshnessSmoke` checks automatic
clouds; Surface→Clouds immediate texture/date identity; a first Satellite load
with repeated selections; eight cached Satellite→Clouds→Surface→Satellite
cycles; forced latest refresh; real invalid-layer WMS failure retention; and
offline restoration. It captures `nasa-freshness-latest.png` in the player's
persistent data directory.

The Integration owner reported combined source b0e8fe4 plus PR #56's 99d3c4a
passed the Unity build, cloud checks and packaged freshness smoke: automatic
4096×2048 cloud loading, twelve repeated loading selections, eight cached
cycles, explicit replacement, failed-refresh texture/date retention and offline
restoration. The 4K data had 8,372,683 valid pixels, 15,925 missing pixels,
99.969% area-weighted coverage and no unknown palette colors, dated November
13–15 UTC. The combined CloudNightSmoke also passed before the latest border
revision. These passes validate the earlier newest-valid/cache combination,
not the subsequent feather appearance.

PR #56's 1e57aa6 adds the bounded border feather and lower default daytime
opacity described above. The Integration owner reported build 4 and its
CloudBlendChecks passed. The new night/map, both-pad captures and real-ascent
run remain in progress at this documentation update; do not claim the latest
rendered appearance has passed before inspecting those results. The Integration
owner retains the VM lease and will record final visual/package evidence.
Windows, the complete 15-minute refresh interval and injected metadata transport
failure have not been exercised for this revision.
