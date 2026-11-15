# Earth imagery provenance and mapping

Local surface imagery tiles are cached in the application's persistent data directory
after a successful download. Previously visited areas can load without another
network request while network imagery is enabled. The cache holds at most 256
tiles of at most 1 MiB each, evicts least-recently accessed files, and expires
USGS tiles after seven days and fixed NASA composites after 90 days. Invalid or
unreadable entries fall back to a fresh request; the bundled globe remains
available. The first visit still requires downloads; cache files are disposable.

The bundled Earth image is NASA GIBS `BlueMarble_ShadedRelief_Bathymetry`,
retrieved November 14, 2026 at 8192 × 4096 pixels. This replaces the original
2048 × 1024 fallback with 16 times as many texels, in both map and flight views.

[Exact source request](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=BlueMarble_ShadedRelief_Bathymetry&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=8192&HEIGHT=4096&FORMAT=image%2Fjpeg)

Unmodified JPEG response: 3,118,845 bytes; SHA-256
`dc26b816037021601eeea972c59e264a37fd41cd4062381fdb4d9dc9a6230348`.
The image is committed directly under the existing explicit LFS exception so
clean clones retain an offline Earth. Unity imports at 8192 with mipmaps and
anisotropic filtering. It is a historical visual composite, not live imagery,
elevation, terrain geometry, or collision data. At global scale it cannot show
individual buildings or metre-scale terrain.

GIBS WMS 1.3.0 EPSG:4326 BBOX uses south, west, north, east ordering. The texture
is equirectangular. Longitude wraps; latitude clamps. The display mesh uses
512 longitude and 256 latitude segments with UInt32 indices on WGS84. ECEF
X/Z/Y map to Unity X/Y/Z; UV U increases eastward and V northward. One map unit
is one million metres; the authoritative simulation remains double-precision SI.

The flight backdrop shares the selected map material and current imagery.
A separate scaled-space camera follows the flight camera orientation and
position, with Earth expressed in the launch site's fixed east/up/north frame.
The backdrop sunlight is rotated into that same frame. NASA clouds are a
separate shell; see [cloud provenance](weather-cloud-shell.md). Satellite mode
currently uses a 2048 × 1024 dated mosaic, while Surface and Clouds retain the
higher-resolution cloud-free base.

Credit: **Blue Marble imagery: NASA, via NASA GIBS.** See
[NASA media usage](https://www.nasa.gov/nasa-brand-center/images-and-media/),
[NASA Earth Science FAQ](https://science.nasa.gov/earth/faq/) and
[GIBS access](https://nasa-gibs.github.io/gibs-api-docs/access-basics/).

VM verification: NASA returned the dimensions above; Unity 6000.6.9f1 built the
macOS ARM player with an explicit 8192 × 4096 import assertion. The packaged
Starship ascent reached verified orbit and native camera dragging changed the
view around the vehicle with the Earth backdrop following. Windows and Intel
macOS remain untested. BuildMission.Build is the integrated game entry point;
BuildEarthPreview.Build remains an optional globe-only qualification scene.
