# Mission integration

The Mission scene connects the map and launch presentation to the pure C# flight core through `RocketFlightDriver`. Authoritative position and velocity remain double-precision state; Unity renders camera-relative visuals. The simplified mission-relative inertial frame, WGS84 placement, atmosphere, drag, staging and point-mass orbital diagnostics are described in the flight-core documentation. Osculating orbit diagnostics are a model result, not a prediction of a real launch.

## Build and controls

Use the pinned Unity editor and invoke `BuildMission.Build` with `ESP_BUILD_PATH` set to the output application path. The Unity package manifest references the core as `file:../../FlightCore`, resolved from `UnityProject/Packages`. Keep the full repository layout with `FlightCore` and `UnityProject` as siblings; copying UnityProject alone will not resolve the package. Keep generated builds and caches outside Git.

The map starts with the bundled offline Blue Marble globe. Select Starbase or Kennedy LC-39A for a representative launch site. Drag to rotate, scroll to zoom, and right-click a point for modeled weather and regional imagery. Surface, Satellite and Clouds select the offline composite, dated MODIS true color and daytime cloud fraction. Satellite imagery is not wind or a live forecast. EONET and DONKI event catalog information is not an operational all-clear.

Launch begins with assistance enabled; the assistance toggle selects guided or manual flight. Manual throttle or W/S (up/down arrows) pitch input disables assistance; Space stages. Reset returns the mission to its initial state. Time-rate controls select 1×, 5× or 20×; the displayed mission time is simulation time, and the driver retains accumulated steps if it cannot keep up. Guidance and the test vehicle are game models, not operational flight software.

## Weather and scenery

At launch the driver freezes a fresh NASA GEOS weather vertical column, if available and no more than 12 hours old. Otherwise it uses a fresh Open-Meteo modeled profile up to two hours old, then the deterministic offline standard atmosphere. The selected source is shown; the frozen profile does not track later network updates or weather along the vehicle's trajectory. Wind and atmosphere affect the simulation, while satellite images do not. Model data are estimates, not measurements of exact pad conditions. Open-Meteo data are credited under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); its [hosted API terms](https://open-meteo.com/en/terms) apply, including noncommercial free-use limits.

Regional textures are NASA GIBS Landsat WELD annual imagery for the **2000 epoch**, not current launch facilities. Starbase construction postdates the imagery. Requests and bounds are recorded in `UnityProject/Assets/ESP/Art/Sites/provenance.json`. The 2 km ground is flat visual scenery, not georeferenced elevation, terrain or authoritative collision. Coordinates, elevation and structures are approximate; see [launch sites](launch-sites.md). NASA imagery is credited to NASA EOSDIS/ESDIS; [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/) applies. No NASA endorsement is implied.

## Verification and remaining scope

The flight core was merged in PR #10, and its .NET 9 CI checks passed. Offline missions at both sites and a historical NASA-weather mission at LC-39A passed at 20 ms and 10 ms simulation steps. The current Unity build passed parser checks and compilation. The packaged flight smoke test passed as detailed below.

These checks do not establish actual vehicle performance, real-world weather fidelity, physical terrain, or production reliability. macOS ARM64 is the current packaging target; Windows and Intel builds are unverified. An offline rehearsal and judging-hardware check remain before submission.

## Packaged flight checkpoint — November 14

The macOS ARM development player completed `-espFlightSmoke` with a live-fetched NASA GEOS-FP analysis valid at 09:00 UTC. It staged at approximately 148 s, verified 60 s of unpowered orbit at mission time 481.14 s (altitude about 227.5 km), displayed the actual map position, and reset successfully when switching launch sites. The high-altitude scaled Earth camera rendered successfully; it activates above 120 km. This is a rendered package test in the VM, not a Windows or judging-hardware qualification. Weather is a frozen modeled column with a visible timestamp, not instantaneous measurements along the whole flight. Source profile and camera-relative motion are shared with the headless tests.
