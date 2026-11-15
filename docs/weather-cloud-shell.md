# Observed cloud shell

The CLOUDS selection renders a **smoothed blend of dated NASA cloud-fraction observations** on one mesh above cloud-free Blue Marble. It is not instantaneous global weather, a forecast, measured cloud-top geometry, or a volumetric reconstruction. It does not affect flight forces. The map and flight backdrop share the same decoded texture.

## Why the earlier globe showed swaths

The previous cloud selection left the three-day satellite true-color mosaic on the ground. That image already includes clouds and hard daily-swath boundaries; adding a translucent cloud shell doubled the cloud field and could not hide those seams. Cloud mode now explicitly restores Blue Marble underneath the shell. Satellite mode still offers its separately labeled true-color mosaic.

The previous cloud mosaic also selected the newest valid pixel independently at every location. Adjacent swaths could therefore switch observation day abruptly. Cloud mode now forms one normalized scalar blend, with a small spatial interpolation footprint and smoothly varying support near each daily swath edge. Lower display opacity keeps the ground legible; it does not alter the decoded fractions.

## Data and decoding

- Source: NASA GIBS `MODIS_Terra_Cloud_Fraction_Day`, Terra/MODIS MOD06_L2 6.1 / 6.1NRT daytime swaths. NASA's [layer metadata](https://gibs.earthdata.nasa.gov/layer-metadata/v1.0/MODIS_Terra_Cloud_Fraction_Day.json), checked November 14, 2026, identifies its daily period and underlying products.
- [Official v1.3 MODIS cloud-fraction colormap](https://gibs.earthdata.nasa.gov/colormaps/v1.3/MODIS_Cloud_Fraction.xml). `CloudShell.TryCloudFraction` retains exact palette inversion for values 0–100 percent. Alpha-zero samples, fill RGB 192,192,192 (source value 127), and unknown palette colors remain missing. There is no thresholding of white satellite pixels or land/ocean color.
- WMS 1.3.0 EPSG:4326 requests use latitude/longitude BBOX `-90,-180,90,180`, 2048×1024 transparent PNG. Exact palette inversion precedes filtering.
- Up to three advertised observation days are fetched, newest first. Every valid sample contributes, including clear-sky zero percent. Relative date weights are 1, 0.65 and 0.4225. These are display-compositing choices, not a NASA product algorithm or uncertainty model. Original per-day dates remain logged; the contributing date range and fetch time remain visible.
- For each day, the decoded fraction numerator and validity weight are filtered together with two separable box passes of nominal radius two texels. Latitude support extends at most four texels (about 0.7 degrees); longitude radius grows with latitude to approximate the same geographic footprint. A missing region larger than that footprint is not filled. The algorithm takes a weighted mean of the daily filtered numerator/weight pairs, never a maximum of cloudiness. Swath-internal discontinuities present in NASA's raster can only be softened; no new observations are claimed.
- Longitude filtering wraps at the antimeridian. Latitude filtering reflects across a pole and shifts longitude by 180 degrees. The outermost north/south rows use a common validity-weighted ring value so all UV longitudes meet consistently at the pole. This is a display interpolation of a subpixel polar cap, not a new polar observation.
- The linear RG texture stores **fraction × spatial support, spatial support**. The shader divides R by G **after** bilinear/trilinear sampling, including mipmaps. Missing values therefore cannot darken neighboring valid values as if they were measured clear sky. Opacity fades at low support; large unsampled areas remain transparent. Both clear sky and missing observations can look transparent, so the UI explicitly reports missingness.
- Coverage statistics use the original, unfiltered union of valid observation pixels. Logs include the valid and missing pixel counts and unknown palette counts per date; the UI reports approximate Earth area coverage weighted by cosine of latitude. Smoothing and the polar-ring interpolation never count as new observations. Failed/empty downloads preserve the prior rendered layer.

NASA provides these [GIBS services](https://nasa-gibs.github.io/gibs-api-docs/access-basics/); credit NASA GIBS / Terra MODIS and NASA Blue Marble in the map. Follow [NASA imagery usage guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/). No new external library or copied third-party rendering code is included.

## Alternative products considered

NASA's [MODIS cloud optical property guide](https://atmosphere-imager.gsfc.nasa.gov/sites/default/files/ModAtmo/MODISCloudOpticalPropertyUserGuideFinal_v1.1_1.pdf) distinguishes daily and multi-day cloud fractions. An eight-day aggregation would trade temporal specificity for coverage; it would not establish a simultaneous cloud field. This patch retains the bounded three-day download window and labels the result as a blend.

NASA [GEOS near-real-time products](https://gmao.gsfc.nasa.gov/gmao-products/geos-near-real-time-data-products/) provide global model/assimilation output and are a plausible future source of a coherent modeled field. They are not direct satellite cloud observations. Their cloud-fraction download, time selection and decoding have **not** been qualified here; this patch does not silently switch to or promise a live GEOS cloud feed.

## Geometry and appearance

`CloudShell` clones the existing WGS84 display mesh, preserving its geodetic UVs, outward normals, winding and ECEF X,Z,Y → Unity X,Y,Z convention. It offsets vertices along the geodetic normal by 10,000 m at the map's 1,000,000 m/unit scale. **This is a representative visual altitude, not a measured cloud-top height.** The mesh is an ellipsoidal shell, not a terrain or collision surface.

The URP `ESP/ObservedCloudShell` shader renders the decoded mean fraction as white opacity with simple directional sunlight. Maximum display opacity is 0.68 on the map and 0.35 in close flight to keep ground legible; opacity is a visual approximation, not measured optical depth. There is no invented wind, temporal advection, procedural cloud field, ray marching or cloud physics. One shell and a 2048×1024 mipmapped texture bound rendering work. Filtering is performed at download time, not per frame.

## Integration and verification

Scene builders must serialize `NasaMapLayers.cloudShader` from `Assets/ESP/Art/CloudShell.shader` so player shader stripping retains it. `CloudDensityTexture`, `CloudShader` and `CloudsVisible` expose the same map data for the scaled flight backdrop. A backdrop renderer with the same WGS84 mesh can add `CloudShell`, call `Show(texture, shader)` while visible, and `Hide()` otherwise. The shell inherits the renderer's layer and transform. Backdrop ground-material synchronization must follow the map's Blue Marble switch when cloud mode becomes active.

Historical VM validation on November 14, before the smoothing revision: Unity compilation and packaged shader inclusion passed. The player decoded 2,093,654 valid pixels with zero unknown palette colors across November 12–14, rendered the shell, and restored the offline map. A separate VM check matched all 101 encoded palette colors against NASA's live XML. These results do not validate the new blend or its visual appearance.

VM qualification for this revision: Unity compiled and packaged the shader. CloudBlendChecks passed constant/clear fields, missing-data support, periodic longitude, polar-ring consistency and dated weighting. The player rendered a smooth blend from November 12–14 with 2,093,686 valid and 3,466 missing pixels, zero unknown palette colors, and 99.978% area-weighted observed coverage. Native map and close-flight captures were inspected. A same-angle Surface capture showed the expected ocean/bathymetry underneath; flight opacity is intentionally lower because linear alpha otherwise washes out close ground views. Failed-refresh injection and dedicated antimeridian/pole visual captures remain unperformed for this revision; those boundaries have numerical checks. All compilation/rendering/tests ran in the VM.

## Latest observations and refresh policy

The normal startup view now selects CLOUDS automatically. Blue Marble remains
visible while data loads or if NASA is unreachable. Availability is fetched at
startup and every 15 minutes of real time, independently of simulation time
rate or pause. REFRESH fetches the current catalog and reloads the latest
advertised day for the selected imagery layer; SURFACE explicitly selects the
offline surface. Refreshing SURFACE does not silently enable clouds.

Date selection uses the newest advertised day at or before UTC today, respects
catalog gaps and whole-day interval cadence, and rejects unsupported time
formats. An advertised current day may still contain no observations; only
nonempty decoded days contribute to the displayed date range. The cloud field
continues to blend at most three dated days. This improves access to the latest
available imagery; it does not create an instantaneous global weather field.

The visible map credit reports daily observation age; the full provenance and
fetch/failure status are also available in its tooltip and mission information.
Age is calculated from the wall clock on each UI refresh, including when the
network fails. A newest contributing day more than two UTC calendar days old
is marked STALE (an application policy, not a NASA guarantee). Daily raster
metadata cannot establish the exact per-pixel acquisition time. A failed
refresh retains the existing texture and its original observation dates, and
reports that retention. Transparent missing data remains distinct from decoded
clear sky in the data and coverage accounting.

`NasaImageryTimeChecks.Validate` exercises gaps, future dates, cadence,
unsupported/malformed domains and aging; release builds run it automatically.
The development-only `-espNasaFreshnessSmoke` player check exercises automatic
cloud startup, a manual latest refresh, a real invalid-layer WMS failure with
texture/date retention, and offline restoration. It captures
`nasa-freshness-latest.png` in the player's persistent data directory.

Qualification for the latest-observation changes, November 15, 2026 UTC
(November 14 EST): Unity 6000.6.9f1 compiled and packaged the macOS ARM player;
the build's cloud-blend and imagery-time checks passed. The packaged freshness
smoke passed automatic cloud loading, manual refresh, real invalid-layer WMS
failure retention and offline restore. Both successful loads used November
13–15 observations, 2,093,314 valid pixels, 3,838 missing pixels, 99.969%
area-weighted coverage and zero unknown palette colors. The 1280×800 capture
was inspected, including the refresh control and visible daily-age credit.
The Python NASA profile tests also passed; a separate live GEOS-FP request
returned 72/72 levels valid November 14 at 21:00 UTC. No instantaneous cloud
field is claimed. This revision was not run on Windows; the full 15-minute
automatic timer was not waited out, and metadata-network failure was not
injected (the packaged failure check targets WMS imagery requests).

Locale follow-up: NASA request/selection dates, composite date ranges and UTC
fetch labels explicitly use invariant Gregorian formatting. The date checks
also switch to `th-TH` (Thai Buddhist calendar), verify the latest selection
round-trips as `2026-11-15`, and restore the prior culture. The complete date
check passed in a standalone .NET 8 run using the same C# source and a logging
shim, without opening Unity or changing the shared project. Rendering was not
repeated for this date-format-only follow-up.


Pending switch-cycle follow-up (source prepared for combined QA): Surface no
longer destroys satellite textures. Each observed mode retains one texture with
its requested day, contributing dates, provider identity, description and fetch
status. Selecting a cached mode restores its visual and provenance together
without waiting for a download. Selecting the same mode during loading does
not abort/restart its request. Explicit refresh and the 15-minute catalog
refresh still force new imagery requests; when a newer advertised day appears,
selection can show the older cached observations while fetching the new day.
The credit distinguishes requested/loading mode from the layer actually shown.
The data decoding, blend weights, resolution and cloud shader are unchanged.

The expanded development smoke checks Surface→Clouds immediate texture/date
identity, a first satellite load with repeated same-mode selections, eight
Satellite→Clouds→Surface→Satellite cache cycles, then explicit refresh, failed
refresh retention and offline restoration. The owner of the Integration lease
will run the combined packaged/UI QA; these new switch checks have not yet been
executed by the weather audit chat. Previous qualification above applies to the
prior source revision.
