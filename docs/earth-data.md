# Earth imagery provenance and mapping

NASA's Global Imagery Browse Services (GIBS) lists `BlueMarble_ShadedRelief_Bathymetry` in its geographic WMS capabilities. On November 14, 2026, the following request returned HTTP 200 with `image/jpeg` (283,733 bytes). This checks the endpoint response; it does not establish that the image has been imported or rendered in the game.

[Download global 2048 × 1024 Blue Marble image](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=BlueMarble_ShadedRelief_Bathymetry&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fjpeg)

The WMS 1.3.0 EPSG:4326 bounding box is ordered latitude, longitude: south -90°, west -180°, north 90°, east 180°. The resulting equirectangular texture spans longitude west-to-east and latitude north-to-south. Map longitude -180° and +180° to the texture seam and the poles to its top and bottom; verify the engine's UV orientation and longitude convention visually. Geographic coordinates use WGS84. See [GIBS access basics](https://nasa-gibs.github.io/gibs-api-docs/access-basics/) and [available visualizations](https://nasa-gibs.github.io/gibs-api-docs/available-visualizations/).

This is a visual composite, including shaded relief and bathymetry. It is not elevation data and must not define terrain height, collision, or physics. Keep the simulation's WGS84 placement and collision model independent of the texture. The low-resolution global image is an approximation and will not provide detailed ground imagery.

Bundle a locally stored visual fallback in the build so the demo does not depend on GIBS or a network connection; the request above is a candidate source, not evidence that a fallback has already been bundled. Record the retrieved file and any processing with the shipped asset. Credit the imagery in the application or accompanying attribution as **“Blue Marble imagery: NASA, via NASA GIBS.”** NASA states that most NASA Earth Science materials may be reused, including commercially, except where copyright is indicated; check the specific product's attribution and any third-party restrictions before distribution, and follow [NASA's media usage guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/). See also the [NASA Earth Science FAQ](https://science.nasa.gov/earth/faq/).

## Bundled fallback

`UnityProject/Assets/ESP/Art/Earth/BlueMarble.jpg` is the unmodified response to
that request, retrieved November 14, 2026. SHA-256:
`b7c2ee592bae14fc1a60f4a2c75475693156cbbe093e057a1646d938df706d6a`.
This small image is committed directly (explicit exception to LFS) so a clean
checkout can build offline without an LFS download. The build has no runtime
NASA API calls or API-key requirement. GIBS was used to acquire the bundled data.

The globe uses geodetic latitude/longitude vertices on a WGS84 ellipsoid.
ECEF X/Z/Y map to Unity X/Y/Z; UV U increases eastward and V northward.
This is a map view at one Unity unit per million meters, not a change to flight
units. It does not yet place the launch pad or connect the flight trajectory.

## Verification

November 14: Unity 6000.6.9f1 compiled and packaged the Earth preview for macOS;
the VM's Metal player rendered the Americas with north up. Build checks passed
for WGS84 equatorial/polar diameters and outward triangle winding. Texture size
was checked at 2048 × 1024. No live imagery request is made by the player. This
is a globe-rendering smoke test; orbital flight, ground detail, and Windows
packaging remain unverified.

Open `Assets/ESP/Art/Earth/EarthPreview.unity` in the Unity project. To package
this view, set `ESP_BUILD_PATH` and execute `BuildEarthPreview.Build` in the
owned integration editor. The ordinary build scene remains the rocket preview.
