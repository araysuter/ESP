# Observed cloud shell

CLOUDS renders a **dated NASA cloud-fraction composite**, choosing each pixel's
newest valid observation from at most three advertised daily rasters. Clear-sky
zero is a valid observation and wins in newest-valid interiors. Missing/fill
pixels can use an older valid day within that bounded window. The display
crossfade near a date join uses only actual overlapping observations; it does
not create valid pixels in missing regions. This is not
instantaneous global weather, a forecast, measured cloud-top geometry or a
volumetric reconstruction. It does not affect flight forces.

The newest-valid selector replaces the earlier weighted temporal blend and
broad CPU smoothing, which could spread intermediate cloud fractions into a
blanket. The current visual treatment preserves these newest-valid interiors
and crossfades only within real overlap at a date boundary. It does not
reintroduce averaging across the full daily fields or a spatial blur of cloud
features. The Integration owner is qualifying the combined overlap-crossfade
revision; earlier passes are identified below.
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

## Bounded overlap crossfade

For each pixel, `ObservationBlend` retains both the newest valid sample and its
nearest older valid sample, when one exists. `Finish` seeds only the newer side
of a date join where that pixel's older sample has the same observation day as
the adjacent older region. A breadth-first traversal expands at most 32 pixels
inward, and only through pixels with that same selected-day/fallback-day pair.
Longitude wraps at the antimeridian; latitude sampling reflects across a pole
with a 180° longitude shift.

Within that bounded overlap strip, smoothstep weights crossfade the newer
sample toward its actual older sample at the join. Beyond the strip, newest
interior values stay unchanged. No neighboring cloud fractions are blurred or
averaged together, and pixels without an older overlapping observation are not
eligible. A join lacking matching real overlap may therefore remain visible.
This is a display interpolation between two real dates, not simultaneous weather
or a newly observed cloud field. The radius is a visual choice, not an accuracy
or uncertainty estimate; its physical width varies with latitude.

Fraction × support and support are crossfaded together. The shader divides
them after texture filtering, preserving the distinction between measured
clear sky and missing observations. The crossfade does not extend the validity
mask into gaps: narrow and broad missing regions retain their original missing
samples. The raw observation mask, contributing dates and reported coverage
remain unchanged. Normal texture filtering does not count as new observations.
This replaces the intermediate border-running-sum blur, which could extend
visual support into missing pixels.

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
opacity, not measured optical depth. Map and close-flight daytime opacity are
both 0.22, reducing the previous opaque appearance while keeping the surface
legible. The existing night opacity/illumination treatment is preserved. These opacity values are visual choices, not calibrated optical
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

Intermediate PR #56 revision 1e57aa6 used an eight-pixel border-running-sum
feather and 0.38 map opacity. The Integration owner reported build 4,
CloudBlendChecks and CloudNightSmoke passed, including both pads in daylight
and night and a NASA-profile ascent through 12 km and 150 km. Later iterations
changed both the border algorithm and opacity; those earlier captures do not
qualify the final settings.

Current PR #56 revision cc7d790 replaces the spatial blur with the 32-pixel
real-overlap crossfade and uses 0.22 daytime opacity for map and close flight.
The Integration owner reported build 6 and CloudBlendChecks passed. Global,
polar, night and pad/ascent rendered checks remain in progress at this
documentation update. Do not claim final appearance has passed until those
captures are inspected. The Integration owner retains the VM lease and will
record final visual/package evidence. Windows, the complete 15-minute refresh
interval and injected metadata transport failure have not been exercised for
this revision.
