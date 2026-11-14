# Satellite imagery coverage

Satellite true color is a newest-valid-pixel temporal mosaic of up to three UTC
observation days, ending on the advertised default day (capped at today UTC).
It is not an instantaneous/live globe. An explicit date uses that day and its two
predecessors. Only dates inside advertised layer intervals are requested.

The global backing image is always bundled historical Blue Marble. Remaining
polar/night/swath gaps show that fallback rather than fabricated observations.
NASA MODIS Terra daily true-color images include clouds; they are not cloud-free
surface imagery, wind measurements, or a forecast. This remains a visualization,
independent of the flight weather profile.

## Why PNG alone is insufficient

A real EPSG:4326 GIBS WMS request for 2026-11-14 was inspected at 1024×512.
Despite `FORMAT=image/png&TRANSPARENT=TRUE`, corrected reflectance had 524,288
opaque pixels, including 79,065 exactly black pixels. Native JPEG no-data areas
survive the PNG response. Do not mask dark RGB values: dark ocean is legitimate.

The paired `MODIS_Terra_Data_No_Data` visualization has transparent valid-data
regions and opaque gold no-data regions. The same day's inspected mask had
433,265 transparent pixels and 91,023 opaque no-data pixels. Satellite loading
therefore requires both the day's true-color image and this mask at identical
projection, bounds and dimensions. Opaque mask pixels never replace the fallback.
If a day's image or mask fails, that day is skipped. If all days fail, the previous
map and its displayed metadata remain in place.

Requests use WMS 1.3.0 EPSG:4326 latitude/longitude axis order, global bounding box
`-90,-180,90,180`, 2048×1024 output. Fetch time is separate from observation date.
`DisplayedDate` is the newest successful day for existing consumers;
`DisplayedDescription` describes the actual received date range and fallback.
Clicks while metadata loads remain selected and load after metadata arrives.
Switching layers aborts the current image/mask request and invalidates its result.

Official references:
- [NASA GIBS access and time dimensions](https://nasa-gibs.github.io/gibs-api-docs/access-basics/)
- [NASA visualization catalog and utility masks](https://nasa-gibs.github.io/gibs-api-docs/available-visualizations/)
- [Live WMS capabilities, including layer time domains](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetCapabilities)

Source inspection and network samples were verified outside Unity. Packaged
rendering, request cancellation, and offline interaction require player rehearsal.
