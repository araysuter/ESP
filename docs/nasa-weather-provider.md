# Experimental NASA GEOS-FP point profile

Run `python3 scripts/fetch_nasa_profile.py LAT LON` to write JSON to stdout. Failures are reported on stderr with nonzero exit. No key is used. Requests select the nearest 0.25° latitude × 0.3125° longitude grid point and use the latest advertised 3-hourly assimilation valid time no later than the current UTC clock. The script rejects data older than 12 hours, limits each response to 500 kB and each request to 15 seconds, rejects inconsistent response shapes/coordinates, filters invalid or below-ground rows without substituting zero, and requires at least 36 of the 72 native levels to remain. Returned native/retained/dropped counts expose partial coverage; the 36-level threshold is an application quality gate, not a NASA accuracy guarantee. Model weather is an estimate, not a measurement or a launch safety product. Save the complete output and validity time when using it; never claim it reflects the current instant.

Source: NASA GMAO GEOS-FP OPeNDAP assimilation `inst3_3d_asm_Nv` at https://opendap.nccs.nasa.gov/dods/GEOS-5/fp/0.25_deg/assim/inst3_3d_asm_Nv . NASA's current [file specification v2.0](https://gmao.gsfc.nasa.gov/media/publications/zbly36ziNFDQeVqPhUoFbmYmvh/GEOS_5_FP_File_Specification_ON4v2_0_fqkxorzhgte_FgpliLt.pdf), page 26, describes 72 co-located native mid-level H (m), PL (Pa), T (K), U and V (m/s), and PS (Pa), every 3 hours. This avoids the ambiguous pressure-level H described as edge heights. NASA's [earlier GEOS-FP specification](https://gmao.gsfc.nasa.gov/pubs/docs/Lucchesi617.pdf) calls native H geopotential height; the newer specification says mid-layer heights without explicitly restating the height datum. We assume it remains geopotential height and convert to approximate geometric height using z=R H/(R-H), R=6,371,000 m. This spherical approximation neglects latitude, geoid, and variable gravity effects and is not a WGS84 ellipsoidal altitude guarantee. The native-level interpretation and conversion should be independently confirmed before precision use. Below-ground pressure levels are dropped using surface pressure. Levels are sorted by altitude; extrapolation to surface or above model top is a caller decision.

Six separately bounded DAP ASCII requests are used because a combined request timed out during qualification. Availability, latency, layout, upstream clock and cadence can change; handle failure and retain the existing offline atmosphere fallback. The supplied profile can be evaluated by the standalone C# force probe below; Unity flight integration remains separate. The test on 2026-11-14 used LC-39A coordinates 28.608389,-80.604333, nearest grid 28.5,-80.625, 09:00 UTC analysis, and returned 72 increasing levels (~66 m to ~76.1 km); the analysis lagged the current date by several hours and must be labeled with its validity time. Future invocations may correctly reject that same snapshot as stale.


## Exercise the actual data-to-force path

```
python3 scripts/fetch_nasa_profile.py 28.608422 -80.604133 > profile.json
dotnet run --project Atmosphere.Probe -- profile.json
```

The probe checks the source timestamp against UTC and uses a **12-hour maximum age** for this experimental 3-hourly assimilation provider. This is a different freshness policy from the existing 2-hour Open-Meteo profile. Validity time, age and provider are printed; future/stale profiles select the standard atmosphere. This lag is why the result is described as latest available analysis, not instantaneous live weather. The probe uses a synthetic 300 m/s vertical airframe case at four altitudes and reports wind-aware drag versus baseline. It does not launch or propagate a rocket.

For reproducible historical verification, the bundled NASA sample is explicitly replayed:

```
dotnet run --project Atmosphere.Probe -- data/weather/geos-fp-lc39a-20261114T0900Z.json --at 2026-11-14T12:00:00Z
python3 -m unittest discover -s tests -p 'test_nasa_weather.py'
```

The profile retains requested and nearest-grid coordinates; the C# profile records the grid column coordinates. Latitude is nearest-neighbor at 0.25 degrees and longitude at 0.3125 degrees. No bilinear horizontal interpolation, vertical wind, gust, surface-observation or trajectory sampling is supplied. Retain the height-datum approximation above in any user-facing realism claims. NASA GMAO GEOS-FP is the data source; NASA does not endorse this game.
