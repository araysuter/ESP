# Point weather

`PointWeather` requests modeled current conditions from [Open-Meteo](https://open-meteo.com/en/docs) for a selected latitude and longitude. It does not request device location. Results are model estimates, not direct observations or NASA data; model resolution, update timing, interpolation and local terrain limit accuracy. Show the valid UTC time and the Open-Meteo attribution alongside the values. The client caches rounded coordinates for ten minutes, spaces requests by at least two seconds and clears values on failure. It is independent of flight physics.

Open-Meteo states that its hosted API is free for non-commercial use within its published limits, with attribution required under CC BY 4.0; commercial use requires an appropriate plan. Review the [provider terms and limits](https://open-meteo.com/) before distribution. Network failure must leave the offline map usable. Unity integration and target-platform network behavior have not yet been verified.

## Map interaction

Right-click a visible location on the globe, or choose Michigan, Starbase or
Southern Ocean. The inspector requests weather for that location rounded to
0.01° and a NASA regional image for the displayed map layer/day. Regional imagery
is a 1024-pixel inset covering up to 10° × 10°; requests are clamped at the poles
and date line, not stitched across the seam. Sensor resolution still limits detail.

The weather valid time is displayed separately from the satellite observation
day. Ten-minute weather refresh and a bounded ten-minute in-memory cache reduce
requests; latest NASA layer metadata refreshes every 15 minutes when enabled.
This is not a forecast raster covering every map pixel and does not drive flight
forces. Wind is at 10 m above ground, temperature at 2 m, and displayed pressure
is reduced to mean sea level—not rocket-altitude atmospheric pressure.

No API keys are required by the currently selected GIBS, EONET, direct CCMC/DONKI,
or Open-Meteo endpoints. This does not imply every NASA API is keyless. A free
NASA API key or Earthdata login may be needed for different products later.

## Verification checkpoint

Unity compiled the inspector and passed analytic globe-picking tests for equator,
poles, inside-to-outside rays and misses. The packaged Mac app fetched Michigan
point weather and regional NASA imagery. Direct provider checks also returned
non-null current values at (-50,120), (0,179.9), (89,0), and (-89,0).
These samples demonstrate tested access, not continuous coverage or accuracy at
every place/time. Requests use `cell_selection=nearest`; returned model-grid
coordinates and valid-time interval are displayed. Missing numeric fields are
rejected rather than silently shown as zeros. Manual mouse interaction and the
10/15-minute timer durations still need a longer rehearsal.

The NASA inset clears on a new selection so an old image cannot be mistaken for
the selected point. The static Blue Marble inset carries no observation date.
