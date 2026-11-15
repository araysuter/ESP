# NASA API access in downloadable players

The repository Actions secret `WEATHER_API_KEY` holds the developer's NASA key.
It is used only by `.github/workflows/nasa-feed.yml`, never by Unity build jobs.
The refresh job fetches NASA DONKI GST through api.nasa.gov with the key in a
request header, validates and allow-lists fields, then publishes `donki.json` to
the public `nasa-data` branch. The dedicated data branch contains no application
code or credentials. The script does not print keys, upstream bodies or exception
text. Actions can be delayed; the twice-hourly schedule is not a freshness SLA.

Both macOS and Windows players read the same HTTPS feed and refresh every 30
minutes. They show the cache timestamp and reject snapshots older than two hours
or more than five minutes in the future. Invalid, unavailable or stale feeds use
the existing public CCMC DONKI endpoint. If both fail, the status says unavailable;
an empty storm catalog is never presented as an all-clear. No user key setup is
required in distributed packages. Previously built packages need a new build to
include this feed client.

This NASA key is not a general weather API. DONKI reports **space weather events**;
it does not provide launch-site atmospheric density, winds or cloud opacity.
The simulator already obtains numerical profiles from NASA GEOS-FP, Earth imagery
from GIBS, and event markers from EONET using their keyless endpoints. Those paths
and offline fallbacks remain separate. POWER's daily historical weather and EPIC's
sunlit-disc photos from the hackathon guide are not substitutes for current local
vertical profiles or global instantaneous clouds.

NASA API documentation: https://api.nasa.gov/
DONKI documentation: https://ccmc.gsfc.nasa.gov/tools/DONKI/
Supported by the Michigan Space Grant Consortium, NASA grant 80NSSC20M0124.
