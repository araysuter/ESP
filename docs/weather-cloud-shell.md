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
