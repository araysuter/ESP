# Current Earth and space-weather data research

These are candidate feeds, not an implemented or qualified Unity feature. The map should show each layer's observation date and retrieval time, distinguish stale/offline data, and retain the bundled offline Earth fallback described in [earth-data.md](earth-data.md). Satellite imagery is an observation composite, not live video or a weather forecast. Swaths may be missing, cloud-obscured, or only partially populated for the newest date; an HTTP 200 and an advertised date do not prove global coverage.

## NASA GIBS imagery

The [EPSG:4326 WMS capabilities](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&REQUEST=GetCapabilities&VERSION=1.3.0) advertised the following latest dates when queried on November 14, 2026 (local time). Refresh capabilities at runtime or on a reasonable schedule: these dates change and may cross UTC midnight. The future-looking November 15 date is the service's advertised default, not a claim of complete observations.

| Layer | Meaning | Advertised latest `TIME` | Verified example request |
| --- | --- | --- | --- |
| `MODIS_Terra_CorrectedReflectance_TrueColor` | Terra MODIS true-color view, including visible clouds | 2026-11-15 | [GetMap PNG](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=MODIS_Terra_CorrectedReflectance_TrueColor&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fpng&TRANSPARENT=TRUE&TIME=2026-11-15) |
| `MODIS_Terra_Cloud_Top_Temp_Day` | Color-coded daytime cloud-top temperature, not a natural-color cloud mask | 2026-11-15 | [GetMap PNG](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=MODIS_Terra_Cloud_Top_Temp_Day&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fpng&TRANSPARENT=TRUE&TIME=2026-11-15) |
| `MODIS_Terra_Cloud_Top_Temp_Night` | Color-coded nighttime cloud-top temperature | 2026-11-14 | [GetMap PNG](https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=MODIS_Terra_Cloud_Top_Temp_Night&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fpng&TRANSPARENT=TRUE&TIME=2026-11-14) |

Each example returned HTTP 200 `image/png`; content completeness and Unity rendering were not checked. Capabilities advertised daily periods and `nearestValue=0` for these layers. Request an explicitly available date, handle empty imagery, and provide a date selector or fallback to an earlier available date. WMS 1.3.0 EPSG:4326 BBOX is latitude/longitude (`-90,-180,90,180`); map the resulting north-up equirectangular image to the globe and verify UV orientation. Use alpha compositing over the offline base, and label the cloud temperature legend with its actual product scale rather than interpreting the colors as precipitation or a forecast. Do not use imagery for collision or atmospheric simulation. See [GIBS access and time dimensions](https://nasa-gibs.github.io/gibs-api-docs/access-basics/) and [layer conventions](https://nasa-gibs.github.io/gibs-api-docs/access-advanced-topics/). Product-specific latency and the actual observation time can vary; no universal real-time latency is established for these three layers.

## Events and space weather

- [EONET v3 open events](https://eonet.gsfc.nasa.gov/api/v3/events?status=open) supplies curated natural-event records with categories, source links, and dated geometry; [documentation](https://eonet.gsfc.nasa.gov/docs/v3) describes filters and defaults. Plot only events with usable geometry, show source and observation date, and do not interpret open status as a real-time hazard boundary. The documented endpoint does not specify a key. Freshness depends on contributing sources; no universal latency or rate limit was verified.
- [NASA DONKI documentation](https://api.nasa.gov/) lists CME and geomagnetic storm (`GST`) APIs. For example, `https://api.nasa.gov/DONKI/GST?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD&api_key=YOUR_KEY` retrieves a date range; replace placeholders at runtime. DONKI records expert interpretations, analyses, models, forecasts and notifications; a CME is not automatically Earth-directed and an event is not a local surface-weather overlay. Display event time and source and avoid asserting impacts without supporting fields. NASA documents `DEMO_KEY` for evaluation and a personal key for regular use; keep credentials out of source control. No live DONKI request, latency guarantee, or specific rate limit was verified here.

Never expose private API keys in a distributed client; a production integration needs an appropriate protected service or an explicitly accepted public-key strategy. Cache responsibly, handle network and rate-limit errors, and label cached results with their timestamps.

## Implemented first slice

The Earth scene now has bottom map buttons (bundled Blue Marble, MODIS Terra
true color, cloud fraction), a requested-date field and Latest available action.
Capabilities are fetched at startup; an advertised future default is capped to
UTC today. A successful image response proves receipt, not complete coverage.
The UI keeps requested observation day separate from fetch time and warns of
partial coverage. NASA errors retain the previous map, with an explicit status.
The bundled Blue Marble button restores the offline image without networking.

Mouse drag orbits a fixed globe and scroll zooms. This is a coarse 2048×1024
global map; detailed regional tiles/pixel-scale inspection are not implemented.
EONET severe-storm point reports and DONKI geomagnetic-storm catalog summaries
are fetched independently for recent UTC ranges. Changing the imagery date does
not change these panels: they remain explicitly current/recent event context.
No wind, temperature, density, radiation or drag forces are inferred from these
feeds. No NASA key is required by the selected endpoints.

The shared flight snapshot/mission date contract remains with the flight owner.
Historical flight weather and altitude profiles, automated refresh/cache persistence,
and a date slider are later work. Current date text entry is an early selection UI.

Cloud layer: `MODIS_Terra_Cloud_Fraction_Day` (daytime retrieval, not forecast).
The bundled NASA legend is https://gibs.earthdata.nasa.gov/legends/MODIS_Cloud_Fraction_H.png
and shows cloud fraction in percent (0–100). White/blank areas must not be
interpreted as zero cloud cover. GIBS metadata permits no nearest-date substitution.
The Latest date action requests the advertised default or UTC today if the
service advertises a future default; unavailable dates retain the old image.

The EONET display is a startup snapshot (open severe storms, last 30 days,
maximum 20 reports); DONKI is a startup seven-day geomagnetic-storm catalog query.
Neither feed is continuously refreshed or changes with the imagery date field.

Packaged macOS checks: metadata retrieval, true-color/day cloud-fraction downloads for November 14, and return to the bundled map succeeded. EONET returned two point reports; DONKI reported no catalogued geomagnetic storms in its seven-day range. These are observed responses, not permanent conditions. Manual orbit/zoom interaction and historical imagery coverage still require rehearsal.
