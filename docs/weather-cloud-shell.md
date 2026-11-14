# Observed cloud shell

The map's CLOUDS selection renders measured cloud fraction as white translucent coverage on a separate mesh above the Earth. The satellite/Blue Marble ground texture remains visible. This is a display of recent daily satellite observations, not instantaneous global weather, a forecast, or a volumetric reconstruction. It does not affect flight forces.

## Data and decoding

- Source: NASA GIBS `MODIS_Terra_Cloud_Fraction_Day`, Terra/MODIS MOD06_L2 6.1 / 6.1NRT daytime swaths. NASA's [layer metadata](https://gibs.earthdata.nasa.gov/layer-metadata/v1.0/MODIS_Terra_Cloud_Fraction_Day.json) identifies its daily period and underlying products.
- [Official v1.3 MODIS cloud-fraction colormap](https://gibs.earthdata.nasa.gov/colormaps/v1.3/MODIS_Cloud_Fraction.xml), inspected November 14, 2026. `CloudShell.TryCloudFraction` implements every exact palette RGB for values 0–100 percent. The transparent fill RGB 192,192,192 has source value 127. Unknown palette colors and alpha-zero pixels remain missing. Cloud cover is never inferred from white satellite pixels, a red-channel threshold, or a land/ocean color.
- WMS 1.3.0 EPSG:4326 requests use latitude/longitude BBOX `-90,-180,90,180`, 2048×1024 transparent PNG. Exact palette inversion happens on original PNG bytes before filtering. Decoded density textures use linear sampling, longitude repeat, latitude clamp and trilinear mipmap filtering, so spatial interpolation operates on cloud fractions rather than the scientific display palette.
- Up to three advertised observation days are requested newest first. Every valid sample, including clear-sky zero percent, wins over older samples. Only missing pixels are filled from earlier observation days. The date range, fetch time and observed coverage are displayed. An advertised date does not guarantee complete coverage or current data; partial swaths are retained honestly. Failed or empty downloads preserve the previous rendered layer.
- Missing sample centers have zero density, hence transparent coverage. Filtering softens boundaries over neighboring texels; it does not fill large missing swaths or extrapolate observations across the globe. Clear sky and missing coverage are both visually transparent; the displayed coverage percentage counts valid clear samples.

NASA provides these [GIBS services](https://nasa-gibs.github.io/gibs-api-docs/access-basics/); credit NASA GIBS / Terra MODIS and NASA Blue Marble in the map. Follow [NASA imagery usage guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/). No new external library or copied third-party rendering code is included.

## Geometry and appearance

`CloudShell` clones the existing WGS84 display mesh, preserving its geodetic UVs, outward normals, winding and ECEF X,Z,Y → Unity X,Y,Z convention. It offsets vertices along the geodetic normal by 10,000 m at the map's 1,000,000 m/unit scale. **This is a representative visual altitude, not a measured cloud-top height.** The mesh is an ellipsoidal shell, not a terrain or collision surface.

The URP `ESP/ObservedCloudShell` shader reads decoded fraction as opacity and shades it white with simple directional sunlight. Opacity is a visual approximation, not measured optical depth. There is no invented wind, temporal advection, noise-only cloud field, ray marching or cloud physics. A single shell and a 2048×1024 mipmapped density texture bound rendering work.

[CesiumJS CloudCollection](https://cesium.com/learn/cesiumjs/ref-doc/CloudCollection.html) was evaluated as an open-source approach. It provides local procedural cumulus billboards with noise controls, rather than observed global cloud coverage; adding CesiumJS to Unity would not supply the missing weather reconstruction. The bounded mesh shell therefore uses the existing Unity renderer and NASA data without a new dependency.

## Integration and verification

Scene builders must serialize `NasaMapLayers.cloudShader` from `Assets/ESP/Art/CloudShell.shader` so player shader stripping retains it. `CloudDensityTexture`, `CloudShader` and `CloudsVisible` expose the same map data for the scaled flight backdrop. A backdrop renderer with the same WGS84 mesh can add `CloudShell`, call `Show(texture, shader)` while visible, and `Hide()` otherwise. The shell inherits the renderer's layer and transform.

Validation is pending on the shared VM: Unity compilation, shader import/player inclusion, cloud visual orientation and date/coverage labels, transparent gaps, repeated layer switching, offline restore, and orbit-backdrop behavior. No local compilation, render or packaged test was run for this change.
